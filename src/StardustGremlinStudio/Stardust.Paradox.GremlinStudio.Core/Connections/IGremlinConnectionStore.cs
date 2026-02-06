namespace Stardust.Paradox.GremlinStudio.Core.Connections;

/// <summary>
/// Interface for managing Gremlin connection storage.
/// Handles persistence of connection metadata and secure secret storage.
/// </summary>
public interface IGremlinConnectionStore
{
    /// <summary>
    /// Gets all stored connection metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of connection metadata.</returns>
    Task<IReadOnlyList<GremlinConnectionMetadata>> GetAllConnectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific connection by ID.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection metadata, or null if not found.</returns>
    Task<GremlinConnectionMetadata?> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a connection with its secret (full settings).
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full connection settings, or null if not found.</returns>
    Task<GremlinConnectionSettings?> GetConnectionWithSecretAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a connection (creates or updates).
    /// </summary>
    /// <param name="settings">The connection settings to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveConnectionAsync(GremlinConnectionSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a connection by ID.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the connection was deleted, false if not found.</returns>
    Task<bool> DeleteConnectionAsync(string connectionId, CancellationToken cancellationToken = default);
}
