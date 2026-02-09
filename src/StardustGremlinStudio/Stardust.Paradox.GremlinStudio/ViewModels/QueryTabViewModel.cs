using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.Data;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Execution;
using Stardust.Paradox.GremlinStudio.Core.Export;
using Stardust.Paradox.GremlinStudio.Core.History;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// Represents an independent query tab with its own connection, query, and results.
/// </summary>
public partial class QueryTabViewModel : ObservableObject
{
    private readonly IGremlinQueryExecutor _queryExecutor;
    private readonly IQueryHistoryService _queryHistoryService;
    private readonly IScenarioExportService _scenarioExportService;
    private readonly IGremlinConnectorFactory _connectorFactory;
    private readonly ILogger _logger;
    private readonly Action<string> _updateMainStatus;
    private readonly Action _refreshMainHistory;

    private IGremlinLanguageConnector? _activeConnector;
    private CancellationTokenSource? _queryCts;
    private CancellationTokenSource? _exportCts;


    private static int _tabCounter;

    /// <summary>
    /// Resets the tab counter. Call when application restarts or all tabs are closed.
    /// </summary>
    public static void ResetTabCounter() => Interlocked.Exchange(ref _tabCounter, 0);

    public QueryTabViewModel(
        IGremlinQueryExecutor queryExecutor,
        IQueryHistoryService queryHistoryService,
        IScenarioExportService scenarioExportService,
        IGremlinConnectorFactory connectorFactory,
        ILogger logger,
        Action<string> updateMainStatus,
        Action refreshMainHistory,
        GremlinConnectionSettings? initialConnection = null)
    {
        _queryExecutor = queryExecutor;
        _queryHistoryService = queryHistoryService;
        _scenarioExportService = scenarioExportService;
        _connectorFactory = connectorFactory;
        _logger = logger;
        _updateMainStatus = updateMainStatus;
        _refreshMainHistory = refreshMainHistory;

        // Set tab name with thread-safe counter increment
        var tabNumber = Interlocked.Increment(ref _tabCounter);
        TabName = $"Query {tabNumber}";
        Id = Guid.NewGuid().ToString();

        // Initialize collections
        ExportFormats = Enum.GetValues<ScenarioExportFormat>().ToList();
        GraphLayouts = new List<string> { "Force-Directed", "Circular", "Grid" };
        SelectedGraphLayout = "Force-Directed";

        // Initialize connection from snapshot if provided
        if (initialConnection != null)
        {
            ApplyConnectionSettings(initialConnection);
        }

        // Default values
        QueryText = "g.V().limit(10)";
        StatusText = "Ready";
    }

    #region Tab Identity

    /// <summary>
    /// Unique identifier for this tab.
    /// </summary>
    public string Id { get; }

    [ObservableProperty]
    private string _tabName = "New Tab";

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _canClose = true;

    #endregion

    #region Connection (Tab-specific snapshot)

    /// <summary>
    /// The connection settings snapshot for this tab.
    /// </summary>
    [ObservableProperty]
    private GremlinConnectionSettings? _connectionSettings;

    /// <summary>
    /// Display name of the connection used by this tab.
    /// </summary>
    public string ConnectionDisplayName
    {
        get
        {
            if (IsUsingPlayground)
            {
                return string.IsNullOrEmpty(PlaygroundScenarioName)
                    ? "Playground (Empty)"
                    : $"Playground: {PlaygroundScenarioName}";
            }
            return ConnectionSettings?.Metadata.Name ?? "No Connection";
        }
    }

    /// <summary>
    /// The connection metadata (read-only reference to settings).
    /// </summary>
    public GremlinConnectionMetadata? ConnectionMetadata => ConnectionSettings?.Metadata;

    /// <summary>
    /// Indicates whether this tab has an active connection.
    /// </summary>
    public bool HasConnection => ConnectionSettings != null || IsUsingPlayground;

    /// <summary>
    /// Indicates whether the tab is using the playground connector.
    /// </summary>
    [ObservableProperty]
    private bool _isUsingPlayground;

    /// <summary>
    /// The name of the loaded scenario when using playground.
    /// </summary>
    [ObservableProperty]
    private string? _playgroundScenarioName;

    partial void OnPlaygroundScenarioNameChanged(string? value)
    {
        OnPropertyChanged(nameof(ConnectionDisplayName));
        if (IsUsingPlayground)
        {
            TabName = string.IsNullOrEmpty(value) ? "Playground" : $"Playground: {value}";
        }
    }

    /// <summary>
    /// Reference to playground connector when using playground.
    /// </summary>
    private IGremlinLanguageConnector? _playgroundConnector;

    /// <summary>
    /// Applies a connection settings snapshot to this tab.
    /// </summary>
    public void ApplyConnectionSettings(GremlinConnectionSettings? settings)
    {
        ConnectionSettings = settings;
        IsUsingPlayground = false;
        PlaygroundScenarioName = null;
        _playgroundConnector = null;

        if (settings != null)
        {
            _activeConnector = _connectorFactory.CreateConnector(settings);
            TabName = $"{settings.Metadata.Name}";
        }
        else
        {
            _activeConnector = null;
        }

        OnPropertyChanged(nameof(ConnectionDisplayName));
        OnPropertyChanged(nameof(ConnectionMetadata));
        OnPropertyChanged(nameof(HasConnection));
    }

    /// <summary>
    /// Sets the tab to use a playground connector.
    /// </summary>
    public void UsePlaygroundConnector(IGremlinLanguageConnector? connector, string? scenarioName = null)
    {
        _playgroundConnector = connector;
        IsUsingPlayground = connector != null;
        PlaygroundScenarioName = scenarioName;
        ConnectionSettings = null;
        _activeConnector = null;

        if (connector != null)
        {
            TabName = string.IsNullOrEmpty(scenarioName) ? "Playground" : $"Playground: {scenarioName}";
        }

        OnPropertyChanged(nameof(ConnectionDisplayName));
        OnPropertyChanged(nameof(ConnectionMetadata));
        OnPropertyChanged(nameof(HasConnection));
    }

    /// <summary>
    /// Gets the active connector for this tab.
    /// </summary>
    public IGremlinLanguageConnector? GetActiveConnector()
    {
        if (IsUsingPlayground && _playgroundConnector != null)
        {
            return _playgroundConnector;
        }
        return _activeConnector;
    }

    #endregion

    #region Query

    [ObservableProperty]
    private string _queryText = string.Empty;

    partial void OnQueryTextChanged(string value)
    {
        IsDirty = true;
    }

    [ObservableProperty]
    private string _resultJson = string.Empty;

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

    // Status
    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _lastDurationText = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    #endregion

    #region Graph View

    [ObservableProperty]
    private ObservableCollection<GraphNodeViewModel> _resultNodes = new();

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
    private List<string> _graphLayouts;

    [ObservableProperty]
    private string _selectedGraphLayout;

    [ObservableProperty]
    private bool _hasGraphData;

    [ObservableProperty]
    private double _graphZoom = 1.0;

    [ObservableProperty]
    private double _graphPanX = 100;

    [ObservableProperty]
    private double _graphPanY = 50;

    [ObservableProperty]
    private bool _isLoadingConnections;

    // Properties panel
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

    // Node dragging state
    private GraphNodeViewModel? _draggingNode;
    private double _dragStartX;
    private double _dragStartY;

    partial void OnSelectedGraphLayoutChanged(string value)
    {
        if (GraphNodes.Count > 0)
        {
            ReapplyCurrentLayout();
        }
    }

    partial void OnSelectedResultNodeChanged(GraphNodeViewModel? oldValue, GraphNodeViewModel? newValue)
    {
        if (newValue != null)
        {
            _ = LoadGraphForSelectedNodeAsync(newValue.Id);
        }
    }

    partial void OnSelectedGraphNodeChanged(GraphNodeViewModel? oldValue, GraphNodeViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;

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

    #endregion

    #region Scenario Export

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

    [RelayCommand(CanExecute = nameof(CanRunQuery))]
    private async Task RunQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(QueryText))
        {
            StatusText = "Enter a query first";
            return;
        }

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

            _updateMainStatus(StatusText);

            if (result.IsSuccess)
            {
                _queryHistoryService.AddQuery(QueryText);
                _ = _queryHistoryService.SaveAsync();
                _refreshMainHistory();
            }

            PopulateResultViews(result.ResultJson);
            IsDirty = false;
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

    private bool CanRunQuery() => !IsExecuting;


    [RelayCommand]
    private void Cancel()
    {
        _queryCts?.Cancel();
        StatusText = "Cancelling...";
    }

    [RelayCommand]
    private void ExportTableToCsv()
    {
        if (ResultTable == null || ResultTable.Count == 0)
        {
            StatusText = "No data to export";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"query-results-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var csv = new System.Text.StringBuilder();
                var table = ResultTable.Table;
                
                if (table == null)
                {
                    StatusText = "No table data available";
                    return;
                }
                
                // Headers
                var headers = table.Columns.Cast<System.Data.DataColumn>().Select(c => EscapeCsvField(c.ColumnName));
                csv.AppendLine(string.Join(",", headers));
                
                // Rows from the DataView
                foreach (System.Data.DataRowView rowView in ResultTable)
                {
                    var values = rowView.Row.ItemArray.Select(v => EscapeCsvField(v?.ToString() ?? ""));
                    csv.AppendLine(string.Join(",", values));
                }
                
                System.IO.File.WriteAllText(dialog.FileName, csv.ToString(), System.Text.Encoding.UTF8);
                StatusText = $"Exported {ResultTable.Count} rows to {dialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to CSV");
            StatusText = $"Export failed: {ex.Message}";
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
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
        if (SelectedGraphNode != null)
        {
            SelectedGraphNode.IsSelected = false;
            SelectedGraphNode = null;
        }

        if (SelectedGraphEdge != null)
        {
            SelectedGraphEdge.IsSelected = false;
        }

        SelectedGraphEdge = edge;

        if (edge != null)
        {
            edge.IsSelected = true;
            UpdatePropertiesPanel(edge);
        }
        else
        {
            ClearPropertiesPanel();
        }
    }

    [RelayCommand]
    private async Task GenerateExportPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(ResultJson) || ResultJson == "No results")
        {
            ExportPreview = "// No query results to export. Run a query first.";
            HasExportData = false;
            return;
        }

        var connector = GetActiveConnector();
        if (connector == null)
        {
            ExportPreview = "// No active connection. Cannot fetch edges.\n// Connect to a database first.";
            HasExportData = false;
            return;
        }

        try
        {
            _exportCts = new CancellationTokenSource();
            IsExporting = true;

            ExportStepName = "Initializing";
            ExportStatusText = "Preparing export...";
            await Task.Delay(500, _exportCts.Token).ConfigureAwait(true);

            _exportCts.Token.ThrowIfCancellationRequested();

            ExportStepName = "Parsing Data";
            ExportStatusText = "Parsing vertices from query results...";

            var scenarioData = _scenarioExportService.ParseQueryResults(
                ResultJson,
                ExportScenarioName,
                ExportScenarioDescription,
                ConnectionSettings?.Metadata.Name,
                QueryText);

            _exportCts.Token.ThrowIfCancellationRequested();

            if (scenarioData.Vertices.Count > 0)
            {
                ExportStepName = "Fetching Edges";
                ExportStatusText = $"Fetching edges between {scenarioData.Vertices.Count} vertices...";

                var progress = new Progress<string>(message => ExportStatusText = message);
                scenarioData = await _scenarioExportService.FetchEdgesAsync(scenarioData, connector, progress, _exportCts.Token);
            }

            _exportCts.Token.ThrowIfCancellationRequested();

            ExportStepName = "Generating Output";
            ExportStatusText = "Generating preview content...";

            ExportPreview = _scenarioExportService.Export(scenarioData, SelectedExportFormat);
            HasExportData = scenarioData.Vertices.Count > 0;

            ExportStepName = "Finishing up";
            ExportStatusText = "Finalizing export preview...";
            await Task.Delay(500, _exportCts.Token).ConfigureAwait(true);

            if (!HasExportData)
            {
                ExportPreview = "// No vertices found in query results.\n// Make sure your query returns graph elements.";
                ExportStepName = "";
                ExportStatusText = "No data found";
            }
            else
            {
                ExportStepName = "";
                ExportStatusText = $"Ready: {scenarioData.Vertices.Count} vertices, {scenarioData.Edges.Count} edges";
                StatusText = ExportStatusText;
            }
        }
        catch (OperationCanceledException)
        {
            ExportPreview = "// Export was cancelled.";
            HasExportData = false;
            ExportStepName = "";
            ExportStatusText = "Export cancelled";
            StatusText = "Export cancelled";
        }
        catch (Exception ex)
        {
            ExportPreview = $"// Error generating preview:\n// {ex.Message}";
            HasExportData = false;
            ExportStepName = "";
            ExportStatusText = $"Error: {ex.Message}";
            StatusText = $"Export error: {ex.Message}";
            _logger.LogWarning(ex, "Failed to generate export preview");
        }
        finally
        {
            IsExporting = false;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    [RelayCommand]
    private void CancelExport()
    {
        _exportCts?.Cancel();
        ExportStepName = "Cancelling";
        ExportStatusText = "Cancelling export...";
    }

    #endregion

    #region Node Dragging

    public void StartNodeDrag(GraphNodeViewModel node, double startX, double startY)
    {
        _draggingNode = node;
        _dragStartX = startX;
        _dragStartY = startY;
    }

    public void UpdateNodeDrag(double currentX, double currentY)
    {
        if (_draggingNode == null) return;

        double deltaX = (currentX - _dragStartX) / GraphZoom;
        double deltaY = (currentY - _dragStartY) / GraphZoom;

        _draggingNode.X += deltaX;
        _draggingNode.Y += deltaY;

        _dragStartX = currentX;
        _dragStartY = currentY;

        RecalculateEdgesForNode(_draggingNode);
    }

    public void EndNodeDrag()
    {
        _draggingNode = null;
    }

    #endregion

    #region Private Methods

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

            // Populate Table View
            ColumnIsProperty.Clear();
            ResultTable = BuildResultDataTable(results, ColumnIsProperty);

            // Populate Graph View
            PopulateGraphView(results);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse results for visualization");
        }
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

    private void PopulateGraphView(List<object> results)
    {
        var vertices = new Dictionary<string, GraphNodeViewModel>();

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

            if (type != "edge" && !vertices.ContainsKey(id))
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

        foreach (var node in vertices.Values)
        {
            ResultNodes.Add(node);
        }

        HasGraphData = ResultNodes.Count > 0;

        if (ResultNodes.Count > 0)
        {
            SelectedResultNode = ResultNodes[0];
        }
    }

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

            var selectedNodeData = ResultNodes.FirstOrDefault(n => n.Id == nodeId);
            if (selectedNodeData == null) return;

            var outVerticesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').out()");
            var inVerticesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').in()");
            var outEdgesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').outE()");
            var inEdgesResult = await _queryExecutor.ExecuteAsync(connector, $"g.V('{nodeId}').inE()");

            var displayedNodes = new Dictionary<string, GraphNodeViewModel>();
            var displayedEdges = new List<GraphEdgeViewModel>();

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

            var connectedVertexIds = new HashSet<string>();
            ProcessVerticesForGraph(outVerticesResult.ResultJson, displayedNodes, connectedVertexIds);
            ProcessVerticesForGraph(inVerticesResult.ResultJson, displayedNodes, connectedVertexIds);

            ProcessEdgesForGraph(outEdgesResult.ResultJson, displayedEdges);
            ProcessEdgesForGraph(inEdgesResult.ResultJson, displayedEdges);

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

            GraphNodes.Clear();
            GraphEdges.Clear();

            foreach (var node in displayedNodes.Values)
            {
                GraphNodes.Add(node);
            }

            foreach (var edge in displayedEdges)
            {
                if (displayedNodes.TryGetValue(edge.FromId, out var fromNode) &&
                    displayedNodes.TryGetValue(edge.ToId, out var toNode))
                {
                    UpdateEdgeCoordinates(edge, fromNode, toNode, 0);
                    GraphEdges.Add(edge);
                }
            }

            CalculateParallelEdgeOffsets(GraphEdges.ToList());
            RecalculateEdgeCoordinatesWithOffsets(displayedNodes);

            HasGraphData = GraphNodes.Count > 0;
            GraphPanX = 100;
            GraphPanY = 50;
            GraphZoom = 1.0;

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

    private void ReapplyCurrentLayout()
    {
        var nodes = GraphNodes.ToList();
        if (nodes.Count == 0) return;

        var centerNode = nodes.FirstOrDefault(n => n.IsSelected) ?? nodes.First();
        var otherNodes = nodes.Where(n => n != centerNode).ToList();

        centerNode.X = 250;
        centerNode.Y = 150;

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
                        int gridIndex = i + 1;
                        int row = gridIndex / cols;
                        int col = gridIndex % cols;
                        otherNodes[i].X = 100 + col * spacing;
                        otherNodes[i].Y = 80 + row * spacing;
                    }
                    break;

                case "Force-Directed":
                default:
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

    private static void UpdateEdgeCoordinates(GraphEdgeViewModel edge, GraphNodeViewModel from, GraphNodeViewModel to, double perimeterOffset = 0)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) dist = 1;

        double baseAngle = Math.Atan2(dy, dx);
        double maxAngleSpread = 25.0 * Math.PI / 180.0;
        double angleOffset = (perimeterOffset / 60.0) * maxAngleSpread;

        double startAngle = baseAngle + angleOffset;
        edge.X1 = from.X + from.Radius * Math.Cos(startAngle);
        edge.Y1 = from.Y + from.Radius * Math.Sin(startAngle);

        double endAngle = baseAngle + Math.PI + angleOffset;
        edge.X2 = to.X + to.Radius * Math.Cos(endAngle);
        edge.Y2 = to.Y + to.Radius * Math.Sin(endAngle);
    }

    private static void CalculateParallelEdgeOffsets(List<GraphEdgeViewModel> edges)
    {
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
            double baseOffset = 60;

            for (int i = 0; i < count; i++)
            {
                var edge = groupEdges[i];
                double offset = baseOffset * ((i - (count - 1) / 2.0));
                bool isReversed = edge.FromId.CompareTo(edge.ToId) >= 0;
                edge.CurveOffset = isReversed ? -offset : offset;
            }
        }

        var singleEdges = edges
            .GroupBy(e => e.FromId.CompareTo(e.ToId) < 0
                ? (e.FromId, e.ToId)
                : (e.ToId, e.FromId))
            .Where(g => g.Count() == 1)
            .SelectMany(g => g);

        foreach (var edge in singleEdges)
        {
            edge.CurveOffset = 0;
        }
    }

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
        var hash = Math.Abs(label.GetHashCode());
        var colors = new[]
        {
            System.Windows.Media.Color.FromRgb(0, 140, 240),
            System.Windows.Media.Color.FromRgb(32, 160, 32),
            System.Windows.Media.Color.FromRgb(230, 100, 20),
            System.Windows.Media.Color.FromRgb(160, 40, 180),
            System.Windows.Media.Color.FromRgb(0, 180, 200),
            System.Windows.Media.Color.FromRgb(220, 60, 80),
            System.Windows.Media.Color.FromRgb(100, 100, 180),
        };
        return new System.Windows.Media.SolidColorBrush(colors[hash % colors.Length]);
    }

    private static DataView BuildResultDataTable(List<object> results, Dictionary<string, bool> columnIsProperty)
    {
        var table = new DataTable("Results");
        var columnInfo = new Dictionary<string, (string OriginalName, bool IsProperty)>();

        foreach (var item in results)
        {
            if (item is not Newtonsoft.Json.Linq.JObject jObj) continue;

            foreach (var prop in jObj.Properties())
            {
                if (prop.Name == "properties" && prop.Value is Newtonsoft.Json.Linq.JObject propsObj)
                {
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
                    if (!columnInfo.ContainsKey(prop.Name))
                    {
                        columnInfo[prop.Name] = (prop.Name, false);
                    }
                }
            }
        }

        var orderedColumns = columnInfo
            .OrderBy(c => c.Value.IsProperty)
            .ThenBy(c => c.Key == "id" ? 0 : c.Key == "label" ? 1 : c.Key == "type" ? 2 : 3)
            .ThenBy(c => c.Key)
            .ToList();

        foreach (var col in orderedColumns)
        {
            table.Columns.Add(col.Key, typeof(string));
            columnIsProperty[col.Key] = col.Value.IsProperty;
        }

        foreach (var item in results)
        {
            if (item is not Newtonsoft.Json.Linq.JObject jObj) continue;

            var row = table.NewRow();
            var values = new Dictionary<string, string?>();

            foreach (var prop in jObj.Properties())
            {
                if (prop.Name == "properties" && prop.Value is Newtonsoft.Json.Linq.JObject propsObj)
                {
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

    #endregion
}
