namespace Stardust.Paradox.GremlinStudio.Core.History;

/// <summary>
/// Service for managing query history with persistence.
/// </summary>
public interface IQueryHistoryService
{
    /// <summary>
    /// Gets the list of query history items, ordered by pinned first then by last executed.
    /// </summary>
    IReadOnlyList<QueryHistoryItem> GetHistory();

    /// <summary>
    /// Adds a query to the history. If it already exists, updates the last executed time.
    /// </summary>
    /// <param name="query">The query text to add.</param>
    void AddQuery(string query);

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
    /// Clears all non-pinned queries from history.
    /// </summary>
    void ClearUnpinned();

    /// <summary>
    /// Saves the history to persistent storage.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Loads the history from persistent storage.
    /// </summary>
    Task LoadAsync();
}
