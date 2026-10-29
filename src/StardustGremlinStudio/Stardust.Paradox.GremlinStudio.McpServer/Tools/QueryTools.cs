using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Execution;
using Stardust.Paradox.GremlinStudio.Core.Export;

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
        [Description("The Gremlin query to execute (e.g. 'g.V().limit(10)', 'g.V().hasLabel(\"person\").count()').")] string query,
        CancellationToken cancellationToken = default)
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
        var connections = await _connectionStore.GetAllConnectionsAsync(cancellationToken).ConfigureAwait(false);
        var match = connections.FirstOrDefault(c =>
            c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase) ||
            c.Id.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var available = string.Join(", ", connections.Select(c => $"'{c.Name}'"));
            throw new McpException($"Connection '{connectionName}' not found. Available: {available}");
        }

        // Get connection with secret for authentication
        var settings = await _connectionStore.GetConnectionWithSecretAsync(match.Id, cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            return QueryToolResult.Error($"Could not load credentials for connection '{match.Name}'.");
        }

        // Create connector and execute
        var connector = _connectorFactory.CreateConnector(settings);
        try
        {
            var result = await _queryExecutor.ExecuteAsync(connector, query, cancellationToken: cancellationToken).ConfigureAwait(false);

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
        catch (OperationCanceledException)
        {
            throw;
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

    [McpServerTool(Name = "export_query_results"), Description(
        "Executes a Gremlin query against a saved connection and exports the results to a CSV or Excel (.xlsx) file, " +
        "using the same flattened table layout as the Table view in Gremlin Studio's results pane " +
        "(one row per element, vertex/edge properties as columns, id/name/pk first). " +
        "Returns the written file path, columns and row count.")]
    public async Task<ExportToolResult> ExportQueryResults(
        [Description("The name or ID of the saved connection to execute against.")] string connectionName,
        [Description("The Gremlin query whose results should be exported (e.g. 'g.V().hasLabel(\"person\")').")] string query,
        [Description("Export format: 'csv' or 'xlsx' (alias 'excel'). Default: 'csv'.")] string format = "csv",
        [Description("Optional full output file path. If omitted, a timestamped file is created in Documents\\GremlinStudio\\Exports. The extension is added if missing.")] string? outputPath = null,
        [Description("XLSX only: convert epoch timestamps in date-like columns (e.g. 'createdAt') to Excel date/time values. Default: false.")] bool convertEpochDates = false,
        CancellationToken cancellationToken = default)
    {
        var extension = (format ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "" or "csv" => ".csv",
            "xlsx" or "excel" => ".xlsx",
            _ => null
        };
        if (extension is null)
        {
            return ExportToolResult.Error($"Unsupported format '{format}'. Use 'csv' or 'xlsx'.");
        }

        var queryResult = await ExecuteGremlinQuery(connectionName, query, cancellationToken).ConfigureAwait(false);
        if (!queryResult.IsSuccess)
        {
            return ExportToolResult.Error(queryResult.ErrorMessage ?? "Query execution failed.");
        }

        ResultTableData table;
        try
        {
            table = ResultTableExporter.BuildTable(queryResult.ResultJson ?? "[]");
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            return ExportToolResult.Error($"Could not parse query results: {ex.Message}");
        }

        if (table.Rows.Count == 0)
        {
            return ExportToolResult.Error(
                $"Query returned {queryResult.ResultCount} result(s) but none are vertices, edges or maps that can be shown as table rows.");
        }

        string filePath;
        try
        {
            filePath = ResolveOutputPath(outputPath, extension);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            if (extension == ".csv")
            {
                // UTF-8 with BOM so Excel detects the encoding when opening the CSV.
                await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(true));
                await writer.WriteAsync(ResultTableExporter.ToCsv(table).AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                ResultTableExporter.WriteXlsx(stream, table, convertEpochDates);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ExportToolResult.Error($"Could not write export file: {ex.Message}");
        }

        return new ExportToolResult
        {
            IsSuccess = true,
            FilePath = filePath,
            Format = extension.TrimStart('.'),
            RowCount = table.Rows.Count,
            Columns = table.Columns.ToList(),
            DurationMs = queryResult.DurationMs,
            RequestUnits = queryResult.RequestUnits
        };
    }

    private static string ResolveOutputPath(string? outputPath, string extension)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GremlinStudio", "Exports");
            return Path.Combine(directory, $"query-results-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
        }

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(outputPath.Trim()));
        return Path.GetExtension(fullPath).Equals(extension, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath + extension;
    }
}

/// <summary>
/// Result of a query-results export via MCP.
/// </summary>
public sealed class ExportToolResult
{
    public bool IsSuccess { get; set; }
    public string? FilePath { get; set; }
    public string? Format { get; set; }
    public int RowCount { get; set; }
    public List<string> Columns { get; set; } = new();
    public long DurationMs { get; set; }
    public double? RequestUnits { get; set; }
    public string? ErrorMessage { get; set; }

    public static ExportToolResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
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
