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
}
