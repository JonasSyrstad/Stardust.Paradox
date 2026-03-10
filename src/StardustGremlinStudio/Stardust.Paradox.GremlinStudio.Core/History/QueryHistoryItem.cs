namespace Stardust.Paradox.GremlinStudio.Core.History;

/// <summary>
/// Represents a query in the history list.
/// </summary>
public class QueryHistoryItem
{
    /// <summary>
    /// Gets or sets the unique identifier for this history item.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the Gremlin query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection identifier this query was executed against.
    /// Null for queries executed before connection-scoped history was introduced,
    /// or for playground queries.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets when the query was last executed.
    /// </summary>
    public DateTime LastExecuted { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether this query is pinned to the top of the list.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Gets or sets an optional display name for pinned queries.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets a short preview of the query for display.
    /// </summary>
    public string Preview => Query.Length > 60 ? Query[..60] + "..." : Query;

    /// <summary>
    /// Gets the display text for the history item.
    /// </summary>
    public string DisplayText => !string.IsNullOrEmpty(DisplayName) ? DisplayName : Preview;
}
