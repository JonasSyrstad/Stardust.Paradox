using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Execution;

/// <summary>
/// Executes Gremlin queries and maintains an execution log.
/// </summary>
public sealed class GremlinQueryExecutor : IGremlinQueryExecutor
{
    private readonly ILogger<GremlinQueryExecutor> _logger;
    private readonly List<QueryLogEntry> _executionLog = new();
    private readonly object _logLock = new();
    private const int MaxLogEntries = 1000;

    public GremlinQueryExecutor(ILogger<GremlinQueryExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QueryExecutionResult> ExecuteAsync(
        IGremlinLanguageConnector connector,
        string query,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var queryParams = parameters is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(parameters);

        _logger.LogDebug("Executing query: {Query}", query);

        var stopwatch = Stopwatch.StartNew();
        string connectionName = "Unknown";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var results = await connector.ExecuteAsync(query, queryParams).ConfigureAwait(false);
            stopwatch.Stop();

            var resultList = results?.ToList() ?? new List<dynamic>();
            var resultJson = JsonConvert.SerializeObject(resultList, Formatting.Indented);

            var executionResult = QueryExecutionResult.Success(
                resultList,
                resultJson,
                stopwatch.Elapsed,
                query,
                connector.ConsumedRU > 0 ? connector.ConsumedRU : null);

            AddLogEntry(new QueryLogEntry(
                DateTimeOffset.UtcNow,
                connectionName,
                query,
                stopwatch.Elapsed,
                resultList.Count,
                true));

            _logger.LogInformation(
                "Query executed successfully in {Elapsed}ms, returned {Count} results",
                stopwatch.ElapsedMilliseconds,
                resultList.Count);

            return executionResult;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Query execution cancelled");

            AddLogEntry(new QueryLogEntry(
                DateTimeOffset.UtcNow,
                connectionName,
                query,
                stopwatch.Elapsed,
                0,
                false,
                "Cancelled"));

            return QueryExecutionResult.Failure(
                "Query execution was cancelled",
                null,
                stopwatch.Elapsed,
                query);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Query execution failed");

            AddLogEntry(new QueryLogEntry(
                DateTimeOffset.UtcNow,
                connectionName,
                query,
                stopwatch.Elapsed,
                0,
                false,
                ex.Message));

            return QueryExecutionResult.Failure(
                ex.Message,
                ex.ToString(),
                stopwatch.Elapsed,
                query);
        }
    }

    public IReadOnlyList<QueryLogEntry> GetExecutionLog()
    {
        lock (_logLock)
        {
            return _executionLog.ToList();
        }
    }

    public void ClearExecutionLog()
    {
        lock (_logLock)
        {
            _executionLog.Clear();
        }
    }

    private void AddLogEntry(QueryLogEntry entry)
    {
        lock (_logLock)
        {
            _executionLog.Add(entry);

            // Trim log if it exceeds max size
            if (_executionLog.Count > MaxLogEntries)
            {
                _executionLog.RemoveRange(0, _executionLog.Count - MaxLogEntries);
            }
        }
    }
}
