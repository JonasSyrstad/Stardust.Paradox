namespace Stardust.Paradox.GremlinStudio.Core.Connections;

/// <summary>
/// Represents a complete Gremlin connection with metadata and secret.
/// Used for in-memory operations; secrets should never be persisted in plaintext.
/// </summary>
public sealed class GremlinConnectionSettings
{
    /// <summary>
    /// The connection metadata (safe to persist).
    /// </summary>
    public GremlinConnectionMetadata Metadata { get; }

    /// <summary>
    /// The secret (password/access key). Never persist in plaintext.
    /// </summary>
    public string Secret { get; }

    public GremlinConnectionSettings(GremlinConnectionMetadata metadata, string secret)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
        Secret = secret ?? string.Empty;
    }

    /// <summary>
    /// Gets the friendly name.
    /// </summary>
    public string Name => Metadata.Name;

    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public string Id => Metadata.Id;

    /// <summary>
    /// Gets the connection kind.
    /// </summary>
    public GremlinConnectionKind Kind => Metadata.Kind;

    /// <summary>
    /// Gets the host.
    /// </summary>
    public string Host => Metadata.Host;

    /// <summary>
    /// Gets the port.
    /// </summary>
    public int Port => Metadata.Port;

    /// <summary>
    /// Gets the username.
    /// </summary>
    public string Username => Metadata.Username;

    /// <summary>
    /// Gets whether SSL is enabled.
    /// </summary>
    public bool EnableSsl => Metadata.EnableSsl;

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string? DatabaseName => Metadata.DatabaseName;

    /// <summary>
    /// Gets the graph name.
    /// </summary>
    public string? GraphName => Metadata.GraphName;
}
