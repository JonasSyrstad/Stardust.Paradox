using System.ComponentModel;
using ModelContextProtocol.Server;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Execution;
using Stardust.Paradox.GremlinStudio.Core.Export;
using Stardust.Paradox.GremlinStudio.Core.Playground;

namespace Stardust.Paradox.GremlinStudio.McpServer.Tools;

/// <summary>
/// MCP tools for InMemory playground scenarios and data export.
/// </summary>
[McpServerToolType]
public sealed class ScenarioTools
{
    private readonly IPlaygroundService _playgroundService;
    private readonly IScenarioExportService _scenarioExportService;
    private readonly IGremlinConnectorFactory _connectorFactory;
    private readonly IGremlinConnectionStore _connectionStore;
    private readonly IGremlinQueryExecutor _queryExecutor;

    public ScenarioTools(
        IPlaygroundService playgroundService,
        IScenarioExportService scenarioExportService,
        IGremlinConnectorFactory connectorFactory,
        IGremlinConnectionStore connectionStore,
        IGremlinQueryExecutor queryExecutor)
    {
        _playgroundService = playgroundService;
        _scenarioExportService = scenarioExportService;
        _connectorFactory = connectorFactory;
        _connectionStore = connectionStore;
        _queryExecutor = queryExecutor;
    }

    [McpServerTool, Description(
        "Lists all available InMemory playground scenarios. " +
        "Scenarios provide pre-populated graph data for testing and exploration. " +
        "These can be loaded into InMemory connections or used as test fixtures.")]
    public IReadOnlyList<ScenarioSummary> ListScenarios()
    {
        var scenarios = _playgroundService.GetAvailableScenarios();
        return scenarios.Select(s => new ScenarioSummary
        {
            Name = s.Name,
            Description = s.Description
        }).ToList();
    }

    [McpServerTool, Description(
        "Exports graph data from a connection as a scenario file. " +
        "First runs a query to select vertices, then fetches related edges, " +
        "and exports the data as JSON (compatible with InMemory scenario loader) or C# code. " +
        "Use format 'Json' or 'CSharp'.")]
    public async Task<ScenarioExportResult> ExportScenario(
        [Description("The name or ID of the connection to export from.")] string connectionName,
        [Description("A Gremlin query that returns the vertices to include (e.g. 'g.V()' or 'g.V().hasLabel(\"person\")').")] string vertexQuery,
        [Description("Name for the exported scenario.")] string scenarioName,
        [Description("Export format: 'Json' or 'CSharp'. Default: 'Json'.")] string format = "Json",
        [Description("Optional description for the scenario.")] string? description = null)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return ScenarioExportResult.Error("Connection name is required.");
        }

        if (string.IsNullOrWhiteSpace(vertexQuery))
        {
            return ScenarioExportResult.Error("A vertex query is required.");
        }

        if (!Enum.TryParse<ScenarioExportFormat>(format, ignoreCase: true, out var exportFormat))
        {
            return ScenarioExportResult.Error($"Invalid format '{format}'. Use 'Json' or 'CSharp'.");
        }

        var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(false);
        var match = connections.FirstOrDefault(c =>
            c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase) ||
            c.Id.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var available = string.Join(", ", connections.Select(c => $"'{c.Name}'"));
            return ScenarioExportResult.Error(
                $"Connection '{connectionName}' not found. Available: {available}");
        }

        var settings = await _connectionStore.GetConnectionWithSecretAsync(match.Id).ConfigureAwait(false);
        if (settings is null)
        {
            return ScenarioExportResult.Error($"Could not load credentials for '{match.Name}'.");
        }

        var connector = _connectorFactory.CreateConnector(settings);
        try
        {
            // Execute the vertex query
            var result = await _queryExecutor.ExecuteAsync(connector, vertexQuery).ConfigureAwait(false);
            if (!result.IsSuccess || string.IsNullOrEmpty(result.ResultJson))
            {
                return ScenarioExportResult.Error($"Query failed: {result.ErrorMessage ?? "No results"}");
            }

            // Parse results into scenario data
            var scenarioData = _scenarioExportService.ParseQueryResults(
                result.ResultJson,
                scenarioName,
                description,
                match.Name,
                vertexQuery);

            // Fetch edges between the vertices
            scenarioData = await _scenarioExportService.FetchEdgesAsync(
                scenarioData, connector).ConfigureAwait(false);

            // Export to the requested format
            return new ScenarioExportResult
            {
                IsSuccess = true,
                Content = _scenarioExportService.Export(scenarioData, exportFormat),
                Format = exportFormat.ToString(),
                VertexCount = scenarioData.Vertices?.Count ?? 0,
                EdgeCount = scenarioData.Edges?.Count ?? 0
            };
        }
        catch (Exception ex)
        {
            return ScenarioExportResult.Error($"Scenario export failed: {ex.Message}");
        }
        finally
        {
            if (connector is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (connector is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

/// <summary>
/// Summary of an available InMemory scenario.
/// </summary>
public sealed class ScenarioSummary
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Result of a scenario export operation.
/// </summary>
public sealed class ScenarioExportResult
{
    public bool IsSuccess { get; set; }
    public string? Content { get; set; }
    public string? Format { get; set; }
    public int VertexCount { get; set; }
    public int EdgeCount { get; set; }
    public string? ErrorMessage { get; set; }

    public static ScenarioExportResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
