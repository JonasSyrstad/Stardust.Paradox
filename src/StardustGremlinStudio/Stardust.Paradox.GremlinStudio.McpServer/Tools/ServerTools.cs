using System.ComponentModel;
using ModelContextProtocol.Server;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.McpServer.Tools;

/// <summary>
/// MCP health-check tools used to verify that the server is reachable.
/// </summary>
[McpServerToolType]
public sealed class ServerTools
{
    private readonly IGremlinConnectionStore _connectionStore;

    public ServerTools(IGremlinConnectionStore connectionStore)
    {
        _connectionStore = connectionStore;
    }

    [McpServerTool(Name = "get_server_info"), Description(
        "Health check for the Gremlin Studio MCP server. " +
        "Returns server version, process id, start time, AppData path and number of saved connections. " +
        "Use this to distinguish 'server reachable' from 'tool failed'.")]
    public async Task<ServerInfo> GetServerInfo(CancellationToken cancellationToken = default)
    {
        var connections = await _connectionStore.GetAllConnectionsAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        return new ServerInfo
        {
            Version = ServerActivity.Version,
            ProcessId = Environment.ProcessId,
            StartedAt = ServerActivity.StartedAt,
            UptimeSeconds = (long)(now - ServerActivity.StartedAt).TotalSeconds,
            AppDataPath = Path.GetDirectoryName(AppDataPaths.ConnectionsFilePath) ?? string.Empty,
            ConnectionCount = connections.Count()
        };
    }
}

/// <summary>
/// Health information about the running MCP server process.
/// </summary>
public sealed class ServerInfo
{
    public string Version { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public long UptimeSeconds { get; set; }
    public string AppDataPath { get; set; } = string.Empty;
    public int ConnectionCount { get; set; }
}
