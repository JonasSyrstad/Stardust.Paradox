using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    /// Gets the GraphCanvasBorder element from XAML.
    /// </summary>
    private Border GraphCanvasBorder => (Border)FindName("GraphCanvasBorder");

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        
        // Update maximize button icon when window state changes
        StateChanged += (s, e) => UpdateMaximizeRestoreButton();
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

            // Select the node
            vm.SelectGraphNodeCommand.Execute(node);

            // Start dragging
            var startPoint = e.GetPosition(GraphCanvasBorder);
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

    /// <summary>
    /// Handles node drag start from the node template.
    /// </summary>
    public void StartNodeDrag(ViewModels.GraphNodeViewModel node, Point startPoint)
    {
        var vm = DataContext as ViewModels.MainViewModel;
        if (vm == null) return;

        _isDraggingNode = true;
        vm.StartNodeDrag(node, startPoint.X, startPoint.Y);
        GraphCanvasBorder.CaptureMouse();
        GraphCanvasBorder.Cursor = Cursors.SizeAll;
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