namespace Stardust.Paradox.GremlinStudio.Core.Execution;

/// <summary>
/// Represents a single query execution entry in the session log.
/// </summary>
public sealed record ExecutionLogEntry
{
    /// <summary>
    /// The timestamp when the query was executed.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// The Gremlin query that was executed.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Whether the query executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// The number of results returned.
    /// </summary>
    public int ResultCount { get; init; }

    /// <summary>
    /// Request units consumed (for Cosmos DB). Null for non-Cosmos connections.
    /// </summary>
    public double? RequestUnits { get; init; }

    /// <summary>
    /// The error message if the query failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a log entry from a query execution result.
    /// </summary>
    public static ExecutionLogEntry FromResult(QueryExecutionResult result)
    {
        return new ExecutionLogEntry
        {
            Timestamp = DateTime.Now,
            Query = result.ExecutedQuery ?? string.Empty,
            IsSuccess = result.IsSuccess,
            Duration = result.Duration,
            ResultCount = result.ResultCount,
            RequestUnits = result.RequestUnits,
            ErrorMessage = result.ErrorMessage
        };
    }

    /// <summary>
    /// Creates a log entry for a cancelled query.
    /// </summary>
    public static ExecutionLogEntry Cancelled(string query, TimeSpan duration)
    {
        return new ExecutionLogEntry
        {
            Timestamp = DateTime.Now,
            Query = query,
            IsSuccess = false,
            Duration = duration,
            ErrorMessage = "Cancelled"
        };
    }

    /// <summary>
    /// Creates a log entry for a query that threw an exception.
    /// </summary>
    public static ExecutionLogEntry Error(string query, TimeSpan duration, string errorMessage)
    {
        return new ExecutionLogEntry
        {
            Timestamp = DateTime.Now,
            Query = query,
            IsSuccess = false,
            Duration = duration,
            ErrorMessage = errorMessage
        };
    }
}
