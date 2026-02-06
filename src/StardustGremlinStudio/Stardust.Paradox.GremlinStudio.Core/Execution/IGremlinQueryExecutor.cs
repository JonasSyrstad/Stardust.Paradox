using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Execution;

/// <summary>
/// Interface for executing Gremlin queries.
/// </summary>
public interface IGremlinQueryExecutor
{
    /// <summary>
    /// Executes a Gremlin query against the specified connector.
    /// </summary>
    /// <param name="connector">The Gremlin language connector.</param>
    /// <param name="query">The Gremlin query to execute.</param>
    /// <param name="parameters">Query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The query execution result.</returns>
    Task<QueryExecutionResult> ExecuteAsync(
        IGremlinLanguageConnector connector,
        string query,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the execution log.
    /// </summary>
    IReadOnlyList<QueryLogEntry> GetExecutionLog();

    /// <summary>
    /// Clears the execution log.
    /// </summary>
    void ClearExecutionLog();
}
