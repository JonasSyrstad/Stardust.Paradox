namespace Stardust.Paradox.GremlinStudio.Core.History;

/// <summary>
/// Service for managing query history with persistence.
/// </summary>
public interface IQueryHistoryService
{
    /// <summary>
    /// Gets the list of query history items for a specific connection,
    /// ordered by pinned first then by last executed.
    /// </summary>
    /// <param name="connectionId">The connection to filter by, or null for all.</param>
    IReadOnlyList<QueryHistoryItem> GetHistory(string? connectionId = null);

    /// <summary>
    /// Adds a query to the history. If it already exists for the same connection, updates the last executed time.
    /// </summary>
    /// <param name="query">The query text to add.</param>
    /// <param name="connectionId">The connection this query was executed against.</param>
    void AddQuery(string query, string? connectionId = null);

    /// <summary>
    /// Pins or unpins a query in the history.
    /// </summary>
    /// <param name="id">The history item ID.</param>
    /// <param name="isPinned">Whether to pin or unpin.</param>
    void SetPinned(string id, bool isPinned);

    /// <summary>
    /// Removes a query from the history.
    /// </summary>
    /// <param name="id">The history item ID.</param>
    void RemoveQuery(string id);

    /// <summary>
    /// Clears all non-pinned queries from history for the given connection.
    /// </summary>
    /// <param name="connectionId">The connection to clear history for, or null for all.</param>
    void ClearUnpinned(string? connectionId = null);

    /// <summary>
    /// Saves the history to persistent storage.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Loads the history from persistent storage.
    /// </summary>
    Task LoadAsync();
}
