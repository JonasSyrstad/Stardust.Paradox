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
        
        // Update maximize button icon and handle maximize bounds
        StateChanged += OnWindowStateChanged;
        
        // Subscribe to settings panel toggle event
        _viewModel.ToggleSettingsPanelRequested += (s, e) => ToggleSettingsPanel();
        
        // Subscribe to welcome guide step navigation
        _viewModel.WelcomeGuide.StepNavigationRequested += OnGuideStepNavigationRequested;
        
        // Hook into window messages for proper multi-monitor maximize support
        SourceInitialized += OnSourceInitialized;
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

        // Add epoch date/time tooltip to cell values
        if (e.Column is DataGridTextColumn textColumn)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(
                FrameworkElement.ToolTipProperty,
                new Binding(columnName) { Converter = _epochTooltipConverter }));
            textColumn.ElementStyle = style;
        }
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

    #endregion
}