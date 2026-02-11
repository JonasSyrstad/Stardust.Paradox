using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Stardust.Paradox.GremlinStudio.ViewModels;

namespace Stardust.Paradox.GremlinStudio;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    
    /// <summary>
    /// Cached reference to the GraphCanvasBorder element.
    /// </summary>
    private Border? _graphCanvasBorder;

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

        // Check if this column is a property or instance value
        if (_viewModel.ColumnIsProperty.TryGetValue(columnName, out var isProperty))
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
}