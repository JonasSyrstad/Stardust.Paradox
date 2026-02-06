using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Stardust.Paradox.Data;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Execution;
using Stardust.Paradox.GremlinStudio.Core.Export;
using Stardust.Paradox.GremlinStudio.Core.History;
using Stardust.Paradox.GremlinStudio.Core.Playground;
using Stardust.Paradox.GremlinStudio.Dialogs;
using Stardust.Paradox.GremlinStudio.Services;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// Main view model for the Gremlin Studio application.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IGremlinConnectionStore _connectionStore;
    private readonly IGremlinConnectorFactory _connectorFactory;
    private readonly IGremlinConnectionTester _connectionTester;
    private readonly IGremlinConnectionExporter _connectionExporter;
    private readonly IGremlinQueryExecutor _queryExecutor;
    private readonly IPlaygroundService _playgroundService;
    private readonly IQueryHistoryService _queryHistoryService;
    private readonly IScenarioExportService _scenarioExportService;
    private readonly IThemeService _themeService;
    private readonly ICosmosDbDiscoveryService _discoveryService;
    private readonly ILogger<MainViewModel> _logger;

    private IGremlinLanguageConnector? _activeConnector;
    private CancellationTokenSource? _queryCts;
    private CancellationTokenSource? _exportCts;
    private Task? _connectionLoadingTask;

    public MainViewModel(
        IGremlinConnectionStore connectionStore,
        IGremlinConnectorFactory connectorFactory,
        IGremlinConnectionTester connectionTester,
        IGremlinConnectionExporter connectionExporter,
        IGremlinQueryExecutor queryExecutor,
        IPlaygroundService playgroundService,
        IQueryHistoryService queryHistoryService,
        IScenarioExportService scenarioExportService,
        IThemeService themeService,
        ICosmosDbDiscoveryService discoveryService,
        ILogger<MainViewModel> logger)
    {
        _connectionStore = connectionStore;
        _connectorFactory = connectorFactory;
        _connectionTester = connectionTester;
        _connectionExporter = connectionExporter;
        _queryExecutor = queryExecutor;
        _playgroundService = playgroundService;
        _queryHistoryService = queryHistoryService;
        _scenarioExportService = scenarioExportService;
        _themeService = themeService;
        _discoveryService = discoveryService;
        _logger = logger;

        // Initialize collections
        Connections = new ObservableCollection<GremlinConnectionMetadata>();
        ConnectionKinds = Enum.GetValues<GremlinConnectionKind>().ToList();
        ScenarioNames = _playgroundService.GetAvailableScenarios().Select(s => s.Name).ToList();
        ThemeModes = Enum.GetValues<ThemeMode>().ToList();
        ExportFormats = Enum.GetValues<ScenarioExportFormat>().ToList();
        _selectedThemeMode = _themeService.CurrentMode;

        // Subscribe to theme changes
        _themeService.ThemeChanged += (_, mode) => SelectedThemeMode = mode;


        // Default values
        EditConnectionKind = GremlinConnectionKind.GremlinServer;
        EditPort = 8182;
        QueryText = "g.V().limit(10)";
        StatusText = "Ready";


        // Subscribe to playground state changes
        _playgroundService.StateChanged += OnPlaygroundStateChanged;

        // Load connections and query history on startup
        _ = LoadConnectionsAsync();
        _ = LoadQueryHistoryAsync();
    }

    private async Task LoadQueryHistoryAsync()
    {
        await _queryHistoryService.LoadAsync();
        RefreshQueryHistory();
    }

    private void RefreshQueryHistory()
    {
        // Replace collection atomically to avoid binding issues during collection changes
        var newHistory = new ObservableCollection<QueryHistoryItem>(_queryHistoryService.GetHistory());
        QueryHistory = newHistory;
    }

    #region Properties

    [ObservableProperty]
    private ObservableCollection<QueryHistoryItem> _queryHistory = new();

    [ObservableProperty]
    private QueryHistoryItem? _selectedHistoryItem;

    partial void OnSelectedHistoryItemChanged(QueryHistoryItem? value)
    {
        if (value != null)
        {
            QueryText = value.Query;
            _selectedHistoryItem = null; // Reset selection without triggering change
            OnPropertyChanged(nameof(SelectedHistoryItem));
        }
    }

    [ObservableProperty]
    private ObservableCollection<GremlinConnectionMetadata> _connections;

    [ObservableProperty]
    private GremlinConnectionMetadata? _selectedConnection;


    [ObservableProperty]
    private List<GremlinConnectionKind> _connectionKinds;

    [ObservableProperty]
    private List<string> _scenarioNames;

    [ObservableProperty]
    private string? _selectedScenarioName;

    // Theme
    public List<ThemeMode> ThemeModes { get; }

    [ObservableProperty]
    private ThemeMode _selectedThemeMode;

    partial void OnSelectedThemeModeChanged(ThemeMode value)
    {
        _themeService.SetTheme(value);
    }

    // Connection editing fields
    [ObservableProperty]
    private string _editConnectionName = string.Empty;

    [ObservableProperty]
    private GremlinConnectionKind _editConnectionKind;

    [ObservableProperty]
    private string _editHost = string.Empty;

    [ObservableProperty]
    private int _editPort;

    [ObservableProperty]
    private string _editUsername = string.Empty;

    [ObservableProperty]
    private string _editSecret = string.Empty;

    [ObservableProperty]
    private string _editDatabase = string.Empty;

    [ObservableProperty]
    private string _editGraph = string.Empty;

    // Query
    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private string _resultJson = string.Empty;

    // Result Views
    [ObservableProperty]
    private int _selectedResultViewIndex;

    [ObservableProperty]
    private DataView? _resultTable;

    /// <summary>
    /// Tracks which columns are property columns (true) vs instance columns (false).
    /// </summary>
    public Dictionary<string, bool> ColumnIsProperty { get; } = new();

    [ObservableProperty]
    private ObservableCollection<TreeNodeViewModel> _resultTreeItems = new();

    // Graph View
    // Collection for the left panel list (all query results)
    [ObservableProperty]
    private ObservableCollection<GraphNodeViewModel> _resultNodes = new();

    // Collection for the graph canvas (selected node + direct connections)
    [ObservableProperty]
    private ObservableCollection<GraphNodeViewModel> _graphNodes = new();

    [ObservableProperty]
    private ObservableCollection<GraphEdgeViewModel> _graphEdges = new();

    [ObservableProperty]
    private GraphNodeViewModel? _selectedResultNode;

    [ObservableProperty]
    private GraphNodeViewModel? _selectedGraphNode;

    [ObservableProperty]
    private GraphEdgeViewModel? _selectedGraphEdge;

    [ObservableProperty]
    private List<string> _graphLayouts = new() { "Force-Directed", "Circular", "Grid" };

    [ObservableProperty]
    private string _selectedGraphLayout = "Force-Directed";

    [ObservableProperty]
    private bool _hasGraphData;

    // Re-apply layout when layout mode changes
    partial void OnSelectedGraphLayoutChanged(string value)
    {
        if (GraphNodes.Count > 0)
        {
            ReapplyCurrentLayout();
        }
    }

    private void ReapplyCurrentLayout()
    {
        var nodes = GraphNodes.ToList();
        if (nodes.Count == 0) return;

        // Find the center node (first one is typically the selected one)
        var centerNode = nodes.FirstOrDefault(n => n.IsSelected) ?? nodes.First();
        var otherNodes = nodes.Where(n => n != centerNode).ToList();

        // Position center node
        centerNode.X = 250;
        centerNode.Y = 150;

        // Apply layout to other nodes
        if (otherNodes.Count > 0)
        {
            switch (SelectedGraphLayout)
            {
                case "Circular":
                    double radius = 120;
                    for (int i = 0; i < otherNodes.Count; i++)
                    {
                        double angle = 2 * Math.PI * i / otherNodes.Count - Math.PI / 2;
                        otherNodes[i].X = 250 + radius * Math.Cos(angle);
                        otherNodes[i].Y = 150 + radius * Math.Sin(angle);
                    }
                    break;

                case "Grid":
                    int cols = (int)Math.Ceiling(Math.Sqrt(otherNodes.Count + 1));
                    double spacing = 100;
                    for (int i = 0; i < otherNodes.Count; i++)
                    {
                        int gridIndex = i + 1; // Skip first position for center
                        int row = gridIndex / cols;
                        int col = gridIndex % cols;
                        otherNodes[i].X = 100 + col * spacing;
                        otherNodes[i].Y = 80 + row * spacing;
                    }
                    break;

                case "Force-Directed":
                default:
                    // Radial layout with some randomness
                    var random = new Random(42);
                    double baseRadius = 120;
                    for (int i = 0; i < otherNodes.Count; i++)
                    {
                        double angle = 2 * Math.PI * i / otherNodes.Count - Math.PI / 2;
                        double r = baseRadius + random.NextDouble() * 30;
                        otherNodes[i].X = 250 + r * Math.Cos(angle);
                        otherNodes[i].Y = 150 + r * Math.Sin(angle);
                    }
                    break;
            }
        }

        // Update edge coordinates
        foreach (var edge in GraphEdges)
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == edge.FromId);
            var toNode = nodes.FirstOrDefault(n => n.Id == edge.ToId);
            if (fromNode != null && toNode != null)
            {
                UpdateEdgeCoordinates(edge, fromNode, toNode, edge.CurveOffset);
            }
        }
    }


    [ObservableProperty]
    private double _graphZoom = 1.0;


    [ObservableProperty]
    private double _graphPanX;

    [ObservableProperty]
    private double _graphPanY;

    [ObservableProperty]
    private bool _isLoadingConnections;

    // Node being dragged
    private GraphNodeViewModel? _draggingNode;
    private double _dragStartX;
    private double _dragStartY;

    /// <summary>
    /// Starts dragging a node.
    /// </summary>
    public void StartNodeDrag(GraphNodeViewModel node, double startX, double startY)
    {
        _draggingNode = node;
        _dragStartX = startX;
        _dragStartY = startY;
    }

    /// <summary>
    /// Updates the position of the dragging node.
    /// </summary>
    public void UpdateNodeDrag(double currentX, double currentY)
    {
        if (_draggingNode == null) return;

        double deltaX = (currentX - _dragStartX) / GraphZoom;
        double deltaY = (currentY - _dragStartY) / GraphZoom;

        _draggingNode.X += deltaX;
        _draggingNode.Y += deltaY;

        _dragStartX = currentX;
        _dragStartY = currentY;

        // Recalculate all edges connected to this node
        RecalculateEdgesForNode(_draggingNode);
    }

    /// <summary>
    /// Ends node dragging.
    /// </summary>
    public void EndNodeDrag()
    {
        _draggingNode = null;
    }

    /// <summary>
    /// Recalculates edges connected to a specific node.
    /// </summary>
    private void RecalculateEdgesForNode(GraphNodeViewModel node)
    {
        var nodeDict = GraphNodes.ToDictionary(n => n.Id);
        foreach (var edge in GraphEdges)
        {
            if (edge.FromId == node.Id || edge.ToId == node.Id)
            {
                if (nodeDict.TryGetValue(edge.FromId, out var fromNode) &&
                    nodeDict.TryGetValue(edge.ToId, out var toNode))
                {
                    UpdateEdgeCoordinates(edge, fromNode, toNode, edge.CurveOffset);
                }
            }
        }
    }

    // Properties panel bindings
    [ObservableProperty]
    private string _selectedElementTitle = string.Empty;

    [ObservableProperty]
    private bool _hasSelectedElement;

    [ObservableProperty]
    private ObservableCollection<KeyValuePair<string, string>> _selectedElementProperties = new();

    [ObservableProperty]
    private ObservableCollection<GraphEdgeViewModel> _incomingEdges = new();

    [ObservableProperty]
    private ObservableCollection<GraphEdgeViewModel> _outgoingEdges = new();

    [ObservableProperty]
    private bool _hasIncomingEdges;

    [ObservableProperty]
    private bool _hasOutgoingEdges;

    // Triggered when user selects from the left panel list - rebuilds the graph
    partial void OnSelectedResultNodeChanged(GraphNodeViewModel? oldValue, GraphNodeViewModel? newValue)
    {
        if (newValue != null)
        {
            _ = LoadGraphForSelectedNodeAsync(newValue.Id);
        }
    }

    // Triggered when user clicks a node on the canvas - only updates properties panel
    partial void OnSelectedGraphNodeChanged(GraphNodeViewModel? oldValue, GraphNodeViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        
        // Clear edge selection when selecting a node
        if (SelectedGraphEdge != null)
        {
            SelectedGraphEdge.IsSelected = false;
            SelectedGraphEdge.StrokeThickness = 1.5;
            SelectedGraphEdge = null;
        }
        
        if (newValue != null)
        {
            newValue.IsSelected = true;
            UpdatePropertiesPanel(newValue);
        }
        else
        {
            ClearPropertiesPanel();
        }
    }

    partial void OnSelectedGraphEdgeChanged(GraphEdgeViewModel? oldValue, GraphEdgeViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.IsSelected = false;
            oldValue.StrokeThickness = 1.5;
        }
        
        if (newValue != null)
        {
            newValue.IsSelected = true;
            newValue.StrokeThickness = 3;
            UpdatePropertiesPanel(newValue);
        }
    }

    private void UpdatePropertiesPanel(GraphNodeViewModel node)
    {
        SelectedElementTitle = node.Label.Length > 20 ? node.Label[..20] + "..." : node.Label;
        HasSelectedElement = true;
        
        SelectedElementProperties.Clear();
        SelectedElementProperties.Add(new KeyValuePair<string, string>("id", node.Id));
        SelectedElementProperties.Add(new KeyValuePair<string, string>("label", node.Label));
        
        foreach (var prop in node.Properties)
        {
            SelectedElementProperties.Add(new KeyValuePair<string, string>(prop.Key, prop.Value?.ToString() ?? ""));
        }
        
        // Update incoming/outgoing edges
        IncomingEdges.Clear();
        OutgoingEdges.Clear();
        
        foreach (var edge in GraphEdges)
        {
            if (edge.ToId == node.Id)
                IncomingEdges.Add(edge);
            else if (edge.FromId == node.Id)
                OutgoingEdges.Add(edge);
        }
        
        HasIncomingEdges = IncomingEdges.Count > 0;
        HasOutgoingEdges = OutgoingEdges.Count > 0;
    }

    private void UpdatePropertiesPanel(GraphEdgeViewModel edge)
    {
        SelectedElementTitle = $"Edge: {edge.Label}";
        HasSelectedElement = true;
        
        SelectedElementProperties.Clear();
        SelectedElementProperties.Add(new KeyValuePair<string, string>("id", edge.Id));
        SelectedElementProperties.Add(new KeyValuePair<string, string>("label", edge.Label));
        SelectedElementProperties.Add(new KeyValuePair<string, string>("from", edge.FromId));
        SelectedElementProperties.Add(new KeyValuePair<string, string>("to", edge.ToId));
        
        foreach (var prop in edge.Properties)
        {
            SelectedElementProperties.Add(new KeyValuePair<string, string>(prop.Key, prop.Value?.ToString() ?? ""));
        }
        
        IncomingEdges.Clear();
        OutgoingEdges.Clear();
        HasIncomingEdges = false;
        HasOutgoingEdges = false;
    }

    private void ClearPropertiesPanel()
    {
        SelectedElementTitle = string.Empty;
        HasSelectedElement = false;
        SelectedElementProperties.Clear();
        IncomingEdges.Clear();
        OutgoingEdges.Clear();
        HasIncomingEdges = false;
        HasOutgoingEdges = false;
    }

    // Status
    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _lastDurationText = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private bool _isPlaygroundRunning;

    [ObservableProperty]
    private string? _loadedScenarioName;

    // Scenario Export Properties
    [ObservableProperty]
    private string _exportScenarioName = "ExportedScenario";

    [ObservableProperty]
    private string _exportScenarioDescription = "";

    [ObservableProperty]
    private ScenarioExportFormat _selectedExportFormat = ScenarioExportFormat.Json;


    public List<ScenarioExportFormat> ExportFormats { get; }

    [ObservableProperty]
    private string _exportPreview = "";

    [ObservableProperty]
    private bool _hasExportData;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string _exportStatusText = "";

    [ObservableProperty]
    private string _exportStepName = "";

    #endregion

    #region Commands



    [RelayCommand]
    private async Task NewConnectionAsync()
    {
        try
        {
            var dialogViewModel = new NewConnectionDialogViewModel(_discoveryService);
            var dialog = new Dialogs.NewConnectionDialog(dialogViewModel);
            
            // Find the active window to set as owner (avoid setting owner to itself)
            var activeWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive);
            
            if (activeWindow != null && activeWindow != dialog)
            {
                dialog.Owner = activeWindow;
            }

            if (dialog.ShowDialog() == true)
            {
                var settings = dialogViewModel.GetConnectionSettings();
                
                // Save the connection
                await _connectionStore.SaveConnectionAsync(settings).ConfigureAwait(true);
                await LoadConnectionsAsync().ConfigureAwait(true);

                // Select the new connection
                var newConnection = Connections.FirstOrDefault(c => c.Id == settings.Id);
                if (newConnection != null)
                {
                    SelectedConnection = newConnection;
                }

                StatusText = $"Created connection: {settings.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connection");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync()
    {
        if (SelectedConnection is null)
        {
            StatusText = "Select a connection to delete";
            return;
        }

        var connectionName = SelectedConnection.Name;
        var connectionId = SelectedConnection.Id;

        try
        {
            // Confirm deletion
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the connection '{connectionName}'?",
                "Delete Connection",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            await _connectionStore.DeleteConnectionAsync(connectionId).ConfigureAwait(true);
            
            // Clear selection and reload
            SelectedConnection = null;
            await LoadConnectionsAsync().ConfigureAwait(true);
            
            // Clear edit fields
            EditConnectionName = string.Empty;
            EditHost = string.Empty;
            EditPort = 8182;
            EditUsername = string.Empty;
            EditSecret = string.Empty;
            EditDatabase = string.Empty;
            EditGraph = string.Empty;
            
            StatusText = $"Deleted connection: {connectionName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete connection");
            StatusText = $"Error deleting connection: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(EditConnectionName))
        {
            StatusText = "Connection name is required";
            return;
        }

        try
        {
            var metadata = SelectedConnection?.Clone() ?? new GremlinConnectionMetadata();
            metadata.Name = EditConnectionName;
            metadata.Kind = EditConnectionKind;
            metadata.Host = EditHost;
            metadata.Port = EditPort;
            metadata.Username = EditUsername;
            metadata.DatabaseName = string.IsNullOrWhiteSpace(EditDatabase) ? null : EditDatabase;
            metadata.GraphName = string.IsNullOrWhiteSpace(EditGraph) ? null : EditGraph;

            var settings = new GremlinConnectionSettings(metadata, EditSecret);
            await _connectionStore.SaveConnectionAsync(settings).ConfigureAwait(true);

            await LoadConnectionsAsync().ConfigureAwait(true);
            StatusText = $"Saved connection: {metadata.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save connection");
            StatusText = $"Error saving connection: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(EditHost))
        {
            StatusText = "Host is required to test connection";
            return;
        }

        try
        {
            StatusText = "Testing connection...";

            var metadata = new GremlinConnectionMetadata
            {
                Name = EditConnectionName,
                Kind = EditConnectionKind,
                Host = EditHost,
                Port = EditPort,
                Username = EditUsername,
                DatabaseName = string.IsNullOrWhiteSpace(EditDatabase) ? null : EditDatabase,
                GraphName = string.IsNullOrWhiteSpace(EditGraph) ? null : EditGraph
            };

            var settings = new GremlinConnectionSettings(metadata, EditSecret);
            var result = await _connectionTester.TestConnectionAsync(settings).ConfigureAwait(true);

            StatusText = result.IsSuccess
                ? $"Connection successful ({result.Latency.TotalMilliseconds:F0}ms)"
                : $"Connection failed: {result.Message}";

            LastDurationText = $"Test: {result.Latency.TotalMilliseconds:F0}ms";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connection");
            StatusText = $"Test error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportConnectionsAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = "gremlin-connections.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await _connectionExporter.ExportAsync(dialog.FileName).ConfigureAwait(true);
            StatusText = $"Exported connections to: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export connections");
            StatusText = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StartPlayground()
    {
        try
        {
            _playgroundService.Start();
            _activeConnector = _playgroundService.Connector;
            IsPlaygroundRunning = true;
            StatusText = "Playground started";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start playground");
            StatusText = $"Error starting playground: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopPlayground()
    {
        try
        {
            _playgroundService.Stop();
            _activeConnector = null;
            IsPlaygroundRunning = false;
            LoadedScenarioName = null;
            StatusText = "Playground stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop playground");
            StatusText = $"Error stopping playground: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadScenarioAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedScenarioName))
        {
            StatusText = "Select a scenario first";
            return;
        }

        try
        {
            _playgroundService.LoadScenario(SelectedScenarioName);
            var state = await _playgroundService.RefreshStateAsync().ConfigureAwait(true);
            StatusText = $"Loaded scenario: {SelectedScenarioName} ({state.VertexCount} vertices, {state.EdgeCount} edges)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scenario");
            StatusText = $"Error loading scenario: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetScenarioAsync()
    {
        try
        {
            _playgroundService.ResetScenario();
            var state = await _playgroundService.RefreshStateAsync().ConfigureAwait(true);
            StatusText = $"Reset scenario: {state.LoadedScenarioName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset scenario");
            StatusText = $"Error resetting scenario: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunQuery))]
    private async Task RunQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(QueryText))
        {
            StatusText = "Enter a query first";
            return;
        }

        // Wait for connection loading to complete if in progress
        if (_connectionLoadingTask is not null)
        {
            StatusText = "Waiting for connection...";
            try
            {
                await _connectionLoadingTask.ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Connection loading failed");
            }
            _connectionLoadingTask = null;
        }

        // Determine which connector to use
        var connector = GetActiveConnector();
        if (connector is null)
        {
            StatusText = "No active connection. Start the playground or select a connection.";
            return;
        }

        try
        {
            IsExecuting = true;
            _queryCts = new CancellationTokenSource();
            StatusText = "Executing query...";

            var result = await _queryExecutor.ExecuteAsync(
                connector,
                QueryText,
                cancellationToken: _queryCts.Token).ConfigureAwait(true);

            ResultJson = result.ResultJson ?? result.ErrorMessage ?? "No results";
            LastDurationText = $"Query: {result.Duration.TotalMilliseconds:F0}ms";

            StatusText = result.IsSuccess
                ? $"Query completed: {result.ResultCount} results in {result.Duration.TotalMilliseconds:F0}ms"
                : $"Query failed: {result.ErrorMessage}";

            // Save successful queries to history
            if (result.IsSuccess)
            {
                _queryHistoryService.AddQuery(QueryText);
                _ = _queryHistoryService.SaveAsync();
                RefreshQueryHistory();
            }

            // Populate the different result views
            PopulateResultViews(result.ResultJson);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Query cancelled";
            ResultJson = "Query was cancelled";
            ClearResultViews();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query");
            StatusText = $"Query error: {ex.Message}";
            ResultJson = ex.ToString();
            ClearResultViews();
        }
        finally
        {
            IsExecuting = false;
            _queryCts?.Dispose();
            _queryCts = null;
        }
    }

    private void PopulateResultViews(string? json)
    {
        ClearResultViews();

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var results = Newtonsoft.Json.JsonConvert.DeserializeObject<List<object>>(json);
            if (results is null || results.Count == 0)
            {
                return;
            }


            // Populate Tree View
            var rootNode = new TreeNodeViewModel
            {
                Name = "Results",
                Icon = "{#}",
                FontWeight = "Bold"
            };
            for (int i = 0; i < results.Count; i++)
            {
                rootNode.Children.Add(TreeNodeViewModel.FromObject($"[{i}]", results[i]));
            }
            ResultTreeItems.Add(rootNode);

            // Populate Table View (flatten Gremlin vertex/edge objects)
            ColumnIsProperty.Clear();
            ResultTable = BuildResultDataTable(results, ColumnIsProperty);

            // Populate Graph View (for vertex/edge data)
            PopulateGraphView(results);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse results for visualization");
        }
    }

    private static DataView BuildResultDataTable(List<object> results, Dictionary<string, bool> columnIsProperty)
    {
        var table = new DataTable("Results");

        // First pass: collect all unique column names
        // Track: key = display name, value = (originalName, isProperty)
        var columnInfo = new Dictionary<string, (string OriginalName, bool IsProperty)>();

        foreach (var item in results)
        {
            if (item is not Newtonsoft.Json.Linq.JObject jObj) continue;

            foreach (var prop in jObj.Properties())
            {
                if (prop.Name == "properties" && prop.Value is Newtonsoft.Json.Linq.JObject propsObj)
                {
                    // Extract nested properties with a marker
                    foreach (var nestedProp in propsObj.Properties())
                    {
                        var displayName = nestedProp.Name;
                        if (!columnInfo.ContainsKey(displayName))
                        {
                            columnInfo[displayName] = (nestedProp.Name, true);
                        }
                    }
                }
                else
                {
                    // Direct instance properties
                    if (!columnInfo.ContainsKey(prop.Name))
                    {
                        columnInfo[prop.Name] = (prop.Name, false);
                    }
                }
            }
        }

        // Create columns ordered: instance values first (id, label, type), then properties
        var orderedColumns = columnInfo
            .OrderBy(c => c.Value.IsProperty) // Instance values first
            .ThenBy(c => c.Key == "id" ? 0 : c.Key == "label" ? 1 : c.Key == "type" ? 2 : 3)
            .ThenBy(c => c.Key)
            .ToList();

        foreach (var col in orderedColumns)
        {
            // Use clean column names and track property vs instance
            table.Columns.Add(col.Key, typeof(string));
            columnIsProperty[col.Key] = col.Value.IsProperty;
        }

        // Second pass: populate rows
        foreach (var item in results)
        {
            if (item is not Newtonsoft.Json.Linq.JObject jObj) continue;

            var row = table.NewRow();
            var values = new Dictionary<string, string?>();

            // Extract instance values
            foreach (var prop in jObj.Properties())
            {
                if (prop.Name == "properties" && prop.Value is Newtonsoft.Json.Linq.JObject propsObj)
                {
                    // Extract property values
                    foreach (var nestedProp in propsObj.Properties())
                    {
                        var value = ExtractPropertyValue(nestedProp.Value);
                        values[nestedProp.Name] = value;
                    }
                }
                else
                {
                    values[prop.Name] = ExtractSimpleValue(prop.Value);
                }
            }

            // Populate row
            foreach (var col in orderedColumns)
            {
                if (values.TryGetValue(col.Key, out var value))
                {
                    row[col.Key] = value ?? string.Empty;
                }
            }

            table.Rows.Add(row);
        }

        return table.DefaultView;
    }

    private static string? ExtractPropertyValue(Newtonsoft.Json.Linq.JToken? token)
    {
        if (token is Newtonsoft.Json.Linq.JArray arr && arr.Count > 0)
        {
            var first = arr[0];
            if (first is Newtonsoft.Json.Linq.JObject obj && obj["value"] != null)
            {
                return obj["value"]?.ToString();
            }
            return first?.ToString();
        }
        return token?.ToString();
    }

    private static string? ExtractSimpleValue(Newtonsoft.Json.Linq.JToken? token)
    {
        if (token is Newtonsoft.Json.Linq.JValue val)
        {
            return val.Value?.ToString();
        }
        if (token is Newtonsoft.Json.Linq.JArray arr)
        {
            if (arr.Count <= 3)
            {
                return string.Join(", ", arr.Select(x => x.ToString()));
            }
            return $"[{arr.Count} items]";
        }
        if (token is Newtonsoft.Json.Linq.JObject obj)
        {
            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }
        return token?.ToString();
    }

    private void PopulateGraphView(List<object> results)
    {
        var vertices = new Dictionary<string, GraphNodeViewModel>();
        var edges = new List<(string fromId, string toId, string label, string id)>();

        // First pass: collect vertices and edges
        foreach (var item in results)
        {
            if (item is not Newtonsoft.Json.Linq.JObject jObj)
            {
                continue;
            }

            var id = jObj["id"]?.ToString();
            var label = jObj["label"]?.ToString();
            var type = jObj["type"]?.ToString();


            if (id is null || label is null)
            {
                continue;
            }

            if (type == "edge")
            {
                // It's an edge
                var inV = jObj["inV"]?.ToString();
                var outV = jObj["outV"]?.ToString();
                if (inV != null && outV != null)
                {
                    edges.Add((outV, inV, label, id));
                }
            }
            else
            {
                // It's a vertex
                if (!vertices.ContainsKey(id))
                {
                    var node = new GraphNodeViewModel
                    {
                        Id = id,
                        Label = label,
                        Type = "vertex",
                        Fill = GetNodeFillColor(label),
                        Stroke = System.Windows.Media.Brushes.White,
                        DisplayText = label
                    };

                    // Extract properties
                    if (jObj["properties"] is Newtonsoft.Json.Linq.JObject props)
                    {
                        foreach (var prop in props.Properties())
                        {
                            var value = ExtractPropertyValue(prop.Value);
                            if (value != null)
                            {
                                node.Properties[prop.Name] = value;
                            }
                        }
                    }

                    vertices[id] = node;
                }
            }
        }

        // Add vertices to ResultNodes (left panel list) - this is the full query result
        foreach (var node in vertices.Values)
        {
            ResultNodes.Add(node);
        }

        // Don't populate GraphNodes or GraphEdges here - that happens when user selects from list
        // Graph canvas starts empty until selection

        // Mark that we have data in the results list
        HasGraphData = ResultNodes.Count > 0;

        // Auto-select first element when loading to trigger graph display
        if (ResultNodes.Count > 0)
        {
            SelectedResultNode = ResultNodes[0];
        }
    }

    /// <summary>
    /// Fetches edges between the given vertices.
    /// </summary>
    [Obsolete("No longer used - edges are fetched per-selection in LoadGraphForSelectedNodeAsync")]
    private async Task FetchEdgesBetweenVerticesAsync(List<string> vertexIds)
    {
        var connector = GetActiveConnector();
        if (connector == null || vertexIds.Count == 0)
        {
            return;
        }

        try
        {
            // Query edges where both endpoints are in our vertex set
            var idsParam = string.Join("','", vertexIds);
            var edgeQuery = $"g.V('{idsParam}').outE().where(inV().hasId('{idsParam}'))";
            
            var result = await _queryExecutor.ExecuteAsync(connector, edgeQuery);

            if (result.IsSuccess && !string.IsNullOrEmpty(result.ResultJson))
            {
                var existingEdgeIds = GraphEdges.Select(e => e.Id).ToHashSet();
                var newEdges = new List<GraphEdgeViewModel>();
                ProcessEdgeResults(result.ResultJson, newEdges, existingEdgeIds);

                foreach (var edge in newEdges)
                {
                    GraphEdges.Add(edge);
                }
                UpdateAllEdgeCoordinates();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch edges between vertices");
        }
    }

    private void ApplyGraphLayout(List<GraphNodeViewModel> nodes, string layout)
    {
        if (nodes.Count == 0) return;

        double centerX = 250, centerY = 150;
        double radius = Math.Min(centerX, centerY) - 40;

        switch (layout)
        {
            case "Circular":
                for (int i = 0; i < nodes.Count; i++)
                {
                    double angle = 2 * Math.PI * i / nodes.Count - Math.PI / 2;
                    nodes[i].X = centerX + radius * Math.Cos(angle);
                    nodes[i].Y = centerY + radius * Math.Sin(angle);
                }
                break;

            case "Grid":
                int cols = (int)Math.Ceiling(Math.Sqrt(nodes.Count));
                double spacing = 80;
                for (int i = 0; i < nodes.Count; i++)
                {
                    int row = i / cols;
                    int col = i % cols;
                    nodes[i].X = 50 + col * spacing;
                    nodes[i].Y = 50 + row * spacing;
                }
                break;

            case "Force-Directed":
            default:
                // Simple force-directed layout
                var random = new Random(42); // Deterministic for consistency
                foreach (var node in nodes)
                {
                    node.X = centerX + (random.NextDouble() - 0.5) * radius * 2;
                    node.Y = centerY + (random.NextDouble() - 0.5) * radius * 2;
                }

                // Apply forces iteratively
                for (int iteration = 0; iteration < 50; iteration++)
                {
                    // Repulsion between all nodes
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        for (int j = i + 1; j < nodes.Count; j++)
                        {
                            double dx = nodes[j].X - nodes[i].X;
                            double dy = nodes[j].Y - nodes[i].Y;
                            double dist = Math.Sqrt(dx * dx + dy * dy);
                            if (dist < 1) dist = 1;

                            double force = 2000 / (dist * dist);
                            double fx = force * dx / dist;
                            double fy = force * dy / dist;

                            nodes[i].X -= fx;
                            nodes[i].Y -= fy;
                            nodes[j].X += fx;
                            nodes[j].Y += fy;
                        }
                    }

                    // Center gravity
                    foreach (var node in nodes)
                    {
                        node.X += (centerX - node.X) * 0.01;
                        node.Y += (centerY - node.Y) * 0.01;

                        // Keep in bounds
                        node.X = Math.Max(30, Math.Min(470, node.X));
                        node.Y = Math.Max(30, Math.Min(270, node.Y));
                    }
                }
                break;
        }
    }


    private static void UpdateEdgeCoordinates(GraphEdgeViewModel edge, GraphNodeViewModel from, GraphNodeViewModel to, double perimeterOffset = 0)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) dist = 1;

        // Base angle from source to target
        double baseAngle = Math.Atan2(dy, dx);
        
        // Convert perimeterOffset to angle offset: +/- 20 degrees max fan spread
        // perimeterOffset typically ranges from -60 to +60, so we scale to radians
        double maxAngleSpread = 25.0 * Math.PI / 180.0; // 25 degrees max in radians
        double angleOffset = (perimeterOffset / 60.0) * maxAngleSpread;

        // Start point on source node perimeter (offset from base angle)
        double startAngle = baseAngle + angleOffset;
        edge.X1 = from.X + from.Radius * Math.Cos(startAngle);
        edge.Y1 = from.Y + from.Radius * Math.Sin(startAngle);

        // End point on target node perimeter (opposite direction with same offset)
        double endAngle = baseAngle + Math.PI + angleOffset;
        edge.X2 = to.X + to.Radius * Math.Cos(endAngle);
        edge.Y2 = to.Y + to.Radius * Math.Sin(endAngle);
    }

    /// <summary>
    /// Calculates curve offsets for parallel edges between the same node pairs.
    /// </summary>
    private static void CalculateParallelEdgeOffsets(List<GraphEdgeViewModel> edges)
    {
        // Group edges by their node pair (regardless of direction)
        var edgeGroups = edges
            .GroupBy(e => e.FromId.CompareTo(e.ToId) < 0 
                ? (e.FromId, e.ToId) 
                : (e.ToId, e.FromId))
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in edgeGroups)
        {
            var groupEdges = group.ToList();
            int count = groupEdges.Count;
            double baseOffset = 60; // Base curve offset - larger for more spacing at midpoint

            for (int i = 0; i < count; i++)
            {
                var edge = groupEdges[i];
                // Distribute edges: -offset, +offset for pairs; spread evenly for more
                double offset = baseOffset * ((i - (count - 1) / 2.0));
                
                // Reverse offset direction for edges going the "other way"
                bool isReversed = edge.FromId.CompareTo(edge.ToId) >= 0;
                edge.CurveOffset = isReversed ? -offset : offset;
            }
        }


        // Single edges between nodes get a small curve for aesthetics
        var singleEdges = edges
            .GroupBy(e => e.FromId.CompareTo(e.ToId) < 0 
                ? (e.FromId, e.ToId) 
                : (e.ToId, e.FromId))
            .Where(g => g.Count() == 1)
            .SelectMany(g => g);

        foreach (var edge in singleEdges)
        {
            edge.CurveOffset = 0; // Straight line for single edges
        }
    }

    /// <summary>
    /// Recalculates edge coordinates with perimeter offsets for parallel edges.
    /// </summary>
    private void RecalculateEdgeCoordinatesWithOffsets(Dictionary<string, GraphNodeViewModel> nodes)
    {
        foreach (var edge in GraphEdges)
        {
            if (nodes.TryGetValue(edge.FromId, out var fromNode) &&
                nodes.TryGetValue(edge.ToId, out var toNode))
            {
                UpdateEdgeCoordinates(edge, fromNode, toNode, edge.CurveOffset);
            }
        }
    }

    private static System.Windows.Media.Brush GetNodeFillColor(string label)
    {
        // Vibrant Azure Portal-like colors
        var hash = Math.Abs(label.GetHashCode());
        var colors = new[]
        {
            System.Windows.Media.Color.FromRgb(0, 140, 240),   // Bright Azure blue
            System.Windows.Media.Color.FromRgb(32, 160, 32),   // Bright Green
            System.Windows.Media.Color.FromRgb(230, 100, 20),  // Bright Orange
            System.Windows.Media.Color.FromRgb(160, 40, 180),  // Bright Purple
            System.Windows.Media.Color.FromRgb(0, 180, 200),   // Bright Teal
            System.Windows.Media.Color.FromRgb(220, 60, 80),   // Bright Red
            System.Windows.Media.Color.FromRgb(100, 100, 180), // Blue-Gray
        };
        return new System.Windows.Media.SolidColorBrush(colors[hash % colors.Length]);
    }

    private void ClearResultViews()
    {
        ResultTable = null;
        ResultTreeItems.Clear();
        ResultNodes.Clear();
        GraphNodes.Clear();
        GraphEdges.Clear();
        SelectedResultNode = null;
        SelectedGraphNode = null;
        SelectedGraphEdge = null;
        HasGraphData = false;
        ClearPropertiesPanel();
    }


    private bool CanRunQuery() => !IsExecuting;

    [RelayCommand]
    private void Cancel()
    {
        _queryCts?.Cancel();
        StatusText = "Cancelling...";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        GraphZoom = Math.Min(3.0, GraphZoom * 1.2);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        GraphZoom = Math.Max(0.3, GraphZoom / 1.2);
    }


    [RelayCommand]
    private void ResetGraph()
    {
        GraphZoom = 1.0;
        GraphPanX = 100;
        GraphPanY = 50;
    }


    [RelayCommand]
    private void SelectGraphNode(GraphNodeViewModel? node)
    {
        SelectedGraphNode = node;
    }

    [RelayCommand]
    private void SelectGraphEdge(GraphEdgeViewModel? edge)
    {
        // Clear node selection when selecting an edge
        if (SelectedGraphNode != null)
        {
            SelectedGraphNode.IsSelected = false;
            SelectedGraphNode = null;
        }
        
        // Clear previous edge selection
        if (SelectedGraphEdge != null)
        {
            SelectedGraphEdge.IsSelected = false;
        }
        
        SelectedGraphEdge = edge;
        
        if (edge != null)
        {
            edge.IsSelected = true;
            UpdatePropertiesPanelForEdge(edge);
        }
        else
        {
            ClearPropertiesPanel();
        }
    }

    private void UpdatePropertiesPanelForEdge(GraphEdgeViewModel edge)
    {
        SelectedElementTitle = $"Edge: {edge.Label}";
        HasSelectedElement = true;
        
        SelectedElementProperties.Clear();
        SelectedElementProperties.Add(new KeyValuePair<string, string>("id", edge.Id));
        SelectedElementProperties.Add(new KeyValuePair<string, string>("label", edge.Label));
        SelectedElementProperties.Add(new KeyValuePair<string, string>("from", edge.FromId));
        SelectedElementProperties.Add(new KeyValuePair<string, string>("to", edge.ToId));
        
        foreach (var prop in edge.Properties)
        {
            SelectedElementProperties.Add(new KeyValuePair<string, string>(prop.Key, prop.Value?.ToString() ?? ""));
        }
        
        // Clear edge lists for edges (they don't have connections)
        IncomingEdges.Clear();
        OutgoingEdges.Clear();
        HasIncomingEdges = false;
        HasOutgoingEdges = false;
    }

    #endregion

    #region Query History Commands

    [RelayCommand]
    private void TogglePinQuery(QueryHistoryItem? item)
    {
        if (item == null) return;
        
        _queryHistoryService.SetPinned(item.Id, !item.IsPinned);
        _ = _queryHistoryService.SaveAsync();
        RefreshQueryHistory();
    }

    [RelayCommand]
    private void RemoveFromHistory(QueryHistoryItem? item)
    {
        if (item == null) return;
        
        _queryHistoryService.RemoveQuery(item.Id);
        _ = _queryHistoryService.SaveAsync();
        RefreshQueryHistory();
    }

    [RelayCommand]
    private void ClearQueryHistory()
    {
        _queryHistoryService.ClearUnpinned();
        _ = _queryHistoryService.SaveAsync();
        RefreshQueryHistory();
    }

    #endregion

    #region Scenario Export Commands

    [RelayCommand]
    private async Task GenerateExportPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(ResultJson) || ResultJson == "No results")
        {
            _exportPreview = "// No query results to export. Run a query first.";
            OnPropertyChanged(nameof(ExportPreview));
            _hasExportData = false;
            OnPropertyChanged(nameof(HasExportData));
            return;
        }

        var connector = GetActiveConnector();
        if (connector == null)
        {
            _exportPreview = "// No active connection. Cannot fetch edges.\n// Connect to a database first.";
            OnPropertyChanged(nameof(ExportPreview));
            _hasExportData = false;
            OnPropertyChanged(nameof(HasExportData));
            return;
        }

        try
        {
            _exportCts = new CancellationTokenSource();
            _isExporting = true;
            OnPropertyChanged(nameof(IsExporting));

            // Pre-delay with "Initializing" step
            _exportStepName = "Initializing";
            _exportStatusText = "Preparing export...";
            OnPropertyChanged(nameof(ExportStepName));
            OnPropertyChanged(nameof(ExportStatusText));
            await Task.Delay(500, _exportCts.Token).ConfigureAwait(true);

            _exportCts.Token.ThrowIfCancellationRequested();

            _exportStepName = "Parsing Data";
            _exportStatusText = "Parsing vertices from query results...";
            OnPropertyChanged(nameof(ExportStepName));
            OnPropertyChanged(nameof(ExportStatusText));

            var scenarioData = _scenarioExportService.ParseQueryResults(
                ResultJson,
                _exportScenarioName,
                _exportScenarioDescription,
                SelectedConnection?.Name,
                QueryText);

            _exportCts.Token.ThrowIfCancellationRequested();

            if (scenarioData.Vertices.Count > 0)
            {
                // Fetch edges between vertices from the database (like ScenarioConnector does)
                _exportStepName = "Fetching Edges";
                _exportStatusText = $"Fetching edges between {scenarioData.Vertices.Count} vertices...";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));
                
                // Create progress reporter for batch updates
                var progress = new Progress<string>(message =>
                {
                    _exportStatusText = message;
                    OnPropertyChanged(nameof(ExportStatusText));
                });
                
                scenarioData = await _scenarioExportService.FetchEdgesAsync(scenarioData, connector, progress, _exportCts.Token);
            }

            _exportCts.Token.ThrowIfCancellationRequested();

            _exportStepName = "Generating Output";
            _exportStatusText = "Generating preview content...";
            OnPropertyChanged(nameof(ExportStepName));
            OnPropertyChanged(nameof(ExportStatusText));

            _exportPreview = _scenarioExportService.Export(scenarioData, _selectedExportFormat);
            OnPropertyChanged(nameof(ExportPreview));
            _hasExportData = scenarioData.Vertices.Count > 0;
            OnPropertyChanged(nameof(HasExportData));

            // Post-delay with "Finishing up" step
            _exportStepName = "Finishing up";
            _exportStatusText = "Finalizing export preview...";
            OnPropertyChanged(nameof(ExportStepName));
            OnPropertyChanged(nameof(ExportStatusText));
            await Task.Delay(500, _exportCts.Token).ConfigureAwait(true);

            if (!_hasExportData)
            {
                _exportPreview = "// No vertices found in query results.\n// Make sure your query returns graph elements.";
                OnPropertyChanged(nameof(ExportPreview));
                _exportStepName = "";
                _exportStatusText = "No data found";
            }
            else
            {
                _exportStepName = "";
                _exportStatusText = $"Ready: {scenarioData.Vertices.Count} vertices, {scenarioData.Edges.Count} edges";
                StatusText = $"Export ready: {scenarioData.Vertices.Count} vertices, {scenarioData.Edges.Count} edges";
            }
            OnPropertyChanged(nameof(ExportStepName));
            OnPropertyChanged(nameof(ExportStatusText));
        }
        catch (OperationCanceledException)
        {
            _exportPreview = "// Export was cancelled.";
            OnPropertyChanged(nameof(ExportPreview));
            _hasExportData = false;
            OnPropertyChanged(nameof(HasExportData));
            _exportStepName = "";
            _exportStatusText = "Export cancelled";
            OnPropertyChanged(nameof(ExportStepName));
            OnPropertyChanged(nameof(ExportStatusText));
            StatusText = "Export cancelled";
        }
        catch (Exception ex)
        {
            _exportPreview = $"// Error generating preview:\n// {ex.Message}";
            OnPropertyChanged(nameof(ExportPreview));
            _hasExportData = false;
            OnPropertyChanged(nameof(HasExportData));
            _exportStepName = "";
            _exportStatusText = $"Error: {ex.Message}";
            OnPropertyChanged(nameof(ExportStepName));
            OnPropertyChanged(nameof(ExportStatusText));
            StatusText = $"Export error: {ex.Message}";
            _logger.LogWarning(ex, "Failed to generate export preview");
        }
        finally
        {
            _isExporting = false;
            OnPropertyChanged(nameof(IsExporting));
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    [RelayCommand]
    private async Task SaveScenarioExportAsync()
    {
        if (!_hasExportData)
        {
            StatusText = "No data to export";
            return;
        }


        var extension = _selectedExportFormat == ScenarioExportFormat.Json ? "json" : "cs";
        var filter = _selectedExportFormat == ScenarioExportFormat.Json 
            ? "JSON files (*.json)|*.json|All files (*.*)|*.*"
            : "C# files (*.cs)|*.cs|All files (*.*)|*.*";

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Scenario File",
            Filter = filter,
            FileName = $"{_exportScenarioName}.{extension}",
            DefaultExt = extension
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                _exportCts = new CancellationTokenSource();
                _isExporting = true;
                OnPropertyChanged(nameof(IsExporting));

                // Pre-delay with "Initializing" step
                _exportStepName = "Initializing";
                _exportStatusText = "Preparing to save scenario...";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));
                await Task.Delay(500, _exportCts.Token).ConfigureAwait(true);

                _exportCts.Token.ThrowIfCancellationRequested();

                var connector = GetActiveConnector();
                
                _exportStepName = "Parsing Data";
                _exportStatusText = "Parsing query results...";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));

                var scenarioData = _scenarioExportService.ParseQueryResults(
                    ResultJson,
                    _exportScenarioName,
                    _exportScenarioDescription,
                    SelectedConnection?.Name,
                    QueryText);

                _exportCts.Token.ThrowIfCancellationRequested();

                // Fetch edges if we have a connector
                if (connector != null && scenarioData.Vertices.Count > 0)
                {
                    _exportStepName = "Fetching Edges";
                    _exportStatusText = $"Fetching edges for {scenarioData.Vertices.Count} vertices...";
                    OnPropertyChanged(nameof(ExportStepName));
                    OnPropertyChanged(nameof(ExportStatusText));
                    
                    // Create progress reporter for batch updates
                    var progress = new Progress<string>(message =>
                    {
                        _exportStatusText = message;
                        OnPropertyChanged(nameof(ExportStatusText));
                    });
                    
                    scenarioData = await _scenarioExportService.FetchEdgesAsync(scenarioData, connector, progress, _exportCts.Token);
                }

                _exportCts.Token.ThrowIfCancellationRequested();

                _exportStepName = "Writing File";
                _exportStatusText = "Writing scenario file to disk...";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));

                await _scenarioExportService.SaveAsync(scenarioData, _selectedExportFormat, saveDialog.FileName);

                // Post-delay with "Finishing up" step
                _exportStepName = "Finishing up";
                _exportStatusText = "Completing save operation...";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));
                await Task.Delay(500, _exportCts.Token).ConfigureAwait(true);
                
                _exportStepName = "";
                _exportStatusText = $"Saved: {scenarioData.Vertices.Count} vertices, {scenarioData.Edges.Count} edges";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));
                StatusText = $"Scenario saved to {saveDialog.FileName}";
            }
            catch (OperationCanceledException)
            {
                _exportStepName = "";
                _exportStatusText = "Export cancelled";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));
                StatusText = "Export cancelled";
            }
            catch (Exception ex)
            {
                _exportStepName = "";
                _exportStatusText = $"Error: {ex.Message}";
                OnPropertyChanged(nameof(ExportStepName));
                OnPropertyChanged(nameof(ExportStatusText));
                StatusText = $"Failed to save scenario: {ex.Message}";
                _logger.LogError(ex, "Failed to save scenario export");
            }
            finally
            {
                _isExporting = false;
                OnPropertyChanged(nameof(IsExporting));
                _exportCts?.Dispose();
                _exportCts = null;
            }
        }
    }

    [RelayCommand]
    private void CancelExport()
    {
        _exportCts?.Cancel();
        _exportStepName = "Cancelling";
        _exportStatusText = "Cancelling export...";
        OnPropertyChanged(nameof(ExportStepName));
        OnPropertyChanged(nameof(ExportStatusText));
    }

    #endregion

    /// <summary>
    /// Clears the graph and loads only the selected node with its direct connections.
    /// Called when a node is selected from the results list.
    /// </summary>
    private async Task LoadGraphForSelectedNodeAsync(string nodeId)
    {
        var connector = GetActiveConnector();
        if (connector == null || string.IsNullOrEmpty(nodeId))
        {
            return;
        }

        try
        {
            IsLoadingConnections = true;
            StatusText = $"Loading graph for {nodeId}...";

            // Get the selected node data from the results list
            var selectedNodeData = ResultNodes.FirstOrDefault(n => n.Id == nodeId);
            if (selectedNodeData == null) return;

            // Query for connected vertices and edges
            var outVerticesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').out()");
            var inVerticesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').in()");
            var outEdgesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').outE()");
            var inEdgesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').inE()");

            // Build the graph with selected node in center
            var displayedNodes = new Dictionary<string, GraphNodeViewModel>();
            var displayedEdges = new List<GraphEdgeViewModel>();

            // Add selected node at center
            var centerNode = new GraphNodeViewModel
            {
                Id = selectedNodeData.Id,
                Label = selectedNodeData.Label,
                Type = selectedNodeData.Type,
                Fill = selectedNodeData.Fill,
                Stroke = selectedNodeData.Stroke,
                DisplayText = selectedNodeData.DisplayText,
                X = 250,
                Y = 150,
                IsSelected = true
            };
            foreach (var prop in selectedNodeData.Properties)
            {
                centerNode.Properties[prop.Key] = prop.Value;
            }
            displayedNodes[nodeId] = centerNode;

            // Parse connected vertices
            var connectedVertexIds = new HashSet<string>();
            ProcessVerticesForGraph(outVerticesResult.ResultJson, displayedNodes, connectedVertexIds);
            ProcessVerticesForGraph(inVerticesResult.ResultJson, displayedNodes, connectedVertexIds);

            // Parse edges
            ProcessEdgesForGraph(outEdgesResult.ResultJson, displayedEdges);
            ProcessEdgesForGraph(inEdgesResult.ResultJson, displayedEdges);

            // Position connected vertices around center
            var connectedNodes = displayedNodes.Values.Where(n => n.Id != nodeId).ToList();
            if (connectedNodes.Count > 0)
            {
                double radius = 120;
                double angleStep = 2 * Math.PI / connectedNodes.Count;
                for (int i = 0; i < connectedNodes.Count; i++)
                {
                    double angle = i * angleStep - Math.PI / 2;
                    connectedNodes[i].X = 250 + radius * Math.Cos(angle);
                    connectedNodes[i].Y = 150 + radius * Math.Sin(angle);
                }
            }

            // Clear and rebuild the displayed graph canvas
            GraphNodes.Clear();
            GraphEdges.Clear();


            foreach (var node in displayedNodes.Values)
            {
                GraphNodes.Add(node);
            }

            foreach (var edge in displayedEdges)
            {
                // Update edge coordinates (initial calculation without offset)
                if (displayedNodes.TryGetValue(edge.FromId, out var fromNode) &&
                    displayedNodes.TryGetValue(edge.ToId, out var toNode))
                {
                    UpdateEdgeCoordinates(edge, fromNode, toNode, 0);
                    GraphEdges.Add(edge);
                }
            }

            // Calculate curve offsets for parallel edges and recalculate coordinates
            CalculateParallelEdgeOffsets(GraphEdges.ToList());
            RecalculateEdgeCoordinatesWithOffsets(displayedNodes);

            HasGraphData = GraphNodes.Count > 0;

            // Reset pan to center the view on the graph
            GraphPanX = 100;
            GraphPanY = 50;
            GraphZoom = 1.0;

            // Select the center node to update properties panel
            SelectedGraphNode = displayedNodes.GetValueOrDefault(nodeId);

            StatusText = $"Loaded {GraphNodes.Count} nodes, {GraphEdges.Count} edges";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load graph for node {NodeId}", nodeId);
            StatusText = $"Failed to load graph: {ex.Message}";
        }
        finally
        {
            IsLoadingConnections = false;
        }
    }

    private void ProcessVerticesForGraph(string? json, Dictionary<string, GraphNodeViewModel> nodes, HashSet<string> vertexIds)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(json);
            if (items == null) return;

            foreach (var item in items)
            {
                var id = item["id"]?.ToString();
                var label = item["label"]?.ToString();

                if (id != null && !nodes.ContainsKey(id))
                {
                    var node = new GraphNodeViewModel
                    {
                        Id = id,
                        Label = label ?? "unknown",
                        Type = "vertex",
                        Fill = GetNodeFillColor(label ?? "unknown"),
                        Stroke = System.Windows.Media.Brushes.White,
                        DisplayText = label ?? ""
                    };

                    // Extract properties
                    if (item["properties"] is Newtonsoft.Json.Linq.JObject props)
                    {
                        foreach (var prop in props.Properties())
                        {
                            var value = ExtractPropertyValue(prop.Value);
                            if (value != null)
                            {
                                node.Properties[prop.Name] = value;
                            }
                        }
                    }

                    nodes[id] = node;
                    vertexIds.Add(id);
                }
            }
        }
        catch { /* Ignore parse errors */ }
    }

    private void ProcessEdgesForGraph(string? json, List<GraphEdgeViewModel> edges)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(json);
            if (items == null) return;

            var existingEdgeIds = edges.Select(e => e.Id).ToHashSet();

            foreach (var item in items)
            {
                var id = item["id"]?.ToString();
                var label = item["label"]?.ToString();
                var inV = item["inV"]?.ToString();
                var outV = item["outV"]?.ToString();

                if (id != null && !existingEdgeIds.Contains(id) && inV != null && outV != null)
                {
                    edges.Add(new GraphEdgeViewModel
                    {
                        Id = id,
                        Label = label ?? "",
                        FromId = outV,
                        ToId = inV,
                        Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120))
                    });
                    existingEdgeIds.Add(id);
                }
            }
        }
        catch { /* Ignore parse errors */ }
    }

    /// <summary>
    /// Fetches connected nodes and edges for the selected node.
    /// </summary>
    [Obsolete("Use LoadGraphForSelectedNodeAsync instead")]
    private async Task FetchNodeConnectionsAsync(string nodeId)
    {
        var connector = GetActiveConnector();
        if (connector == null || string.IsNullOrEmpty(nodeId))
        {
            return;
        }

        try
        {
            IsLoadingConnections = true;
            StatusText = $"Loading connections for {nodeId}...";


            // Use simpler queries that the in-memory graph supports
            // Query 1: Get outgoing edges
            var outEdgesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').outE()");
            // Query 2: Get incoming edges  
            var inEdgesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').inE()");
            // Query 3: Get connected vertices (out)
            var outVerticesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').out()");
            // Query 4: Get connected vertices (in)
            var inVerticesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').in()");

            // Process results
            var newEdges = new List<GraphEdgeViewModel>();
            var newNodes = new List<GraphNodeViewModel>();
            var existingNodeIds = GraphNodes.Select(n => n.Id).ToHashSet();
            var existingEdgeIds = GraphEdges.Select(e => e.Id).ToHashSet();

            // Process edges
            ProcessEdgeResults(outEdgesResult.ResultJson, newEdges, existingEdgeIds);
            ProcessEdgeResults(inEdgesResult.ResultJson, newEdges, existingEdgeIds);

            // Process vertices
            ProcessVertexResults(outVerticesResult.ResultJson, newNodes, existingNodeIds);
            ProcessVertexResults(inVerticesResult.ResultJson, newNodes, existingNodeIds);

            // Position and add new nodes
            if (newNodes.Count > 0)
            {
                var sourceNode = GraphNodes.FirstOrDefault(n => n.Id == nodeId);
                double baseX = sourceNode?.X ?? 250;
                double baseY = sourceNode?.Y ?? 150;
                double angleStep = 2 * Math.PI / Math.Max(newNodes.Count, 1);
                double radius = 100;

                for (int i = 0; i < newNodes.Count; i++)
                {
                    double angle = i * angleStep - Math.PI / 2;
                    newNodes[i].X = baseX + radius * Math.Cos(angle);
                    newNodes[i].Y = baseY + radius * Math.Sin(angle);
                    GraphNodes.Add(newNodes[i]);
                }
            }

            // Add new edges and update coordinates
            foreach (var edge in newEdges)
            {
                GraphEdges.Add(edge);
            }
            UpdateAllEdgeCoordinates();

            StatusText = $"Loaded {GraphNodes.Count} nodes, {GraphEdges.Count} edges";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch connections for node {NodeId}", nodeId);
            StatusText = $"Failed to load connections: {ex.Message}";
        }
        finally
        {
            IsLoadingConnections = false;
        }
    }

    private void ProcessEdgeResults(string? json, List<GraphEdgeViewModel> edges, HashSet<string> existingIds)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(json);
            if (items == null) return;

            foreach (var item in items)
            {
                var id = item["id"]?.ToString();
                var label = item["label"]?.ToString();
                var inV = item["inV"]?.ToString();
                var outV = item["outV"]?.ToString();

                if (id != null && !existingIds.Contains(id) && inV != null && outV != null)
                {
                    edges.Add(new GraphEdgeViewModel
                    {
                        Id = id,
                        Label = label ?? "",
                        FromId = outV,
                        ToId = inV,
                        Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120))
                    });
                    existingIds.Add(id);
                }
            }
        }
        catch { /* Ignore parse errors */ }
    }

    private void ProcessVertexResults(string? json, List<GraphNodeViewModel> nodes, HashSet<string> existingIds)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(json);
            if (items == null) return;

            foreach (var item in items)
            {
                var id = item["id"]?.ToString();
                var label = item["label"]?.ToString();

                if (id != null && !existingIds.Contains(id))
                {
                    nodes.Add(new GraphNodeViewModel
                    {
                        Id = id,
                        Label = label ?? "unknown",
                        Type = "vertex",
                        Fill = GetNodeFillColor(label ?? "unknown"),
                        Stroke = System.Windows.Media.Brushes.White,
                        DisplayText = label ?? ""
                    });
                    existingIds.Add(id);
                }
            }
        }
        catch { /* Ignore parse errors */ }
    }

    private void AddConnectionsToGraph(string sourceNodeId, List<Dictionary<string, object>> items)
    {
        var existingNodeIds = GraphNodes.Select(n => n.Id).ToHashSet();
        var existingEdgeIds = GraphEdges.Select(e => e.Id).ToHashSet();
        var newNodes = new List<GraphNodeViewModel>();

        // Get source node position for layout
        var sourceNode = GraphNodes.FirstOrDefault(n => n.Id == sourceNodeId);
        double baseX = sourceNode?.X ?? 250;
        double baseY = sourceNode?.Y ?? 150;

        foreach (var item in items)
        {
            var type = item.GetValueOrDefault("type")?.ToString();
            var id = item.GetValueOrDefault("id")?.ToString();
            var label = item.GetValueOrDefault("label")?.ToString();

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
            {
                continue;
            }

            if (type == "vertex" && !existingNodeIds.Contains(id))
            {
                // Add new vertex
                var newNode = new GraphNodeViewModel
                {
                    Id = id,
                    Label = label ?? "unknown",
                    Type = "vertex",
                    Fill = GetNodeFillColor(label ?? "unknown"),
                    Stroke = System.Windows.Media.Brushes.White,
                    DisplayText = label ?? ""
                };
                newNodes.Add(newNode);
                existingNodeIds.Add(id);
            }
            else if (type == "edge" && !existingEdgeIds.Contains(id))
            {
                // Add new edge
                var inV = item.GetValueOrDefault("inV")?.ToString();
                var outV = item.GetValueOrDefault("outV")?.ToString();

                if (!string.IsNullOrEmpty(inV) && !string.IsNullOrEmpty(outV))
                {
                    var edge = new GraphEdgeViewModel
                    {
                        Id = id,
                        Label = label ?? "",
                        FromId = outV,
                        ToId = inV,
                        Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120))
                    };
                    GraphEdges.Add(edge);
                    existingEdgeIds.Add(id);
                }
            }
        }

        // Position new nodes around the source node
        if (newNodes.Count > 0)
        {
            double angleStep = 2 * Math.PI / newNodes.Count;
            double radius = 100;

            for (int i = 0; i < newNodes.Count; i++)
            {
                double angle = i * angleStep - Math.PI / 2;
                newNodes[i].X = baseX + radius * Math.Cos(angle);
                newNodes[i].Y = baseY + radius * Math.Sin(angle);
                GraphNodes.Add(newNodes[i]);
            }

            // Update edge coordinates for all edges
            UpdateAllEdgeCoordinates();
        }
    }

    private void UpdateAllEdgeCoordinates()
    {
        var nodeDict = GraphNodes.ToDictionary(n => n.Id);

        foreach (var edge in GraphEdges)
        {
            if (nodeDict.TryGetValue(edge.FromId, out var fromNode) &&
                nodeDict.TryGetValue(edge.ToId, out var toNode))
            {
                UpdateEdgeCoordinates(edge, fromNode, toNode, edge.CurveOffset);
            }
        }
    }

    #region Private Methods


    private async Task LoadConnectionsAsync()
    {
        try
        {
            var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(true);
            Connections.Clear();
            foreach (var connection in connections)
            {
                Connections.Add(connection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load connections");
            StatusText = $"Error loading connections: {ex.Message}";
        }
    }

    partial void OnSelectedConnectionChanged(GremlinConnectionMetadata? value)
    {
        if (value is null)
        {
            return;
        }

        // Populate edit fields from selected connection
        EditConnectionName = value.Name;
        EditConnectionKind = value.Kind;
        EditHost = value.Host;
        EditPort = value.Port;
        EditUsername = value.Username;
        EditDatabase = value.DatabaseName ?? string.Empty;
        EditGraph = value.GraphName ?? string.Empty;

        // Load secret asynchronously and track the task
        _connectionLoadingTask = LoadSelectedConnectionSecretAsync(value.Id);
    }

    private async Task LoadSelectedConnectionSecretAsync(string connectionId)
    {
        try
        {
            var settings = await _connectionStore.GetConnectionWithSecretAsync(connectionId).ConfigureAwait(true);
            if (settings is not null)
            {
                EditSecret = settings.Secret;

                // Set as active connector if not using playground
                if (!IsPlaygroundRunning)
                {
                    _activeConnector = _connectorFactory.CreateConnector(settings);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load connection secret");
        }
    }

    private IGremlinLanguageConnector? GetActiveConnector()
    {
        // Prefer playground if running
        if (IsPlaygroundRunning && _playgroundService.Connector is not null)
        {
            return _playgroundService.Connector;
        }

        return _activeConnector;
    }

    private void OnPlaygroundStateChanged(object? sender, PlaygroundState state)
    {
        IsPlaygroundRunning = state.IsRunning;
        LoadedScenarioName = state.LoadedScenarioName;
    }

    #endregion
}
