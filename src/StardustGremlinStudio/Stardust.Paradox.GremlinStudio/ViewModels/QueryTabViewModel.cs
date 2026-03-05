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
using Stardust.Paradox.GremlinStudio.Core.Schema;

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
    private readonly ISchemaDiscoveryService _schemaDiscoveryService;
    private readonly ISchemaExportService _schemaExportService;
    private readonly ILogger _logger;
    private readonly Action<string> _updateMainStatus;
    private readonly Action _refreshMainHistory;

    private IGremlinLanguageConnector? _activeConnector;
    private CancellationTokenSource? _queryCts;
    private CancellationTokenSource? _exportCts;
    private CancellationTokenSource? _schemaCts;


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
        ISchemaDiscoveryService schemaDiscoveryService,
        ISchemaExportService schemaExportService,
        ILogger logger,
        Action<string> updateMainStatus,
        Action refreshMainHistory,
        GremlinConnectionSettings? initialConnection = null)
    {
        _queryExecutor = queryExecutor;
        _queryHistoryService = queryHistoryService;
        _scenarioExportService = scenarioExportService;
        _connectorFactory = connectorFactory;
        _schemaDiscoveryService = schemaDiscoveryService;
        _schemaExportService = schemaExportService;
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


    // (Snippets/Variables feature removed)

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

    /// <summary>
    /// RU consumption text for Cosmos DB queries (empty for non-Cosmos connections).
    /// </summary>
    [ObservableProperty]
    private string _lastRequestUnitsText = string.Empty;

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
    private string _exportNamespace = "Stardust.Paradox.InMemory.Scenarios";

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

    /// <summary>
    /// Cached scenario data to avoid re-fetching edges when only format changes.
    /// </summary>
    private ScenarioData? _cachedScenarioData;

    /// <summary>
    /// The ResultJson that was used to generate the cached scenario data.
    /// </summary>
    private string? _cachedResultJson;

    /// <summary>
    /// Re-exports the cached scenario data when format changes.
    /// </summary>
    partial void OnSelectedExportFormatChanged(ScenarioExportFormat value)
    {
        // If we have cached data, re-export with the new format immediately
        if (_cachedScenarioData != null && _scenarioExportService != null)
        {
            try
            {
                // Update metadata in case it changed
                _cachedScenarioData.Name = ExportScenarioName;
                _cachedScenarioData.Description = ExportScenarioDescription;
                _cachedScenarioData.Namespace = ExportNamespace;
                
                ExportPreview = _scenarioExportService.Export(_cachedScenarioData, value);
                ExportStatusText = $"Format: {value} ({_cachedScenarioData.Vertices.Count} vertices, {_cachedScenarioData.Edges.Count} edges)";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to re-export with new format");
                ExportStatusText = $"Error changing format: {ex.Message}";
            }
        }
        else if (!string.IsNullOrEmpty(ExportPreview) && ExportPreview != "// No query results to export. Run a query first.")
        {
            // Prompt user to regenerate
            ExportStatusText = "Click 'Generate Preview' to export with the new format";
        }
    }

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
            LastRequestUnitsText = string.Empty;

            // Clear export data when running a new query
            ClearExportData();

            // Reset schema discovery when running a new query (schema is tied to the current results)
            IsDiscoveringSchema = false;
            SchemaProgressPercent = 0;
            SchemaDiscoveryStatus = string.Empty;
            DiscoveredSchema = null;
            SchemaTreeItems.Clear();
            SchemaCodePreview = string.Empty;

            var queryToExecute = QueryText;

            var result = await _queryExecutor.ExecuteAsync(
                connector,
                queryToExecute,
                cancellationToken: _queryCts.Token).ConfigureAwait(true);

            ResultJson = result.ResultJson ?? result.ErrorMessage ?? "No results";
            LastDurationText = $"Query: {result.Duration.TotalMilliseconds:F0}ms";

            LastRequestUnitsText = result.RequestUnits.HasValue
                ? $"RU: {result.RequestUnits.Value:F2}"
                : string.Empty;

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

            // If user is currently viewing schema mode, run a fresh discovery for the new results
            if (IsSchemaExplorerMode && result.IsSuccess)
            {
                _ = BuildSchemaFromResultsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Query cancelled";
            LastRequestUnitsText = string.Empty;
            ResultJson = "Query was cancelled";
            ClearResultViews();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query");
            StatusText = $"Query error: {ex.Message}";
            LastRequestUnitsText = string.Empty;
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

    /// <summary>
    /// Clears all export data, settings, and preview.
    /// </summary>
    private void ClearExportData()
    {
        _cachedScenarioData = null;
        _cachedResultJson = null;
        ExportPreview = "";
        HasExportData = false;
        ExportStatusText = "";
        ExportStepName = "";
    }

    private bool CanRunQuery() => !IsExecuting;


    [RelayCommand]
    private void Cancel()
    {
        _queryCts?.Cancel();
        StatusText = "Cancelling...";
    }

    [RelayCommand]
    private void CopyJsonToClipboard()
    {
        if (string.IsNullOrWhiteSpace(ResultJson))
        {
            StatusText = "No JSON to copy";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(ResultJson);
            StatusText = "JSON copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to copy: {ex.Message}";
        }
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
            _cachedScenarioData = null;
            _cachedResultJson = null;
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

            ScenarioData scenarioData;

            // Check if we can use cached data (same result JSON)
            if (_cachedScenarioData != null && _cachedResultJson == ResultJson)
            {
                ExportStepName = "Generating Output";
                ExportStatusText = "Using cached data, generating output...";
                await Task.Delay(100, _exportCts.Token).ConfigureAwait(true);
                
                // Update metadata from UI
                _cachedScenarioData.Name = ExportScenarioName;
                _cachedScenarioData.Description = ExportScenarioDescription;
                _cachedScenarioData.Namespace = ExportNamespace;
                
                scenarioData = _cachedScenarioData;
            }
            else
            {
                // Need to re-parse and fetch edges
                ExportStepName = "Initializing";
                ExportStatusText = "Preparing export...";
                await Task.Delay(500, _exportCts.Token).ConfigureAwait(true);

                _exportCts.Token.ThrowIfCancellationRequested();

                ExportStepName = "Parsing Data";
                ExportStatusText = "Parsing vertices from query results...";

                scenarioData = _scenarioExportService.ParseQueryResults(
                    ResultJson,
                    ExportScenarioName,
                    ExportScenarioDescription,
                    ConnectionSettings?.Metadata.Name,
                    QueryText);

                // Set the namespace for C# export
                scenarioData.Namespace = ExportNamespace;

                _exportCts.Token.ThrowIfCancellationRequested();

                if (scenarioData.Vertices.Count > 0)
                {
                    ExportStepName = "Fetching Edges";
                    ExportStatusText = $"Fetching edges between {scenarioData.Vertices.Count} vertices...";

                    var progress = new Progress<string>(message => ExportStatusText = message);
                    scenarioData = await _scenarioExportService.FetchEdgesAsync(scenarioData, connector, progress, _exportCts.Token);
                }

                // Cache the scenario data
                _cachedScenarioData = scenarioData;
                _cachedResultJson = ResultJson;
            }

            _exportCts.Token.ThrowIfCancellationRequested();

            ExportStepName = "Generating Output";
            ExportStatusText = "Generating preview content...";

            ExportPreview = _scenarioExportService.Export(scenarioData, SelectedExportFormat);
            HasExportData = scenarioData.Vertices.Count > 0;

            ExportStepName = "Finishing up";
            ExportStatusText = "Finalizing export preview...";
            await Task.Delay(200, _exportCts.Token).ConfigureAwait(true);

            if (!HasExportData)
            {
                ExportPreview = "// No vertices found in query results.\n// Make sure your query returns graph elements.";
                ExportStepName = "";
                ExportStatusText = "No data found";
                _cachedScenarioData = null;
                _cachedResultJson = null;
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
                    var edge = new GraphEdgeViewModel
                    {
                        Id = id,
                        Label = label ?? "",
                        FromId = outV,
                        ToId = inV,
                        Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120))
                    };

                    if (item["properties"] is Newtonsoft.Json.Linq.JObject props)
                    {
                        foreach (var prop in props.Properties())
                        {
                            var value = ExtractPropertyValue(prop.Value);
                            if (value != null)
                            {
                                edge.Properties[prop.Name] = value;
                            }
                        }
                    }

                    edges.Add(edge);
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

    #region Schema Explorer

    /// <summary>
    /// Whether the tree view is showing schema or data.
    /// </summary>
    [ObservableProperty]
    private bool _isSchemaExplorerMode;

    /// <summary>
    /// The discovered graph schema.
    /// </summary>
    [ObservableProperty]
    private GraphSchema? _discoveredSchema;

    /// <summary>
    /// Tree items for schema display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TreeNodeViewModel> _schemaTreeItems = new();

    /// <summary>
    /// Whether schema discovery is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isDiscoveringSchema;

    /// <summary>
    /// Schema discovery progress percentage.
    /// </summary>
    [ObservableProperty]
    private int _schemaProgressPercent;

    /// <summary>
    /// Schema discovery status message.
    /// </summary>
    [ObservableProperty]
    private string _schemaDiscoveryStatus = string.Empty;

    /// <summary>
    /// Whether schema has been discovered.
    /// </summary>
    public bool HasDiscoveredSchema => DiscoveredSchema != null;

    /// <summary>
    /// The generated schema code preview.
    /// </summary>
    [ObservableProperty]
    private string _schemaCodePreview = string.Empty;

    /// <summary>
    /// Namespace for schema export.
    /// </summary>
    [ObservableProperty]
    private string _schemaExportNamespace = "MyApp.Graph.Entities";

    /// <summary>
    /// Context class name for schema export.
    /// </summary>
    [ObservableProperty]
    private string _schemaContextClassName = "MyGraphContext";

    partial void OnIsSchemaExplorerModeChanged(bool value)
    {
        // Switch between schema and data tree views
        if (value)
        {
            if (DiscoveredSchema != null)
            {
                // Already have schema, just refresh view
                BuildSchemaTreeView();
            }
            else if (!string.IsNullOrWhiteSpace(ResultJson) && ResultJson != "No results")
            {
                // Build schema from current query results (async with edge fetching)
                _ = BuildSchemaFromResultsAsync();
            }
        }
    }

    partial void OnDiscoveredSchemaChanged(GraphSchema? value)
    {
        OnPropertyChanged(nameof(HasDiscoveredSchema));
        if (value != null && IsSchemaExplorerMode)
        {
            BuildSchemaTreeView();
        }
    }

    [RelayCommand]
    private void ToggleSchemaExplorerMode()
    {
        IsSchemaExplorerMode = !IsSchemaExplorerMode;
    }

    /// <summary>
    /// Builds a schema from the current query results, fetching related edges from the database.
    /// Uses the same approach as the export feature.
    /// </summary>
    private async Task BuildSchemaFromResultsAsync()
    {
        if (string.IsNullOrWhiteSpace(ResultJson) || ResultJson == "No results")
        {
            StatusText = "No query results. Run a query that returns vertices/edges first.";
            SchemaDiscoveryStatus = "No data available";
            return;
        }

        var connector = GetActiveConnector();

        try
        {
            _schemaCts = new CancellationTokenSource();
            IsDiscoveringSchema = true;
            SchemaDiscoveryStatus = "Analyzing query results...";
            SchemaProgressPercent = 0;

            var results = Newtonsoft.Json.JsonConvert.DeserializeObject<List<object>>(ResultJson);
            if (results == null || results.Count == 0)
            {
                StatusText = "No parseable results found";
                SchemaDiscoveryStatus = "No data";
                return;
            }

            var schema = new GraphSchema
            {
                DiscoveredAt = DateTime.UtcNow,
                SourceConnection = ConnectionDisplayName
            };

            var vertexLabels = new Dictionary<string, SchemaVertexLabel>();
            var edgeLabels = new Dictionary<string, SchemaEdgeLabel>();
            var vertexIds = new List<string>();
            // Map vertex ID to label for edge processing
            var vertexIdToLabel = new Dictionary<string, string>();
            // Track edge connections: edgeLabel -> (sourceLabel, targetLabel)
            var edgeConnections = new Dictionary<string, List<(string Source, string Target)>>();
            var seenEdgeIds = new HashSet<string>();

            // Phase 1: Parse vertices and edges from current results
            SchemaDiscoveryStatus = "Parsing query results...";
            int processed = 0;
            int total = results.Count;

            foreach (var item in results)
            {
                processed++;
                SchemaProgressPercent = (int)((processed * 30.0) / total); // First 30% for parsing

                if (item is not Newtonsoft.Json.Linq.JObject jObj)
                    continue;

                var type = jObj["type"]?.ToString();
                var label = jObj["label"]?.ToString();
                var id = jObj["id"]?.ToString();

                if (string.IsNullOrEmpty(label))
                    continue;

                if (type == "edge")
                {
                    var edgeId = id;
                    if (!string.IsNullOrEmpty(edgeId) && !seenEdgeIds.Contains(edgeId))
                    {
                        seenEdgeIds.Add(edgeId);
                        ProcessEdgeForSchema(jObj, label, edgeLabels, edgeConnections, schema, vertexIdToLabel);
                    }
                }
                else
                {
                    // Process vertex
                    if (!string.IsNullOrEmpty(id))
                    {
                        if (!vertexIds.Contains(id))
                        {
                            vertexIds.Add(id);
                        }
                        // Map vertex ID to label for edge lookups
                        vertexIdToLabel[id] = label;
                    }

                    if (!vertexLabels.TryGetValue(label, out var vertexLabel))
                    {
                        vertexLabel = new SchemaVertexLabel { Label = label };
                        vertexLabels[label] = vertexLabel;
                    }

                    vertexLabel.Count++;
                    schema.VertexSampleCount++;

                    // Extract vertex properties
                    if (jObj["properties"] is Newtonsoft.Json.Linq.JObject props)
                    {
                        ExtractPropertiesFromVertexFormat(props, vertexLabel.Properties);
                    }
                }
            }

            // Phase 2: Fetch edges for vertices if we have a connection and vertices
            if (connector != null && vertexIds.Count > 0)
            {
                SchemaDiscoveryStatus = $"Fetching edges for {vertexIds.Count} vertices...";
                
                const int batchSize = 100; // Same as export service
                var totalVertices = vertexIds.Count;
                var processedVertices = 0;

                for (int batchStart = 0; batchStart < totalVertices; batchStart += batchSize)
                {
                    _schemaCts.Token.ThrowIfCancellationRequested();

                    var batchEnd = Math.Min(batchStart + batchSize, totalVertices);
                    var batchVertexIds = vertexIds.Skip(batchStart).Take(batchSize).ToList();

                    SchemaDiscoveryStatus = $"Fetching edges for vertices {batchStart + 1}-{batchEnd} of {totalVertices}...";

                    foreach (var vertexId in batchVertexIds)
                    {
                        _schemaCts.Token.ThrowIfCancellationRequested();

                        try
                        {
                            var escapedId = EscapeGremlinString(vertexId);
                            var edgeQuery = $"g.V('{escapedId}').bothE()";

                            // Use _queryExecutor to get proper JSON results (same as main query execution)
                            var edgeQueryResult = await _queryExecutor.ExecuteAsync(connector, edgeQuery, cancellationToken: _schemaCts.Token).ConfigureAwait(true);
                            
                            if (edgeQueryResult.IsSuccess && !string.IsNullOrEmpty(edgeQueryResult.ResultJson))
                            {
                                // Parse JSON results - this is reliable since it works for main queries
                                var edgeItems = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(edgeQueryResult.ResultJson);
                                
                                if (edgeItems != null)
                                {
                                    foreach (var edgeJObj in edgeItems)
                                    {
                                        var edgeId = edgeJObj["id"]?.ToString();
                                        var edgeLabel = edgeJObj["label"]?.ToString();
                                        var outV = edgeJObj["outV"]?.ToString();
                                        var inV = edgeJObj["inV"]?.ToString();
                                        var outVLabelFromEdge = edgeJObj["outVLabel"]?.ToString();
                                        var inVLabelFromEdge = edgeJObj["inVLabel"]?.ToString();
                                        
                                        if (string.IsNullOrEmpty(edgeId) || string.IsNullOrEmpty(edgeLabel))
                                            continue;
                                            
                                        if (seenEdgeIds.Contains(edgeId))
                                            continue;
                                            
                                        seenEdgeIds.Add(edgeId);
                                        
                                        // Get vertex labels from edge or from our map
                                        string? outVLabel = outVLabelFromEdge;
                                        string? inVLabel = inVLabelFromEdge;
                                        
                                        // If labels not in edge, look them up from our vertex map
                                        if (string.IsNullOrEmpty(outVLabel) && !string.IsNullOrEmpty(outV))
                                        {
                                            vertexIdToLabel.TryGetValue(outV, out outVLabel);
                                        }
                                        if (string.IsNullOrEmpty(inVLabel) && !string.IsNullOrEmpty(inV))
                                        {
                                            vertexIdToLabel.TryGetValue(inV, out inVLabel);
                                        }
                                        
                                        // If we still don't have labels, try fetching the vertices
                                        if (string.IsNullOrEmpty(outVLabel) && !string.IsNullOrEmpty(outV))
                                        {
                                            outVLabel = await FetchVertexLabelAsync(connector, outV, vertexIdToLabel, vertexLabels, schema).ConfigureAwait(true);
                                        }
                                        if (string.IsNullOrEmpty(inVLabel) && !string.IsNullOrEmpty(inV))
                                        {
                                            inVLabel = await FetchVertexLabelAsync(connector, inV, vertexIdToLabel, vertexLabels, schema).ConfigureAwait(true);
                                        }
                                        
                                        // ALWAYS process the edge to capture the edge label
                                        ProcessEdgeForSchemaWithLabels(
                                            edgeLabel, 
                                            outVLabel, 
                                            inVLabel, 
                                            edgeJObj,
                                            edgeLabels, 
                                            edgeConnections, 
                                            schema);
                                    }
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch edges for vertex {VertexId}", vertexId);
                        }

                        processedVertices++;
                        SchemaProgressPercent = 30 + (int)((processedVertices * 60.0) / totalVertices); // 30-90%
                    }
                }
            }

            // Phase 3: Build edge connections on vertex labels
            SchemaDiscoveryStatus = "Building schema relationships...";
            SchemaProgressPercent = 90;
            foreach (var kvp in edgeConnections)
            {
                var edgeLabelName = kvp.Key;
                foreach (var (sourceLabel, targetLabel) in kvp.Value)
                {
                    // Add outgoing edge to source vertex
                    if (vertexLabels.TryGetValue(sourceLabel, out var sourceVertex))
                    {
                        if (!sourceVertex.OutgoingEdges.Any(e => e.EdgeLabel == edgeLabelName && e.VertexLabel == targetLabel))
                        {
                            sourceVertex.OutgoingEdges.Add(new SchemaEdgeConnection
                            {
                                EdgeLabel = edgeLabelName,
                                VertexLabel = targetLabel
                            });
                        }
                    }

                    // Add incoming edge to target vertex
                    if (vertexLabels.TryGetValue(targetLabel, out var targetVertex))
                    {
                        if (!targetVertex.IncomingEdges.Any(e => e.EdgeLabel == edgeLabelName && e.VertexLabel == sourceLabel))
                        {
                            targetVertex.IncomingEdges.Add(new SchemaEdgeConnection
                            {
                                EdgeLabel = edgeLabelName,
                                VertexLabel = sourceLabel
                            });
                        }
                    }
                }
            }

            schema.VertexLabels.AddRange(vertexLabels.Values.OrderBy(v => v.Label));
            schema.EdgeLabels.AddRange(edgeLabels.Values.OrderBy(e => e.Label));

            DiscoveredSchema = schema;
            SchemaProgressPercent = 100;
            SchemaDiscoveryStatus = "Complete";

            StatusText = $"Schema built: {schema.VertexLabels.Count} vertex types, {schema.EdgeLabels.Count} edge types";
            _updateMainStatus(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Schema discovery cancelled";
            SchemaDiscoveryStatus = "Cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build schema from results");
            StatusText = $"Schema analysis failed: {ex.Message}";
            SchemaDiscoveryStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsDiscoveringSchema = false;
            _schemaCts?.Dispose();
            _schemaCts = null;
        }
    }

    /// <summary>
    /// Fetches a vertex label by ID and adds it to the vertex labels dictionary if found.
    /// </summary>
    private async Task<string?> FetchVertexLabelAsync(
        IGremlinLanguageConnector connector,
        string vertexId,
        Dictionary<string, string> vertexIdToLabel,
        Dictionary<string, SchemaVertexLabel> vertexLabels,
        GraphSchema schema)
    {
        // Already cached
        if (vertexIdToLabel.TryGetValue(vertexId, out var cachedLabel))
            return cachedLabel;

        try
        {
            var escapedId = EscapeGremlinString(vertexId);
            var query = $"g.V('{escapedId}')";
            
            // Use _queryExecutor for reliable JSON parsing
            var result = await _queryExecutor.ExecuteAsync(connector, query).ConfigureAwait(true);
            
            if (result.IsSuccess && !string.IsNullOrEmpty(result.ResultJson))
            {
                var vertices = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(result.ResultJson);
                
                if (vertices != null && vertices.Count > 0)
                {
                    var jObj = vertices[0];
                    var label = jObj["label"]?.ToString();
                    var propsObj = jObj["properties"] as Newtonsoft.Json.Linq.JObject;

                    if (!string.IsNullOrEmpty(label))
                    {
                        vertexIdToLabel[vertexId] = label;
                        
                        // Also add to vertex labels if not already there
                        if (!vertexLabels.ContainsKey(label))
                        {
                            var schemaVertex = new SchemaVertexLabel { Label = label };
                            vertexLabels[label] = schemaVertex;
                            
                            // Extract properties if available
                            if (propsObj != null)
                            {
                                ExtractPropertiesFromVertexFormat(propsObj, schemaVertex.Properties);
                            }
                        }
                        
                        vertexLabels[label].Count++;
                        schema.VertexSampleCount++;
                        
                        return label;
                    }
                }
            }
        }
        catch
        {
            // Ignore errors fetching vertex
        }

        return null;
    }

    /// <summary>
    /// Processes an edge and adds it to the schema (from initial results with inVLabel/outVLabel).
    /// </summary>
    private static void ProcessEdgeForSchema(
        Newtonsoft.Json.Linq.JObject jObj,
        string label,
        Dictionary<string, SchemaEdgeLabel> edgeLabels,
        Dictionary<string, List<(string Source, string Target)>> edgeConnections,
        GraphSchema schema,
        Dictionary<string, string> vertexIdToLabel)
    {
        // Try to get labels directly from the edge (present in initial results)
        var inVLabel = jObj["inVLabel"]?.ToString();
        var outVLabel = jObj["outVLabel"]?.ToString();
        
        // If not present, try to look up from vertex ID map
        if (string.IsNullOrEmpty(inVLabel))
        {
            var inV = jObj["inV"]?.ToString();
            if (!string.IsNullOrEmpty(inV))
                vertexIdToLabel.TryGetValue(inV, out inVLabel);
        }
        if (string.IsNullOrEmpty(outVLabel))
        {
            var outV = jObj["outV"]?.ToString();
            if (!string.IsNullOrEmpty(outV))
                vertexIdToLabel.TryGetValue(outV, out outVLabel);
        }

        ProcessEdgeForSchemaWithLabels(label, outVLabel, inVLabel, jObj, edgeLabels, edgeConnections, schema);
    }

    /// <summary>
    /// Processes an edge with known vertex labels.
    /// </summary>
    private static void ProcessEdgeForSchemaWithLabels(
        string label,
        string? outVLabel,
        string? inVLabel,
        Newtonsoft.Json.Linq.JObject? propsSource,
        Dictionary<string, SchemaEdgeLabel> edgeLabels,
        Dictionary<string, List<(string Source, string Target)>> edgeConnections,
        GraphSchema schema)
    {
        if (!edgeLabels.TryGetValue(label, out var edgeLabel))
        {
            edgeLabel = new SchemaEdgeLabel { Label = label };
            edgeLabels[label] = edgeLabel;
            edgeConnections[label] = new List<(string, string)>();
        }

        edgeLabel.Count++;
        schema.EdgeSampleCount++;

        // Track source and target labels
        if (!string.IsNullOrEmpty(outVLabel) && !edgeLabel.SourceLabels.Contains(outVLabel))
        {
            edgeLabel.SourceLabels.Add(outVLabel);
        }
        if (!string.IsNullOrEmpty(inVLabel) && !edgeLabel.TargetLabels.Contains(inVLabel))
        {
            edgeLabel.TargetLabels.Add(inVLabel);
        }

        // Track connection for vertex edge lists
        if (!string.IsNullOrEmpty(outVLabel) && !string.IsNullOrEmpty(inVLabel))
        {
            var conn = (outVLabel, inVLabel);
            if (!edgeConnections[label].Contains(conn))
            {
                edgeConnections[label].Add(conn);
            }
        }

        // Extract edge properties
        if (propsSource?["properties"] is Newtonsoft.Json.Linq.JObject edgeProps)
        {
            ExtractProperties(edgeProps, edgeLabel.Properties);
        }
    }

    /// <summary>
    /// Edge data structure for schema discovery.
    /// </summary>
    private struct SchemaEdgeData
    {
        public string Id;
        public string Label;
        public string? OutV;
        public string? InV;
        public string? OutVLabel;
        public string? InVLabel;
        public Newtonsoft.Json.Linq.JObject? Properties;
    }

    /// <summary>
    /// Parses an edge from dynamic result for schema discovery.
    /// Matches the export service's ParseEdgeFromDynamic logic.
    /// </summary>
    private static SchemaEdgeData? ParseEdgeFromDynamicForSchema(dynamic edge)
    {
        try
        {
            string? id = null;
            string? label = null;
            string? outV = null;
            string? inV = null;
            string? outVLabel = null;
            string? inVLabel = null;
            Newtonsoft.Json.Linq.JObject? propsJObj = null;

            // Handle different edge result formats (same as export service)
            if (edge is Newtonsoft.Json.Linq.JObject edgeJObj)
            {
                id = edgeJObj["id"]?.ToString();
                label = edgeJObj["label"]?.ToString();
                outV = edgeJObj["outV"]?.ToString();
                inV = edgeJObj["inV"]?.ToString();
                outVLabel = edgeJObj["outVLabel"]?.ToString();
                inVLabel = edgeJObj["inVLabel"]?.ToString();
                propsJObj = edgeJObj;
            }
            else if (edge is IDictionary<string, object> dict)
            {
                id = dict.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                label = dict.TryGetValue("label", out var labelVal) ? labelVal?.ToString() : null;
                outV = dict.TryGetValue("outV", out var outVVal) ? outVVal?.ToString() : null;
                inV = dict.TryGetValue("inV", out var inVVal) ? inVVal?.ToString() : null;
                outVLabel = dict.TryGetValue("outVLabel", out var outVLabelVal) ? outVLabelVal?.ToString() : null;
                inVLabel = dict.TryGetValue("inVLabel", out var inVLabelVal) ? inVLabelVal?.ToString() : null;
                
                try
                {
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
                    propsJObj = Newtonsoft.Json.Linq.JObject.Parse(json);
                }
                catch { /* ignore serialization errors */ }
            }
            else
            {
                // Fallback: Try to access properties dynamically (matches export service)
                try
                {
                    id = edge.id?.ToString();
                    label = edge.label?.ToString();
                    outV = edge.outV?.ToString();
                    inV = edge.inV?.ToString();
                    // outVLabel and inVLabel usually not present in dynamic access
                }
                catch { /* ignore dynamic access errors */ }
            }

            // Edge must have id, label, and both vertex references
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(label) || 
                string.IsNullOrEmpty(outV) || string.IsNullOrEmpty(inV))
                return null;

            return new SchemaEdgeData
            {
                Id = id,
                Label = label,
                OutV = outV,
                InV = inV,
                OutVLabel = outVLabel,
                InVLabel = inVLabel,
                Properties = propsJObj
            };
        }
        catch
        {
            // Ignore parsing errors
        }
        return null;
    }

    /// <summary>
    /// Escapes a string for use in a Gremlin query.
    /// </summary>
    private static string EscapeGremlinString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    /// <summary>
    /// Extracts properties from edge format (simple key-value pairs).
    /// </summary>
    private static void ExtractProperties(Newtonsoft.Json.Linq.JObject props, List<SchemaProperty> propertyList)
    {
        foreach (var prop in props.Properties())
        {
            var propName = prop.Name;
            var existing = propertyList.FirstOrDefault(p => p.Name == propName);

            if (existing == null)
            {
                var schemaProperty = new SchemaProperty
                {
                    Name = propName,
                    InferredType = InferPropertyType(prop.Value),
                    SampleValues = new List<string>()
                };

                var sampleValue = prop.Value?.ToString();
                if (!string.IsNullOrEmpty(sampleValue) && schemaProperty.SampleValues.Count < 3)
                {
                    schemaProperty.SampleValues.Add(sampleValue.Length > 50 ? sampleValue.Substring(0, 50) + "..." : sampleValue);
                }

                propertyList.Add(schemaProperty);
            }
        }
    }

    /// <summary>
    /// Extracts properties from vertex format (Gremlin vertex properties are arrays).
    /// </summary>
    private static void ExtractPropertiesFromVertexFormat(Newtonsoft.Json.Linq.JObject props, List<SchemaProperty> propertyList)
    {
        foreach (var prop in props.Properties())
        {
            var propName = prop.Name;
            var existing = propertyList.FirstOrDefault(p => p.Name == propName);

            if (existing == null)
            {
                var schemaProperty = new SchemaProperty
                {
                    Name = propName,
                    SampleValues = new List<string>()
                };

                // Vertex properties in Gremlin are arrays of {id, value} objects
                if (prop.Value is Newtonsoft.Json.Linq.JArray arr && arr.Count > 0)
                {
                    var firstItem = arr[0];
                    if (firstItem is Newtonsoft.Json.Linq.JObject propObj && propObj["value"] != null)
                    {
                        schemaProperty.InferredType = InferPropertyType(propObj["value"]);
                        var sampleValue = propObj["value"]?.ToString();
                        if (!string.IsNullOrEmpty(sampleValue))
                        {
                            schemaProperty.SampleValues.Add(sampleValue.Length > 50 ? sampleValue.Substring(0, 50) + "..." : sampleValue);
                        }
                    }
                    else
                    {
                        schemaProperty.InferredType = InferPropertyType(firstItem);
                    }
                }
                else
                {
                    schemaProperty.InferredType = InferPropertyType(prop.Value);
                }

                propertyList.Add(schemaProperty);
            }
        }
    }

    /// <summary>
    /// Infers the C# type from a JSON token.
    /// </summary>
    private static string InferPropertyType(Newtonsoft.Json.Linq.JToken? token)
    {
        if (token == null)
            return "string";

        return token.Type switch
        {
            Newtonsoft.Json.Linq.JTokenType.Integer => "int",
            Newtonsoft.Json.Linq.JTokenType.Float => "double",
            Newtonsoft.Json.Linq.JTokenType.Boolean => "bool",
            Newtonsoft.Json.Linq.JTokenType.Date => "DateTime",
            Newtonsoft.Json.Linq.JTokenType.Array => "ICollection<string>",
            _ => "string"
        };
    }

    [RelayCommand(CanExecute = nameof(CanDiscoverSchema))]
    private async Task DiscoverSchemaAsync()
    {
        // Trigger the async schema building with edge fetching
        await BuildSchemaFromResultsAsync().ConfigureAwait(true);
    }

    private bool CanDiscoverSchema() => !IsDiscoveringSchema && !string.IsNullOrWhiteSpace(ResultJson) && ResultJson != "No results";

    [RelayCommand]
    private void CancelSchemaDiscovery()
    {
        _schemaCts?.Cancel();
        SchemaDiscoveryStatus = "Cancelling...";
    }

    [RelayCommand]
    private void ExportSchemaToCode()
    {
        if (DiscoveredSchema == null)
        {
            StatusText = "No schema discovered. Click 'Discover Schema' first.";
            return;
        }

        try
        {
            var options = new SchemaExportOptions
            {
                Namespace = SchemaExportNamespace,
                ContextClassName = SchemaContextClassName,
                GenerateTypedEdges = true,
                GenerateNavigationProperties = true,
                IncludeXmlDocumentation = true,
                GenerateContext = true,
                UseFileScopedNamespace = true,
                UseNullableReferenceTypes = true
            };

            SchemaCodePreview = _schemaExportService.ExportToCode(DiscoveredSchema, options);
            StatusText = "Schema code generated successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export schema to code");
            StatusText = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveSchemaToFile()
    {
        if (string.IsNullOrWhiteSpace(SchemaCodePreview))
        {
            StatusText = "Generate schema code first";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
                DefaultExt = ".cs",
                FileName = $"{SchemaContextClassName}.cs"
            };

            if (dialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(dialog.FileName, SchemaCodePreview);
                StatusText = $"Schema saved to {dialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save schema file");
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopySchemaToClipboard()
    {
        if (string.IsNullOrWhiteSpace(SchemaCodePreview))
        {
            StatusText = "Generate schema code first";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(SchemaCodePreview);
            StatusText = "Schema code copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to copy: {ex.Message}";
        }
    }

    private void BuildSchemaTreeView()
    {
        SchemaTreeItems.Clear();

        if (DiscoveredSchema == null)
            return;

        // Root node for schema info
        var rootNode = new TreeNodeViewModel
        {
            Icon = "??",
            Name = "Graph Schema",
            Value = $"Discovered at {DiscoveredSchema.DiscoveredAt:HH:mm:ss}",
            FontWeight = "Bold",
            IsExpanded = true
        };

        // Vertex Labels section
        var vertexNode = new TreeNodeViewModel
        {
            Icon = "??",
            Name = "Vertex Labels",
            Value = $"({DiscoveredSchema.VertexLabels.Count})",
            FontWeight = "SemiBold",
            IsExpanded = true
        };

        foreach (var vLabel in DiscoveredSchema.VertexLabels.OrderBy(v => v.Label))
        {
            var labelNode = new TreeNodeViewModel
            {
                Icon = "?",
                Name = vLabel.Label,
                Value = $"({vLabel.Count} instances)",
                IsExpanded = false
            };

            // Properties
            if (vLabel.Properties.Count > 0)
            {
                var propsNode = new TreeNodeViewModel
                {
                    Icon = "??",
                    Name = "Properties",
                    Value = $"({vLabel.Properties.Count})"
                };

                foreach (var prop in vLabel.Properties)
                {
                    propsNode.Children.Add(new TreeNodeViewModel
                    {
                        Icon = "•",
                        Name = prop.Name,
                        Value = $"{prop.CSharpType}"
                    });
                }

                labelNode.Children.Add(propsNode);
            }

            // Outgoing edges
            if (vLabel.OutgoingEdges.Count > 0)
            {
                var outNode = new TreeNodeViewModel
                {
                    Icon = "??",
                    Name = "Outgoing Edges",
                    Value = $"({vLabel.OutgoingEdges.Count})"
                };

                foreach (var edge in vLabel.OutgoingEdges)
                {
                    outNode.Children.Add(new TreeNodeViewModel
                    {
                        Icon = "?",
                        Name = edge.EdgeLabel,
                        Value = $"? {edge.VertexLabel}"
                    });
                }

                labelNode.Children.Add(outNode);
            }

            // Incoming edges
            if (vLabel.IncomingEdges.Count > 0)
            {
                var inNode = new TreeNodeViewModel
                {
                    Icon = "??",
                    Name = "Incoming Edges",
                    Value = $"({vLabel.IncomingEdges.Count})"
                };

                foreach (var edge in vLabel.IncomingEdges)
                {
                    inNode.Children.Add(new TreeNodeViewModel
                    {
                        Icon = "?",
                        Name = edge.EdgeLabel,
                        Value = $"? {edge.VertexLabel}"
                    });
                }

                labelNode.Children.Add(inNode);
            }

            vertexNode.Children.Add(labelNode);
        }

        rootNode.Children.Add(vertexNode);

        // Edge Labels section
        var edgeNode = new TreeNodeViewModel
        {
            Icon = "??",
            Name = "Edge Labels",
            Value = $"({DiscoveredSchema.EdgeLabels.Count})",
            FontWeight = "SemiBold",
            IsExpanded = true
        };

        foreach (var eLabel in DiscoveredSchema.EdgeLabels.OrderBy(e => e.Label))
        {
            var labelNode = new TreeNodeViewModel
            {
                Icon = "—",
                Name = eLabel.Label,
                Value = $"({eLabel.Count} instances)"
            };

            // Connection info
            if (eLabel.SourceLabels.Count > 0 || eLabel.TargetLabels.Count > 0)
            {
                var connNode = new TreeNodeViewModel
                {
                    Icon = "??",
                    Name = "Connections",
                    Value = $"{string.Join(", ", eLabel.SourceLabels)} ? {string.Join(", ", eLabel.TargetLabels)}"
                };
                labelNode.Children.Add(connNode);
            }

            // Properties
            if (eLabel.Properties.Count > 0)
            {
                var propsNode = new TreeNodeViewModel
                {
                    Icon = "??",
                    Name = "Properties",
                    Value = $"({eLabel.Properties.Count})"
                };

                foreach (var prop in eLabel.Properties)
                {
                    propsNode.Children.Add(new TreeNodeViewModel
                    {
                        Icon = "•",
                        Name = prop.Name,
                        Value = $"{prop.CSharpType}"
                    });
                }

                labelNode.Children.Add(propsNode);
            }

            edgeNode.Children.Add(labelNode);
        }

        rootNode.Children.Add(edgeNode);

        SchemaTreeItems.Add(rootNode);
    }

    #endregion
}
