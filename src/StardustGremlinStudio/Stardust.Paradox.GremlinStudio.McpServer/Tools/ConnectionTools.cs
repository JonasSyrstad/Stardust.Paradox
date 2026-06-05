using System.ComponentModel;
using ModelContextProtocol.Server;
using Stardust.Paradox.GremlinStudio.Core.Connections;

namespace Stardust.Paradox.GremlinStudio.McpServer.Tools;

/// <summary>
/// MCP tools for managing and inspecting Gremlin connections.
/// </summary>
[McpServerToolType]
public sealed class ConnectionTools
{
    private readonly IGremlinConnectionStore _connectionStore;

    public ConnectionTools(IGremlinConnectionStore connectionStore)
    {
        _connectionStore = connectionStore;
    }

    [McpServerTool(Name = "list_connections"), Description(
        "Lists all saved Gremlin database connections in Gremlin Studio. " +
        "Returns connection names, types (CosmosDb/GremlinServer/InMemory), hosts, and identifiers.")]
    public async Task<IReadOnlyList<ConnectionSummary>> ListConnections()
    {
        var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(false);
        return connections.Select(c => new ConnectionSummary
        {
            Id = c.Id,
            Name = c.Name,
            Kind = c.Kind.ToString(),
            Host = c.Host,
            Port = c.Port,
            DatabaseName = c.DatabaseName,
            GraphName = c.GraphName,
            EnableSsl = c.EnableSsl,
            ScenarioName = c.ScenarioName,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    [McpServerTool(Name = "get_connection_details"), Description(
        "Gets detailed information about a specific Gremlin connection by its name or ID. " +
        "Returns full connection metadata including host, port, database, graph, SSL, and pool settings.")]
    public async Task<ConnectionDetails?> GetConnectionDetails(
        [Description("The connection name or ID to look up.")] string nameOrId)
    {
        var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(false);
        var match = connections.FirstOrDefault(c =>
            c.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
            c.Id.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return null;
        }

        return new ConnectionDetails
        {
            Id = match.Id,
            Name = match.Name,
            Kind = match.Kind.ToString(),
            Host = match.Host,
            Port = match.Port,
            Username = match.Username,
            DatabaseName = match.DatabaseName,
            GraphName = match.GraphName,
            PartitionKey = match.PartitionKey,
            EnableSsl = match.EnableSsl,
            PoolSize = match.PoolSize,
            MaxConnectionPoolSize = match.MaxConnectionPoolSize,
            ConnectionTimeoutSeconds = match.ConnectionTimeoutSeconds,
            ScenarioName = match.ScenarioName,
            CreatedAt = match.CreatedAt,
            LastModifiedAt = match.LastModifiedAt
        };
    }
}

/// <summary>
/// Summary representation of a connection for list operations.
/// </summary>
public sealed class ConnectionSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? GraphName { get; set; }
    public bool EnableSsl { get; set; }
    public string? ScenarioName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Detailed representation of a connection.
/// </summary>
public sealed class ConnectionDetails
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DatabaseName { get; set; }
    public string? GraphName { get; set; }
    public string? PartitionKey { get; set; }
    public bool EnableSsl { get; set; }
    public int PoolSize { get; set; }
    public int MaxConnectionPoolSize { get; set; }
    public int ConnectionTimeoutSeconds { get; set; }
    public string? ScenarioName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
}
