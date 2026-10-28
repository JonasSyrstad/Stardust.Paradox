using System.Data;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;
using Stardust.Paradox.GremlinStudio.ViewModels;

namespace Stardust.Paradox.GremlinStudio;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private static readonly Converters.EpochToDateTimeTooltipConverter _epochTooltipConverter = new();

    /// <summary>
    /// Cached reference to the GraphCanvasBorder element.
    /// </summary>
    private Border? _graphCanvasBorder;

    /// <summary>
    /// The DataGridRow being edited, used to apply/remove the edit highlight.
    /// </summary>
    private DataGridRow? _editingDataGridRow;
    private Brush? _editingRowOriginalBackground;

    /// <summary>
    /// Gets the GraphCanvasBorder element, searching the visual tree if not cached.
    /// </summary>
    private Border? GraphCanvasBorder
    {
        get
        {
            if (_graphCanvasBorder != null)
                return _graphCanvasBorder;

            // Search the visual tree for the named element
            _graphCanvasBorder = FindVisualChild<Border>(this, "GraphCanvasBorder");
            return _graphCanvasBorder;
        }
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        HookEditorNavigation(viewModel);

        PreviewMouseDown += CloseHistoryPopupOnOutsideClick;
        Deactivated += (_, _) => CloseHistoryPopup();

        // Update maximize button icon and handle maximize bounds
        StateChanged += OnWindowStateChanged;

        // Subscribe to settings panel toggle event
        _viewModel.ToggleSettingsPanelRequested += (s, e) => ToggleSettingsPanel();

        // Subscribe to welcome guide step navigation
        _viewModel.WelcomeGuide.StepNavigationRequested += OnGuideStepNavigationRequested;

        _viewModel.FocusConnectionsRequested += (_, _) =>
        {
            ExpandButton_Click(this, new RoutedEventArgs());
            if (FindName("ConnectionFilterBox") is TextBox filterBox)
            {
                filterBox.Focus();
                filterBox.SelectAll();
            }
        };

        if (FindResource("GroupedConnections") is CollectionViewSource connectionsView)
        {
            connectionsView.Filter += (_, e) =>
                e.Accepted = e.Item is Core.Connections.GremlinConnectionMetadata c && _viewModel.MatchesConnectionFilter(c);
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ConnectionFilter))
                    connectionsView.View?.Refresh();
            };
        }

        // Hook into window messages for proper multi-monitor maximize support
        SourceInitialized += OnSourceInitialized;

        RestoreWindowLayout();
        AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
            new System.Windows.Controls.Primitives.DragCompletedEventHandler(OnSplitterDragCompleted), true);
        Closing += OnWindowClosing;
    }

    private bool _shutdownConfirmed;
    private bool _shutdownInProgress;

    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_shutdownConfirmed) return;

        // Fast path: nothing needs a prompt, so persist synchronously and let the window close.
        if (!_viewModel.HasUnsavedFileTabs)
        {
            _shutdownConfirmed = true;
            try
            {
                _viewModel.SaveSessionState();
                SaveWindowLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Saving session on shutdown failed: {ex}");
            }
            return;
        }

        e.Cancel = true;
        if (_shutdownInProgress) return;
        _shutdownInProgress = true;

        var canClose = true;
        try
        {
            canClose = await _viewModel.PrepareForShutdownAsync();
            if (canClose) SaveWindowLayout();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shutdown preparation failed: {ex}");
        }
        finally
        {
            _shutdownInProgress = false;
        }

        if (!canClose) return;

        _shutdownConfirmed = true;
        // Close() cannot be called re-entrantly while the Closing event is still on the stack.
        await Dispatcher.InvokeAsync(Close, System.Windows.Threading.DispatcherPriority.Background);
    }

    private sealed record WindowLayout(
        double Left, double Top, double Width, double Height, bool Maximized, double LeftPanelWidth,
        bool LeftPanelCollapsed = false,
        string? MonitorDeviceName = null,
        int[]? NormalPosition = null,
        Dictionary<string, PanelRow[]>? Panels = null);

    private sealed record PanelRow(double Value, GridUnitType Unit);

    private const string LayoutTagPrefix = "Layout:";
    private readonly Dictionary<string, PanelRow[]> _panelLayouts = new();
    private WindowLayout? _pendingPlacement;

    private void RestoreWindowLayout()
    {
        try
        {
            var path = Core.Storage.AppDataPaths.WindowLayoutFilePath;
            if (!System.IO.File.Exists(path)) return;

            var layout = System.Text.Json.JsonSerializer.Deserialize<WindowLayout>(System.IO.File.ReadAllText(path));
            if (layout == null || layout.Width < 400 || layout.Height < 300) return;

            Width = layout.Width;
            Height = layout.Height;

            if (layout.NormalPosition is { Length: 4 } && IsMonitorAvailable(layout.MonitorDeviceName, layout.NormalPosition))
            {
                // Applied in SourceInitialized via SetWindowPlacement (device pixels, DPI-safe)
                _pendingPlacement = layout;
            }
            else if (layout.NormalPosition == null && layout.MonitorDeviceName == null &&
                     new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                         SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight)
                         .IntersectsWith(new Rect(layout.Left, layout.Top, layout.Width, layout.Height)))
            {
                // Layout saved by an older version without monitor information
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = layout.Left;
                Top = layout.Top;
            }
            else
            {
                // The saved monitor is gone: open centered on the primary monitor
                var workArea = SystemParameters.WorkArea;
                Width = Math.Min(Width, workArea.Width);
                Height = Math.Min(Height, workArea.Height);
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = workArea.Left + (workArea.Width - Width) / 2;
                Top = workArea.Top + (workArea.Height - Height) / 2;
            }

            if (layout.Maximized && _pendingPlacement == null) WindowState = WindowState.Maximized;

            if (layout.LeftPanelWidth >= LeftPanelColumn.MinWidth && layout.LeftPanelWidth <= LeftPanelColumn.MaxWidth)
            {
                LeftPanelColumn.Width = new GridLength(layout.LeftPanelWidth);
            }
            if (layout.LeftPanelCollapsed)
            {
                CollapseButton_Click(this, new RoutedEventArgs());
            }

            if (layout.Panels != null)
            {
                foreach (var (key, rows) in layout.Panels) _panelLayouts[key] = rows;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore window layout: {ex.Message}");
        }
    }

    private void ApplyPendingWindowPlacement(nint hwnd)
    {
        if (_pendingPlacement?.NormalPosition is not { Length: 4 } pos) return;
        try
        {
            var placement = new WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<WINDOWPLACEMENT>(),
                showCmd = SW_HIDE,
                rcNormalPosition = new RECT { left = pos[0], top = pos[1], right = pos[2], bottom = pos[3] }
            };
            SetWindowPlacement(hwnd, ref placement);
            if (_pendingPlacement.Maximized) WindowState = WindowState.Maximized;
        }
        finally
        {
            _pendingPlacement = null;
        }
    }

    private static bool IsMonitorAvailable(string? deviceName, int[] position)
    {
        var rect = new RECT { left = position[0], top = position[1], right = position[2], bottom = position[3] };
        var monitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONULL);
        if (monitor == nint.Zero) return false;
        if (string.IsNullOrEmpty(deviceName)) return true;

        var found = false;
        EnumDisplayMonitors(nint.Zero, nint.Zero, (nint hMonitor, nint hdc, ref RECT monitorRect, nint data) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfoEx(hMonitor, ref info) && string.Equals(info.szDevice, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                return false;
            }
            return true;
        }, nint.Zero);
        return found;
    }

    private void OnSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (e.OriginalSource is GridSplitter { Parent: Grid { Tag: string tag } grid } && tag.StartsWith(LayoutTagPrefix, StringComparison.Ordinal))
        {
            _panelLayouts[tag] = grid.RowDefinitions.Select(r => new PanelRow(r.Height.Value, r.Height.GridUnitType)).ToArray();
        }
    }

    private void LayoutGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Grid { Tag: string tag } grid || !_panelLayouts.TryGetValue(tag, out var rows)) return;
        if (rows.Length != grid.RowDefinitions.Count) return;
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i].Value < 0 || double.IsNaN(rows[i].Value)) return;
        }
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions[i].Height = new GridLength(rows[i].Value, rows[i].Unit);
        }
    }

    private void SaveWindowLayout()
    {
        try
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            var collapsed = LeftPanel.Visibility != Visibility.Visible;
            var panelWidth = collapsed
                ? (_lastLeftPanelWidth.IsAbsolute ? _lastLeftPanelWidth.Value : 280)
                : (LeftPanelColumn.ActualWidth > 0 ? LeftPanelColumn.ActualWidth : 280);

            string? monitorName = null;
            int[]? normalPosition = null;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != nint.Zero)
            {
                var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
                if (GetWindowPlacement(hwnd, ref placement))
                {
                    var r = placement.rcNormalPosition;
                    normalPosition = [r.left, r.top, r.right, r.bottom];
                }
                var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (GetMonitorInfoEx(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info))
                {
                    monitorName = info.szDevice;
                }
            }

            var layout = new WindowLayout(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                WindowState == WindowState.Maximized, panelWidth,
                collapsed, monitorName, normalPosition, new Dictionary<string, PanelRow[]>(_panelLayouts));
            System.IO.File.WriteAllText(Core.Storage.AppDataPaths.WindowLayoutFilePath,
                System.Text.Json.JsonSerializer.Serialize(layout));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save window layout: {ex.Message}");
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeRestoreButton();

        // When maximizing, adjust bounds to the current screen's work area
        if (WindowState == WindowState.Maximized)
        {
            AdjustMaximizedBounds();
        }
    }

    private void AdjustMaximizedBounds()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);

        if (monitor != nint.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var workArea = monitorInfo.rcWork;

                // Get DPI scale for this window
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                // Convert screen pixels to WPF device-independent units
                double width = (workArea.right - workArea.left) / dpiX;
                double height = (workArea.bottom - workArea.top) / dpiY;
                double left = workArea.left / dpiX;
                double top = workArea.top / dpiY;

                // Set MaxWidth/MaxHeight to constrain the window
                MaxWidth = width;
                MaxHeight = height;
            }
        }
    }


    #region Multi-Monitor Maximize Support

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowProc);
        ApplyPendingWindowPlacement(handle);
    }

    private nint WindowProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return nint.Zero;
    }

    private static void WmGetMinMaxInfo(nint hwnd, nint lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        // Get the monitor that the window is currently on
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != nint.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                // Get DPI for the window to properly scale values
                uint dpi = GetDpiForWindow(hwnd);
                double dpiScale = dpi / 96.0;

                // rcWork is the work area (excludes taskbar)
                // rcMonitor is the full monitor area
                var rcWorkArea = monitorInfo.rcWork;
                var rcMonitorArea = monitorInfo.rcMonitor;

                // Calculate work area dimensions
                int workWidth = rcWorkArea.right - rcWorkArea.left;
                int workHeight = rcWorkArea.bottom - rcWorkArea.top;

                // Set the maximized position relative to the monitor's work area
                mmi.ptMaxPosition.x = rcWorkArea.left - rcMonitorArea.left;
                mmi.ptMaxPosition.y = rcWorkArea.top - rcMonitorArea.top;

                // Set the maximized size to the work area size
                mmi.ptMaxSize.x = workWidth;
                mmi.ptMaxSize.y = workHeight;

                // Also set track size to prevent issues with resizing
                mmi.ptMaxTrackSize.x = workWidth;
                mmi.ptMaxTrackSize.y = workHeight;
            }
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    #region Win32 Interop

    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const uint MONITOR_DEFAULTTONULL = 0x00000000;
    private const int SW_HIDE = 0;

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdc, ref RECT rect, nint data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref RECT rect, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoEx(nint hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT placement);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT placement);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    #endregion


    #endregion

    /// <summary>
    /// Toggles the left settings panel between collapsed and expanded states.
    /// </summary>
    private void ToggleSettingsPanel()
    {
        var leftPanel = FindName("LeftPanel") as FrameworkElement;
        if (leftPanel == null) return;

        if (leftPanel.Visibility == Visibility.Visible)
        {
            CollapseButton_Click(this, new RoutedEventArgs());
        }
        else
        {
            ExpandButton_Click(this, new RoutedEventArgs());
        }
    }

    /// <summary>
    /// Finds a child element in the visual tree by name.
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T element && element.Name == name)
                return element;

            var found = FindVisualChild<T>(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    #region Window Chrome Buttons

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateMaximizeRestoreButton()
    {
        var maximizeButton = FindName("MaximizeButton") as Button;
        if (maximizeButton != null)
        {
            maximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE739";
            maximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        }
    }

    #endregion

    private void ResultDataGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var columnName = e.PropertyName;

        // Get ColumnIsProperty from the current tab's ViewModel
        var tab = _viewModel.SelectedTab;
        var columnIsProperty = tab?.ColumnIsProperty ?? _viewModel.ColumnIsProperty;

        // Check if this column is a property or instance value
        if (columnIsProperty.TryGetValue(columnName, out var isProperty))
        {
            // Create a styled header with color coding
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Add colored indicator
            var indicator = new TextBlock
            {
                Text = isProperty ? "●" : "◆",
                Foreground = isProperty
                    ? new SolidColorBrush(Color.FromRgb(86, 156, 214))  // Blue for properties
                    : new SolidColorBrush(Color.FromRgb(78, 201, 176)), // Green for instance values
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = columnName,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(indicator);
            headerPanel.Children.Add(label);

            e.Column.Header = headerPanel;
        }

        // Column names may contain characters that are illegal in a property path (e.g. '.', '@', '+'),
        // so bind through the DataRowView indexer with the name escaped.
        var indexerPath = CreateIndexerPath(columnName);
        e.Column.SortMemberPath = columnName;

        // Add epoch date/time tooltip to cell values
        if (e.Column is DataGridTextColumn textColumn)
        {
            textColumn.Binding = new Binding { Path = indexerPath };
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(
                FrameworkElement.ToolTipProperty,
                new Binding { Path = indexerPath, Converter = _epochTooltipConverter }));
            textColumn.ElementStyle = style;
        }
        else if (e.Column is DataGridBoundColumn boundColumn)
        {
            boundColumn.Binding = new Binding { Path = indexerPath };
        }
    }

    private static PropertyPath CreateIndexerPath(string columnName)
    {
        var escaped = new System.Text.StringBuilder(columnName.Length + 8);
        foreach (var ch in columnName)
        {
            if (ch is '^' or ',' or '[' or ']' or '(' or ')' or '.' or '/' or ' ')
            {
                escaped.Append('^');
            }
            escaped.Append(ch);
        }
        return new PropertyPath("[" + escaped + "]");
    }

    private void ResultDataGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (sender is not DataGrid dataGrid) return;

        var tab = _viewModel.SelectedTab;
        if (tab == null) return;

        // Read columns in visual display order and extract the property name
        var columnOrder = dataGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.SortMemberPath)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        tab.UpdateColumnDisplayOrder(columnOrder);
    }

    private void CopyCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not DataGrid dataGrid) return;

        var currentCell = dataGrid.CurrentCell;
        if (currentCell.Column == null || currentCell.Item is not DataRowView rowView) return;

        var columnName = currentCell.Column.SortMemberPath;
        if (!string.IsNullOrEmpty(columnName) && rowView.Row.Table.Columns.Contains(columnName))
        {
            Clipboard.SetText(rowView[columnName]?.ToString() ?? string.Empty);
        }
    }

    private void CopyRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not DataGrid dataGrid) return;

        if (dataGrid.CurrentItem is not DataRowView rowView) return;

        var values = rowView.Row.ItemArray;
        var text = string.Join("\t", values.Select(v => v?.ToString() ?? string.Empty));
        Clipboard.SetText(text);
    }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not DataGrid dataGrid) return;

        var tab = _viewModel.SelectedTab;
        if (tab == null) return;

        if (dataGrid.CurrentItem is not DataRowView rowView) return;

        // Enable editing on the DataGrid and mark non-editable columns
        dataGrid.IsReadOnly = false;
        foreach (var column in dataGrid.Columns)
        {
            var colName = column.SortMemberPath;
            if (!string.IsNullOrEmpty(colName))
            {
                column.IsReadOnly = QueryTabViewModel.IsNonEditableColumn(colName)
                    || (tab.ColumnIsProperty.TryGetValue(colName, out var isProp) && !isProp);
            }
        }

        // Wire up the commit callback so the ViewModel can flush pending
        // DataGrid cell edits before reading values for change detection.
        tab.CommitDataGridEdit = () =>
        {
            dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            dataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        };

        // Select and scroll to the editing row so it is clearly visible
        dataGrid.SelectedItem = rowView;
        dataGrid.ScrollIntoView(rowView);

        // Apply an accent highlight to the editing row
        dataGrid.UpdateLayout();
        _editingDataGridRow = dataGrid.ItemContainerGenerator.ContainerFromItem(rowView) as DataGridRow;
        if (_editingDataGridRow != null)
        {
            _editingRowOriginalBackground = _editingDataGridRow.Background;
            _editingDataGridRow.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x78, 0xD4));
        }

        // Begin editing on the view model
        tab.BeginEditRow(rowView);

        // Subscribe to IsEditingRow changes to restore read-only state when done
        tab.PropertyChanged -= OnTabEditingRowChanged;
        tab.PropertyChanged += OnTabEditingRowChanged;
    }

    private void OnTabEditingRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(QueryTabViewModel.IsEditingRow)) return;
        if (sender is not QueryTabViewModel tab) return;

        if (!tab.IsEditingRow)
        {
            tab.PropertyChanged -= OnTabEditingRowChanged;
            tab.CommitDataGridEdit = null;

            // Restore the editing row highlight and DataGrid read-only state
            Dispatcher.InvokeAsync(() =>
            {
                if (_editingDataGridRow != null)
                {
                    _editingDataGridRow.Background = _editingRowOriginalBackground
                        ?? (Brush)FindResource("PrimaryBackgroundBrush");
                    _editingDataGridRow = null;
                    _editingRowOriginalBackground = null;
                }

                var dataGrid = FindVisualChild<DataGrid>(this, "ResultDataGrid");
                if (dataGrid != null)
                {
                    dataGrid.IsReadOnly = true;
                }
            });
        }
    }

    private void ResultDataGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        var tab = _viewModel.SelectedTab;
        if (tab == null || !tab.IsEditingRow)
        {
            e.Cancel = true;
            return;
        }

        // Block editing non-editable columns
        var colName = e.Column.SortMemberPath;
        if (!string.IsNullOrEmpty(colName) && QueryTabViewModel.IsNonEditableColumn(colName))
        {
            e.Cancel = true;
        }
    }

    private void QueryEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        var tab = _viewModel.SelectedTab;
        if (tab == null) return;

        if (e.Delta > 0)
        {
            tab.ZoomEditorIn();
        }
        else
        {
            tab.ZoomEditorOut();
        }

        e.Handled = true;
    }

    private GridLength _lastLeftPanelWidth = new(280);

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        // Find elements by name
        var leftPanel = FindName("LeftPanel") as FrameworkElement;
        var leftPanelColumn = FindName("LeftPanelColumn") as ColumnDefinition;
        var expandButton = FindName("ExpandButton") as Button;
        var panelSplitter = FindName("PanelSplitter") as GridSplitter;

        if (leftPanel == null || leftPanelColumn == null || expandButton == null)
            return;

        // Save current width before collapsing
        _lastLeftPanelWidth = leftPanelColumn.Width;

        // Collapse the left panel
        leftPanel.Visibility = Visibility.Collapsed;
        leftPanelColumn.Width = new GridLength(0);
        leftPanelColumn.MinWidth = 0;

        // Hide splitter, show expand button
        if (panelSplitter != null)
            panelSplitter.Visibility = Visibility.Collapsed;
        expandButton.Visibility = Visibility.Visible;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        // Find elements by name
        var leftPanel = FindName("LeftPanel") as FrameworkElement;
        var leftPanelColumn = FindName("LeftPanelColumn") as ColumnDefinition;
        var expandButton = FindName("ExpandButton") as Button;
        var panelSplitter = FindName("PanelSplitter") as GridSplitter;

        if (leftPanel == null || leftPanelColumn == null || expandButton == null)
            return;

        // Restore the left panel
        leftPanel.Visibility = Visibility.Visible;
        leftPanelColumn.MinWidth = 200;
        leftPanelColumn.Width = _lastLeftPanelWidth;

        // Show splitter, hide expand button
        if (panelSplitter != null)
            panelSplitter.Visibility = Visibility.Visible;
        expandButton.Visibility = Visibility.Collapsed;
    }

    private void TreeViewExpandCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ViewModels.TreeNodeViewModel node)
        {
            node.ToggleExpandCollapseAll();

            // Force the TreeView to update by finding and updating all TreeViewItems
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles mouse down on a graph node for dragging.
    /// </summary>
    private void GraphNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ViewModels.GraphNodeViewModel node)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            if (vm == null) return;

            var border = GraphCanvasBorder;
            if (border == null) return;

            if (e.ClickCount == 2)
            {
                vm.SelectedTab?.ExpandGraphNodeCommand.Execute(node);
                e.Handled = true;
                return;
            }

            // Select the node
            vm.SelectGraphNodeCommand.Execute(node);

            // Start dragging
            var startPoint = e.GetPosition(border);
            StartNodeDrag(node, startPoint);

            e.Handled = true;
        }
    }

    #region Graph Pan/Drag

    private bool _isPanning;
    private bool _isDraggingNode;
    private Point _dragStartPoint;
    private double _dragStartTranslateX;
    private double _dragStartTranslateY;

    private void GraphCanvasBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel { SelectedTab: { } tab })
        {
            tab.GraphViewportSize = e.NewSize;
        }
    }

    private void GraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var border = sender as Border;
        if (border == null) return;

        var vm = DataContext as ViewModels.MainViewModel;
        if (vm?.SelectedTab == null) return;

        // Left or middle button pans the canvas (when clicking on background, not on nodes)
        if (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _dragStartPoint = e.GetPosition(border);
            _dragStartTranslateX = vm.SelectedTab.GraphPanX;
            _dragStartTranslateY = vm.SelectedTab.GraphPanY;
            border.CaptureMouse();
            border.Cursor = Cursors.Hand;
        }
    }

    private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var border = sender as Border;
        if (border == null) return;

        var vm = DataContext as ViewModels.MainViewModel;
        if (vm?.SelectedTab == null) return;

        var currentPoint = e.GetPosition(border);

        if (_isDraggingNode)
        {
            vm.UpdateNodeDrag(currentPoint.X, currentPoint.Y);
        }
        else if (_isPanning)
        {
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var deltaY = currentPoint.Y - _dragStartPoint.Y;

            vm.SelectedTab.GraphPanX = _dragStartTranslateX + deltaX;
            vm.SelectedTab.GraphPanY = _dragStartTranslateY + deltaY;
        }
    }

    private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var border = sender as Border;
        var vm = DataContext as ViewModels.MainViewModel;

        if (_isPanning)
        {
            _isPanning = false;
            border?.ReleaseMouseCapture();
            if (border != null) border.Cursor = Cursors.Arrow;
        }

        if (_isDraggingNode)
        {
            _isDraggingNode = false;
            vm?.EndNodeDrag();
            border?.ReleaseMouseCapture();
            if (border != null) border.Cursor = Cursors.Arrow;
        }
    }

    private void GraphCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        // Don't stop dragging on leave - mouse is captured
    }

    private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var vm = DataContext as ViewModels.MainViewModel;
        if (vm?.SelectedTab == null) return;

        const double panSpeed = 30.0;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Ctrl+Scroll = Zoom
            if (e.Delta > 0)
            {
                vm.SelectedTab.ZoomInCommand.Execute(null);
            }
            else if (e.Delta < 0)
            {
                vm.SelectedTab.ZoomOutCommand.Execute(null);
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            // Shift+Scroll = Pan left/right
            if (e.Delta > 0)
            {
                vm.SelectedTab.GraphPanX += panSpeed;
            }
            else if (e.Delta < 0)
            {
                vm.SelectedTab.GraphPanX -= panSpeed;
            }
        }
        else
        {
            // Scroll (no modifier) = Pan up/down
            if (e.Delta > 0)
            {
                vm.SelectedTab.GraphPanY += panSpeed;
            }
            else if (e.Delta < 0)
            {
                vm.SelectedTab.GraphPanY -= panSpeed;
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// Handles node drag start from the node template.
    /// </summary>
    public void StartNodeDrag(ViewModels.GraphNodeViewModel node, Point startPoint)
    {
        var vm = DataContext as ViewModels.MainViewModel;
        if (vm == null) return;

        var border = GraphCanvasBorder;
        if (border == null) return;

        _isDraggingNode = true;
        vm.StartNodeDrag(node, startPoint.X, startPoint.Y);
        border.CaptureMouse();
        border.Cursor = Cursors.SizeAll;
    }

    #endregion

    #region Tab Selection

    private void TabHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ViewModels.QueryTabViewModel tab)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null)
            {
                vm.SelectedTab = tab;
            }
            e.Handled = true;
        }
    }

    private void TabHeader_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle
            && sender is FrameworkElement element
            && element.DataContext is ViewModels.QueryTabViewModel tab
            && DataContext is ViewModels.MainViewModel vm
            && tab.CanClose)
        {
            vm.CloseTabCommand.Execute(tab);
            e.Handled = true;
        }
    }

    private ViewModels.QueryTabViewModel? _errorNavigationTab;

    private void HookEditorNavigation(ViewModels.MainViewModel vm)
    {
        Controls.TextEditorHelper.ActiveCaretMoved += (_, pos) =>
        {
            if (vm.SelectedTab != null) vm.SelectedTab.CaretPositionText = $"Ln {pos.Line}, Col {pos.Column}";
        };

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ViewModels.MainViewModel.SelectedTab)) return;
            if (_errorNavigationTab != null) _errorNavigationTab.NavigateToPositionRequested -= OnNavigateToPositionRequested;
            _errorNavigationTab = vm.SelectedTab;
            if (_errorNavigationTab != null) _errorNavigationTab.NavigateToPositionRequested += OnNavigateToPositionRequested;
        };
        _errorNavigationTab = vm.SelectedTab;
        if (_errorNavigationTab != null) _errorNavigationTab.NavigateToPositionRequested += OnNavigateToPositionRequested;
    }

    private void OnNavigateToPositionRequested(object? sender, (int Line, int Column) pos) =>
            Controls.TextEditorHelper.NavigateTo(pos.Line, pos.Column);

    #endregion

    #region Statistics label lists

    private static ViewModels.LabelCountItem? GetStatsItem(object? source) =>
        FindAncestor<DataGridRow>(source as DependencyObject)?.Item as ViewModels.LabelCountItem;

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null && current is not T)
        {
            current = current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return current as T;
    }

    private static void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Clipboard can be temporarily locked by another process.
        }
    }

    private static string BuildLabelQuery(string label, bool isEdge) =>
        $"g.{(isEdge ? "E" : "V")}().hasLabel('{label.Replace("\\", "\\\\").Replace("'", "\\'")}')";

    private void StatsLabelGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || DataContext is not ViewModels.MainViewModel vm) return;
        var item = GetStatsItem(e.OriginalSource);
        if (item == null) return;
        vm.QueryByLabel(item.Label, isEdge: Equals(grid.Tag, "E"));
        e.Handled = true;
    }

    private void StatsLabelGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid || grid.SelectedItem is not ViewModels.LabelCountItem item) return;
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyText(item.Label);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && DataContext is ViewModels.MainViewModel vm)
        {
            vm.QueryByLabel(item.Label, isEdge: Equals(grid.Tag, "E"));
            e.Handled = true;
        }
    }

    private void StatsLabelGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var item = GetStatsItem(e.OriginalSource) ?? grid.SelectedItem as ViewModels.LabelCountItem;
        if (item == null)
        {
            e.Handled = true;
            return;
        }

        grid.SelectedItem = item;
        var isEdge = Equals(grid.Tag, "E");
        var query = BuildLabelQuery(item.Label, isEdge);
        var menu = new ContextMenu();

        var copyLabel = new MenuItem { Header = "Copy label", InputGestureText = "Ctrl+C" };
        copyLabel.Click += (_, _) => CopyText(item.Label);
        menu.Items.Add(copyLabel);

        var copyQuery = new MenuItem { Header = "Copy query", ToolTip = query };
        copyQuery.Click += (_, _) => CopyText(query);
        menu.Items.Add(copyQuery);

        menu.Items.Add(new Separator());

        var useQuery = new MenuItem { Header = "Use in query editor", InputGestureText = "Double-click" };
        useQuery.Click += (_, _) => (DataContext as ViewModels.MainViewModel)?.QueryByLabel(item.Label, isEdge);
        menu.Items.Add(useQuery);

        grid.ContextMenu = menu;
    }

    private void GraphLegendItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: ViewModels.GraphNodeViewModel node }) return;
        if (string.IsNullOrEmpty(node.Label) || DataContext is not ViewModels.MainViewModel vm) return;
        vm.QueryByLabel(node.Label, isEdge: false);
        e.Handled = true;
    }

    private void ResultGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var cell = FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
        var rowView = (cell?.DataContext ?? grid.CurrentItem) as DataRowView;

        // Make the right-clicked cell current so Copy/Edit act on it
        if (cell?.Column != null && rowView != null)
        {
            grid.CurrentCell = new DataGridCellInfo(rowView, cell.Column);
            if (grid.SelectionUnit != DataGridSelectionUnit.FullRow)
            {
                grid.SelectedCells.Clear();
                grid.SelectedCells.Add(grid.CurrentCell);
            }
        }

        // Header may be a styled StackPanel; SortMemberPath holds the real column name
        var columnName = cell?.Column?.SortMemberPath;
        string? value = null;
        if (rowView != null && !string.IsNullOrEmpty(columnName) && rowView.Row.Table.Columns.Contains(columnName))
        {
            var raw = rowView[columnName];
            value = raw is DBNull ? null : raw?.ToString();
        }

        var menu = new ContextMenu();

        void Add(string text, Action action, bool enabled = true)
        {
            var mi = new MenuItem { Header = text, IsEnabled = enabled };
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
        }

        var hasValue = !string.IsNullOrEmpty(value);
        var hasRow = rowView != null;
        var copyCell = new MenuItem { Header = "Copy Cell", InputGestureText = "Ctrl+C", IsEnabled = hasValue };
        copyCell.Click += CopyCell_Click;
        menu.Items.Add(copyCell);
        var copyRow = new MenuItem { Header = "Copy Row", IsEnabled = hasRow };
        copyRow.Click += CopyRow_Click;
        menu.Items.Add(copyRow);
        menu.Items.Add(new Separator());
        var editRow = new MenuItem { Header = "Edit Row", InputGestureText = "F2", IsEnabled = hasRow };
        editRow.Click += EditRow_Click;
        menu.Items.Add(editRow);
        menu.Items.Add(new Separator());

        var columns = rowView?.Row.Table.Columns;
        var isEdge = columns != null && (columns.Contains("inV") || columns.Contains("outV"));
        var source = isEdge ? "g.E" : "g.V";
        var elementId = GetColumnValue(rowView, "id");
        var escapedId = EscapeGremlin(elementId);
        var hasId = !string.IsNullOrEmpty(elementId);
        var isIdColumn = string.Equals(columnName, "id", StringComparison.OrdinalIgnoreCase);

        Add(isEdge ? "Query edge by id" : "Query vertex by id", () => SetEditorQuery($"{source}('{escapedId}')"), hasId);
        if (isEdge)
        {
            Add("Query out vertex", () => SetEditorQuery($"g.E('{escapedId}').outV()"), hasId);
            Add("Query in vertex", () => SetEditorQuery($"g.E('{escapedId}').inV()"), hasId);
        }
        else
        {
            Add("Query outgoing neighbours", () => SetEditorQuery($"g.V('{escapedId}').out()"), hasId);
            Add("Query incoming neighbours", () => SetEditorQuery($"g.V('{escapedId}').in()"), hasId);
        }

        var isLabelColumn = string.Equals(columnName, "label", StringComparison.OrdinalIgnoreCase);
        var canFilter = hasValue && !isIdColumn && !string.IsNullOrEmpty(columnName)
            && (isLabelColumn || (_viewModel.SelectedTab?.ColumnIsProperty.TryGetValue(columnName!, out var isProp) != true || isProp));
        var filterQuery = isLabelColumn
            ? $"{source}().hasLabel('{EscapeGremlin(value)}')"
            : $"{source}().has('{EscapeGremlin(columnName)}', '{EscapeGremlin(value)}')";
        Add(canFilter ? $"Filter by {columnName} = value" : "Filter by value", () => SetEditorQuery(filterQuery), canFilter);
        grid.ContextMenu = menu;
    }

    private static string? GetColumnValue(DataRowView? rowView, string columnName)
    {
        if (rowView == null || !rowView.Row.Table.Columns.Contains(columnName)) return null;
        var raw = rowView[columnName];
        return raw is DBNull ? null : raw?.ToString();
    }

    private static string EscapeGremlin(string? value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");

    private System.Windows.Controls.Primitives.Popup? _openHistoryPopup;

    private void HistoryDropdown_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton toggle) return;
        var toolBar = ItemsControl.ItemsControlFromItemContainer(toggle) as ToolBar
                      ?? FindLogicalAncestor<ToolBar>(toggle);
        if (toolBar?.FindName("HistoryPopup") is not System.Windows.Controls.Primitives.Popup popup) return;

        // When the toggle lives in the overflow panel it disappears as soon as the overflow closes,
        // so anchor the popup to the toolbar itself to keep it visible and browsable.
        var inOverflow = ToolBar.GetIsOverflowItem(toggle);
        popup.PlacementTarget = inOverflow ? toolBar : toggle;
        popup.HorizontalOffset = inOverflow ? Math.Max(0, toolBar.ActualWidth - 460) : 0;
    }

    private void CloseHistoryPopupOnOutsideClick(object sender, MouseButtonEventArgs e)
    {
        if (_openHistoryPopup == null) return;
        var origin = e.OriginalSource as DependencyObject;
        // Mouse events from popups (the history popup itself, its context menu, the toolbar overflow)
        // are routed to the window through the logical tree; only clicks in the window itself are "outside".
        if (origin is not Visual visual || PresentationSource.FromVisual(visual)?.RootVisual != this) return;
        if (FindAncestor<System.Windows.Controls.Primitives.ToggleButton>(origin) is { Name: "GuideTarget_HistoryDropdown" }) return;
        CloseHistoryPopup();
    }

    private void CloseHistoryPopup()
    {
        if (_openHistoryPopup != null) _openHistoryPopup.IsOpen = false;
    }

    private void HistoryPopup_Closed(object? sender, EventArgs e) => _openHistoryPopup = null;

    private void HistoryPopup_Opened(object? sender, EventArgs e)
    {
        _openHistoryPopup = sender as System.Windows.Controls.Primitives.Popup;
        if (sender is not System.Windows.Controls.Primitives.Popup { Child: { } child }) return;
        Dispatcher.BeginInvoke(
            () => FindDescendant<TextBox>(child)?.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void HistorySearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DependencyObject source) return;
        var popup = FindLogicalAncestor<System.Windows.Controls.Primitives.Popup>(source);
        var list = popup?.Child is { } child ? FindDescendant<ListBox>(child) : null;
        if (list == null || list.Items.Count == 0) return;

        if (e.Key == Key.Down)
        {
            list.SelectedIndex = 0;
            (list.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            LoadHistoryItem(list, list.Items[0] as Core.History.QueryHistoryItem);
            e.Handled = true;
        }
    }

    private void HistoryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Let the pin/remove buttons inside an item handle their own clicks
        var origin = e.OriginalSource as DependencyObject;
        if (FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(origin) != null) return;
        if (FindAncestor<ListBoxItem>(origin)?.DataContext is Core.History.QueryHistoryItem item)
        {
            LoadHistoryItem((DependencyObject)sender, item);
            e.Handled = true;
        }
    }

    private void HistoryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is ListBox { SelectedItem: Core.History.QueryHistoryItem item } list)
        {
            LoadHistoryItem(list, item);
            e.Handled = true;
        }
    }

    private void LoadHistoryItem(DependencyObject source, Core.History.QueryHistoryItem? item)
    {
        if (item == null) return;
        SetEditorQuery(item.Query);
        if (FindLogicalAncestor<System.Windows.Controls.Primitives.Popup>(source) is { } popup)
        {
            popup.IsOpen = false;
        }
        Controls.TextEditorHelper.FocusEditor();
    }

    private static T? FindLogicalAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = LogicalTreeHelper.GetParent(current)
                ?? (current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : null);
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private void SetEditorQuery(string query)
    {
        if (DataContext is ViewModels.MainViewModel { SelectedTab: { } tab })
        {
            tab.QueryText = query;
        }
    }

    #endregion

    #region Welcome Guide Navigation

    private Dialogs.KeyboardShortcutsDialog? _guideShortcutsDialog;

    /// <summary>
    /// Handles step navigation: performs the UI action then positions the bubble near the target element.
    /// </summary>
    private void OnGuideStepNavigationRequested(object? sender, WelcomeGuideStep step)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            // Close any previously opened guide dialog
            CloseGuideDialog();

            // Navigate the UI to reveal the target
            NavigateForStep(step);

            // Allow layout to settle after navigation
            await Task.Delay(100).ConfigureAwait(true);

            PositionBubbleNearTarget(step);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Performs the UI navigation action required to show the target element.
    /// </summary>
    private void NavigateForStep(WelcomeGuideStep step)
    {
        switch (step.NavigationAction)
        {
            case GuideNavigationAction.EnsureSettingsPanelOpen:
                EnsureSettingsPanelOpen();
                break;

            case GuideNavigationAction.SwitchToJsonTab:
                SwitchResultTab(0);
                break;

            case GuideNavigationAction.SwitchToTableTab:
                SwitchResultTab(1);
                break;

            case GuideNavigationAction.SwitchToTreeTab:
                SwitchResultTab(2);
                break;

            case GuideNavigationAction.SwitchToGraphTab:
                SwitchResultTab(3);
                break;

            case GuideNavigationAction.SwitchToExportTab:
                SwitchResultTab(4);
                break;

            case GuideNavigationAction.SwitchToLogTab:
                SwitchResultTab(5);
                break;

            case GuideNavigationAction.SwitchToExplainTab:
                SwitchResultTab(6);
                break;

            case GuideNavigationAction.SwitchToStatsTab:
                SwitchResultTab(7);
                break;

            case GuideNavigationAction.ShowKeyboardShortcutsDialog:
                ShowGuideShortcutsDialog();
                break;
        }
    }

    private void EnsureSettingsPanelOpen()
    {
        var leftPanel = FindName("LeftPanel") as FrameworkElement;
        if (leftPanel is { Visibility: Visibility.Collapsed })
        {
            ExpandButton_Click(this, new RoutedEventArgs());
        }
    }

    /// <summary>
    /// Scrolls the left panel ScrollViewer to bring the target element into view.
    /// </summary>
    private void ScrollTargetIntoView(FrameworkElement target)
    {
        // Walk up the visual tree to find the ScrollViewer in the left panel
        DependencyObject current = target;
        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer)
            {
                // Get position of target relative to the scroll viewer's content
                var transform = target.TransformToAncestor(scrollViewer);
                var targetRect = transform.TransformBounds(
                    new Rect(0, 0, target.ActualWidth, target.ActualHeight));

                // Scroll to make the target visible, centered if possible
                var verticalOffset = targetRect.Top - scrollViewer.ViewportHeight / 3;
                scrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, scrollViewer.VerticalOffset + verticalOffset));
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }

    private void SwitchResultTab(int tabIndex)
    {
        if (_viewModel.SelectedTab is not null)
        {
            _viewModel.SelectedTab.SelectedResultViewIndex = tabIndex;
        }
    }

    private void ShowGuideShortcutsDialog()
    {
        _guideShortcutsDialog = new Dialogs.KeyboardShortcutsDialog { Owner = this };
        _guideShortcutsDialog.Show();
    }

    private void CloseGuideDialog()
    {
        if (_guideShortcutsDialog is { IsLoaded: true })
        {
            _guideShortcutsDialog.Close();
        }
        _guideShortcutsDialog = null;
    }

    /// <summary>
    /// Finds the target element and sets bubble position on the view model.
    /// </summary>
    private void PositionBubbleNearTarget(WelcomeGuideStep step)
    {
        var guideVm = _viewModel.WelcomeGuide;
        var target = FindVisualChild<FrameworkElement>(this, step.TargetElementName);

        if (target is null || !target.IsVisible)
        {
            // Fallback: center the bubble
            guideVm.TargetCenterX = ActualWidth / 2;
            guideVm.TargetCenterY = ActualHeight / 2;
            guideVm.BubbleLeft = ActualWidth / 2 - 160;
            guideVm.BubbleTop = ActualHeight / 2 - 80;
            UpdateOverlayArrow();
            return;
        }

        // For left-panel elements, scroll into view first
        if (step.NavigationAction == GuideNavigationAction.EnsureSettingsPanelOpen)
        {
            ScrollTargetIntoView(target);
        }

        // Get target position relative to this window
        var targetPos = target.TranslatePoint(new Point(0, 0), this);
        var targetWidth = target.ActualWidth;
        var targetHeight = target.ActualHeight;

        // Store actual target center for arrow aiming
        var targetCenterX = targetPos.X + targetWidth / 2;
        var targetCenterY = targetPos.Y + targetHeight / 2;
        guideVm.TargetCenterX = targetCenterX;
        guideVm.TargetCenterY = targetCenterY;

        const double bubbleWidth = 320;
        const double bubbleHeight = 180;
        const double gap = 14;

        double left, top;

        switch (step.ArrowSide)
        {
            case BubbleArrowSide.Top:
                // Bubble below target, arrow points up
                left = targetCenterX - bubbleWidth / 2;
                top = targetPos.Y + targetHeight + gap;
                break;

            case BubbleArrowSide.Bottom:
                // Bubble above target, arrow points down
                left = targetCenterX - bubbleWidth / 2;
                top = targetPos.Y - bubbleHeight - gap;
                break;

            case BubbleArrowSide.Left:
                // Bubble to the right of target, arrow points left
                left = targetPos.X + targetWidth + gap;
                top = targetCenterY - 50;
                break;

            case BubbleArrowSide.Right:
                // Bubble to the left of target, arrow points right
                left = targetPos.X - bubbleWidth - gap;
                top = targetCenterY - 50;
                break;

            default:
                left = ActualWidth / 2 - 160;
                top = ActualHeight / 2 - 80;
                break;
        }

        // Clamp to window bounds
        left = Math.Clamp(left, 10, Math.Max(10, ActualWidth - bubbleWidth - 10));
        top = Math.Clamp(top, 50, Math.Max(50, ActualHeight - bubbleHeight - 10));

        guideVm.BubbleLeft = left;
        guideVm.BubbleTop = top;

        UpdateOverlayArrow();
    }

    /// <summary>
    /// Triggers arrow repositioning on the overlay control after bubble position changes.
    /// </summary>
    private void UpdateOverlayArrow()
    {
        var overlay = FindVisualChild<Controls.WelcomeGuideOverlay>(this, string.Empty);
        // Search for the overlay by type since it may not have x:Name
        overlay ??= FindVisualChildByType<Controls.WelcomeGuideOverlay>(this);
        overlay?.Dispatcher.InvokeAsync(
            () => overlay.UpdateArrowPosition(),
            System.Windows.Threading.DispatcherPriority.Render);
    }

    /// <summary>
    /// Finds a child element in the visual tree by type.
    /// </summary>
    private static T? FindVisualChildByType<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var found = FindVisualChildByType<T>(child);
            if (found is not null)
                return found;
        }
        return null;
    }

    #endregion

    #region Snippets

    private void SnippetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.InsertSnippetCommand.CanExecute(null))
        {
            _viewModel.InsertSnippetCommand.Execute(null);
        }
    }

    private void VariableSetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.EditVariableSetCommand.CanExecute(null))
        {
            _viewModel.EditVariableSetCommand.Execute(null);
        }
    }

    #endregion

    #region Execution Log Copy

    private void ExecutionLogDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (sender is not DataGrid dataGrid) return;
            if (dataGrid.CurrentItem is not Core.Execution.ExecutionLogEntry logEntry) return;

            // If a specific cell is selected, copy that cell's value; otherwise copy the query
            var currentCell = dataGrid.CurrentCell;
            if (currentCell.Column != null)
            {
                var cellValue = GetLogCellValue(logEntry, currentCell.Column);
                if (cellValue != null)
                {
                    Clipboard.SetText(cellValue);
                    e.Handled = true;
                    return;
                }
            }

            Clipboard.SetText(logEntry.Query);
            e.Handled = true;
        }
    }

    private void LogCopyCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not DataGrid dataGrid) return;
        if (dataGrid.CurrentItem is not Core.Execution.ExecutionLogEntry logEntry) return;

        var currentCell = dataGrid.CurrentCell;
        if (currentCell.Column != null)
        {
            var cellValue = GetLogCellValue(logEntry, currentCell.Column);
            Clipboard.SetText(cellValue ?? string.Empty);
        }
    }

    private void LogCopyQuery_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not DataGrid dataGrid) return;
        if (dataGrid.CurrentItem is not Core.Execution.ExecutionLogEntry logEntry) return;

        Clipboard.SetText(logEntry.Query);
    }

    private void LogCopyRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not DataGrid dataGrid) return;
        if (dataGrid.CurrentItem is not Core.Execution.ExecutionLogEntry logEntry) return;

        var text = string.Join("\t",
            logEntry.Timestamp.ToString("HH:mm:ss"),
            logEntry.Query,
            $"{logEntry.Duration.TotalMilliseconds:F0}ms",
            logEntry.ResultCount.ToString(),
            logEntry.RequestUnits?.ToString("F2") ?? "",
            logEntry.ErrorMessage ?? "");
        Clipboard.SetText(text);
    }

    private static string? GetLogCellValue(Core.Execution.ExecutionLogEntry logEntry, DataGridColumn column)
    {
        return column.Header?.ToString() switch
        {
            "Time" => logEntry.Timestamp.ToString("HH:mm:ss"),
            "Query" => logEntry.Query,
            "Duration" => $"{logEntry.Duration.TotalMilliseconds:F0}ms",
            "Results" => logEntry.ResultCount.ToString(),
            "RU" => logEntry.RequestUnits?.ToString("F2") ?? "",
            "Error" => logEntry.ErrorMessage ?? "",
            _ => null
        };
    }

    #endregion
}
