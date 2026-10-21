using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Execution;

/// <summary>
/// Executes Gremlin queries and maintains an execution log.
/// Includes retry logic for transient failures.
/// </summary>
public sealed class GremlinQueryExecutor : IGremlinQueryExecutor
{
    private readonly ILogger<GremlinQueryExecutor> _logger;
    private readonly List<QueryLogEntry> _executionLog = new();
    private readonly object _logLock = new();
    private const int MaxLogEntries = 1000;
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) };

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
        Exception? lastException = null;

        // Capture cumulative RU before the query so we can compute per-query cost
        double ruBefore = connector.ConsumedRU;

        for (int attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 0)
                {
                    _logger.LogInformation("Retry attempt {Attempt} of {MaxAttempts} for query", attempt, MaxRetryAttempts);
                    await Task.Delay(RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                    ruBefore = connector.ConsumedRU;
                }

                var results = await connector.ExecuteAsync(query, queryParams).ConfigureAwait(false);
                stopwatch.Stop();

                var resultList = results?.ToList() ?? new List<dynamic>();
                var resultJson = JsonConvert.SerializeObject(resultList, Formatting.Indented);

                double queryRU = connector.ConsumedRU - ruBefore;
                var executionResult = QueryExecutionResult.Success(
                    resultList,
                    resultJson,
                    stopwatch.Elapsed,
                    query,
                    queryRU > 0 ? queryRU : null);

                AddLogEntry(new QueryLogEntry(
                    DateTimeOffset.UtcNow,
                    connectionName,
                    query,
                    stopwatch.Elapsed,
                    resultList.Count,
                    true,
                    attempt > 0 ? $"Succeeded after {attempt} retries" : null));

                _logger.LogInformation(
                    "Query executed successfully in {Elapsed}ms, returned {Count} results{RetryInfo}",
                    stopwatch.ElapsedMilliseconds,
                    resultList.Count,
                    attempt > 0 ? $" (after {attempt} retries)" : "");

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
            catch (Exception ex) when (IsTransientException(ex) && attempt < MaxRetryAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Transient error on attempt {Attempt}, will retry", attempt + 1);
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

        // All retries exhausted
        stopwatch.Stop();
        var finalMessage = $"Query failed after {MaxRetryAttempts} retries: {lastException?.Message}";
        _logger.LogError(lastException, finalMessage);

        AddLogEntry(new QueryLogEntry(
            DateTimeOffset.UtcNow,
            connectionName,
            query,
            stopwatch.Elapsed,
            0,
            false,
            finalMessage));

        return QueryExecutionResult.Failure(
            finalMessage,
            lastException?.ToString(),
            stopwatch.Elapsed,
            query);
    }

    /// <summary>
    /// Determines if an exception is transient and can be retried.
    /// </summary>
    private static bool IsTransientException(Exception ex)
    {
        // Socket errors, timeouts, and specific Gremlin server errors
        return ex is SocketException
            || ex is TimeoutException
            || ex is IOException
            || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("429", StringComparison.Ordinal) // Rate limiting
            || ex.Message.Contains("503", StringComparison.Ordinal) // Service unavailable
            || (ex.InnerException != null && IsTransientException(ex.InnerException));
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
