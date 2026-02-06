namespace Stardust.Paradox.GremlinStudio.Core.Execution;

/// <summary>
/// Represents an entry in the query execution log.
/// </summary>
public sealed record QueryLogEntry(
    DateTimeOffset ExecutedAt,
    string ConnectionName,
    string Query,
    TimeSpan Duration,
    int ResultCount,
    bool IsSuccess,
    string? ErrorMessage = null);
