using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Schema;

/// <summary>
/// Service for discovering graph schema from a Gremlin database.
/// </summary>
public interface ISchemaDiscoveryService
{
    /// <summary>
    /// Discovers the complete graph schema including vertex labels, edge labels, and their properties.
    /// </summary>
    /// <param name="connector">The Gremlin connector to use for querying.</param>
    /// <param name="sampleSize">Maximum number of vertices/edges to sample per label for property discovery.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovered graph schema.</returns>
    Task<GraphSchema> DiscoverSchemaAsync(
        IGremlinLanguageConnector connector,
        int sampleSize = 100,
        IProgress<SchemaDiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers only vertex labels without property details.
    /// </summary>
    Task<List<string>> DiscoverVertexLabelsAsync(
        IGremlinLanguageConnector connector,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers only edge labels without property details.
    /// </summary>
    Task<List<string>> DiscoverEdgeLabelsAsync(
        IGremlinLanguageConnector connector,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Progress information for schema discovery operations.
/// </summary>
public class SchemaDiscoveryProgress
{
    /// <summary>
    /// Current step name (e.g., "Discovering vertex labels", "Analyzing 'person' properties").
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Detailed status message.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    /// Current item being processed.
    /// </summary>
    public int CurrentItem { get; set; }

    /// <summary>
    /// Total items to process.
    /// </summary>
    public int TotalItems { get; set; }
}
