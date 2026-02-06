namespace Stardust.Paradox.GremlinStudio.Core.Execution;

/// <summary>
/// Represents the result of a Gremlin query execution.
/// </summary>
public sealed class QueryExecutionResult
{
    /// <summary>
    /// Whether the query executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The raw results from the query.
    /// </summary>
    public IReadOnlyList<dynamic>? Results { get; init; }

    /// <summary>
    /// The JSON representation of the results.
    /// </summary>
    public string? ResultJson { get; init; }

    /// <summary>
    /// The number of results returned.
    /// </summary>
    public int ResultCount { get; init; }

    /// <summary>
    /// The execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// The error message if the query failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The detailed error information if the query failed.
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// The query that was executed.
    /// </summary>
    public string? ExecutedQuery { get; init; }

    /// <summary>
    /// Request units consumed (for Cosmos DB).
    /// </summary>
    public double? RequestUnits { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static QueryExecutionResult Success(
        IReadOnlyList<dynamic> results,
        string resultJson,
        TimeSpan duration,
        string executedQuery,
        double? requestUnits = null)
    {
        return new QueryExecutionResult
        {
            IsSuccess = true,
            Results = results,
            ResultJson = resultJson,
            ResultCount = results.Count,
            Duration = duration,
            ExecutedQuery = executedQuery,
            RequestUnits = requestUnits
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static QueryExecutionResult Failure(
        string errorMessage,
        string? errorDetails,
        TimeSpan duration,
        string executedQuery)
    {
        return new QueryExecutionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorDetails = errorDetails,
            Duration = duration,
            ExecutedQuery = executedQuery
        };
    }
}
