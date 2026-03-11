namespace Stardust.Paradox.GremlinStudio.Core.Connections;

/// <summary>
/// Service for discovering databases and graphs in Cosmos DB.
/// </summary>
public interface ICosmosDbDiscoveryService
{
    /// <summary>
    /// Discovers available databases in a Cosmos DB account.
    /// </summary>
    Task<IReadOnlyList<string>> DiscoverDatabasesAsync(string endpoint, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers available graphs (containers) in a Cosmos DB database.
    /// </summary>
    Task<IReadOnlyList<string>> DiscoverGraphsAsync(string endpoint, string key, string database, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers all labels and their counts for vertices or edges using the SQL API.
    /// Runs a <c>GROUP BY</c> query per physical partition (which the gateway supports
    /// for single-partition scope) and merges results client-side. No Gremlin required.
    /// </summary>
    /// <param name="endpoint">The Cosmos DB documents endpoint (https://xxx.documents.azure.com).</param>
    /// <param name="key">The account access key.</param>
    /// <param name="database">The database name.</param>
    /// <param name="container">The container (graph) name.</param>
    /// <param name="isEdge">True for edge labels, false for vertex labels.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping each label to its total count across all partitions.</returns>
    Task<IReadOnlyDictionary<string, long>> DiscoverLabelCountsAsync(
        string endpoint,
        string key,
        string database,
        string container,
        bool isEdge,
        CancellationToken cancellationToken = default);
}
