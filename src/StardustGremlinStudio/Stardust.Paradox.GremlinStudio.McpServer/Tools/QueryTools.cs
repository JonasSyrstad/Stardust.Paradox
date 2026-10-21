using System.ComponentModel;
using ModelContextProtocol.Server;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Execution;

namespace Stardust.Paradox.GremlinStudio.McpServer.Tools;

/// <summary>
/// MCP tools for executing Gremlin queries against saved connections.
/// </summary>
[McpServerToolType]
public sealed class QueryTools
{
    private readonly IGremlinConnectionStore _connectionStore;
    private readonly IGremlinConnectorFactory _connectorFactory;
    private readonly IGremlinQueryExecutor _queryExecutor;

    public QueryTools(
        IGremlinConnectionStore connectionStore,
        IGremlinConnectorFactory connectorFactory,
        IGremlinQueryExecutor queryExecutor)
    {
        _connectionStore = connectionStore;
        _connectorFactory = connectorFactory;
        _queryExecutor = queryExecutor;
    }

    [McpServerTool(Name = "execute_gremlin_query"), Description(
        "Executes a Gremlin query against a saved connection in Gremlin Studio. " +
        "Returns the query results as JSON along with execution metadata (duration, result count, RU cost for Cosmos DB). " +
        "Use 'list_connections' first to find available connection names.")]
    public async Task<QueryToolResult> ExecuteGremlinQuery(
        [Description("The name or ID of the saved connection to execute against.")] string connectionName,
        [Description("The Gremlin query to execute (e.g. 'g.V().limit(10)', 'g.V().hasLabel(\"person\").count()').")] string query)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return QueryToolResult.Error("Connection name is required.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return QueryToolResult.Error("Query is required.");
        }

        // Find the connection
        var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(false);
        var match = connections.FirstOrDefault(c =>
            c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase) ||
            c.Id.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var available = string.Join(", ", connections.Select(c => $"'{c.Name}'"));
            return QueryToolResult.Error(
                $"Connection '{connectionName}' not found. Available connections: {available}");
        }

        // Get connection with secret for authentication
        var settings = await _connectionStore.GetConnectionWithSecretAsync(match.Id).ConfigureAwait(false);
        if (settings is null)
        {
            return QueryToolResult.Error($"Could not load credentials for connection '{match.Name}'.");
        }

        // Create connector and execute
        var connector = _connectorFactory.CreateConnector(settings);
        try
        {
            var result = await _queryExecutor.ExecuteAsync(connector, query).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return QueryToolResult.Error(result.ErrorMessage ?? "Query execution failed.");
            }

            return new QueryToolResult
            {
                IsSuccess = true,
                ResultJson = result.ResultJson ?? "[]",
                ResultCount = result.ResultCount,
                DurationMs = (long)result.Duration.TotalMilliseconds,
                RequestUnits = result.RequestUnits,
                ExecutedQuery = result.ExecutedQuery ?? query
            };
        }
        catch (Exception ex)
        {
            return QueryToolResult.Error($"Query execution failed: {ex.Message}");
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
/// Result of a Gremlin query execution via MCP.
/// </summary>
public sealed class QueryToolResult
{
    public bool IsSuccess { get; set; }
    public string? ResultJson { get; set; }
    public int ResultCount { get; set; }
    public long DurationMs { get; set; }
    public double? RequestUnits { get; set; }
    public string? ExecutedQuery { get; set; }
    public string? ErrorMessage { get; set; }

    public static QueryToolResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
