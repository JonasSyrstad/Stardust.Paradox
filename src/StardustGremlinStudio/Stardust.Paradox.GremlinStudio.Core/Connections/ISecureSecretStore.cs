namespace Stardust.Paradox.GremlinStudio.Core.Connections;

/// <summary>
/// Interface for secure secret storage operations.
/// Implementations use platform-specific secure storage (e.g., Windows DPAPI).
/// </summary>
public interface ISecureSecretStore
{
    /// <summary>
    /// Stores a secret securely, associated with a key.
    /// </summary>
    /// <param name="key">The unique key to identify the secret.</param>
    /// <param name="secret">The secret value to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreSecretAsync(string key, string secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a secret by its key.
    /// </summary>
    /// <param name="key">The unique key to identify the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret by its key.
    /// </summary>
    /// <param name="key">The unique key to identify the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the secret was deleted, false if not found.</returns>
    Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret exists for the given key.
    /// </summary>
    /// <param name="key">The unique key to identify the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the secret exists.</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
