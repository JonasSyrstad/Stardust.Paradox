using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Stardust.Paradox.GremlinStudio.Core.McpInstall;

/// <summary>
/// Installs the Gremlin Studio MCP server configuration into Visual Studio,
/// VS Code, or a specific repository by generating the appropriate mcp.json files.
/// </summary>
public sealed class McpServerInstallService : IMcpServerInstallService
{
    private const string McpServerExeName = "GremlinStudioMcp.exe";
    private const string McpServerId = "GremlinStudio";

    private readonly ILogger<McpServerInstallService> _logger;

    public McpServerInstallService(ILogger<McpServerInstallService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string? ResolveMcpServerExePath()
    {
        // The MCP server exe is expected alongside the running application
        var appDir = AppContext.BaseDirectory;
        var mcpExePath = Path.Combine(appDir, McpServerExeName);

        if (File.Exists(mcpExePath))
        {
            return mcpExePath;
        }

        _logger.LogWarning("MCP server executable not found at {Path}", mcpExePath);
        return null;
    }

    /// <inheritdoc />
    public McpInstallResult InstallForSolutionOrRepo(string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        var mcpExePath = ResolveMcpServerExePath();
        if (mcpExePath is null)
        {
            return new McpInstallResult(false, string.Empty,
                $"MCP server executable ({McpServerExeName}) not found alongside GremlinStudio.");
        }

        try
        {
            // Write .mcp.json for Visual Studio (at repo root)
            var vsConfigPath = Path.Combine(targetDirectory, ".mcp.json");
            var vsContent = BuildMcpJson(mcpExePath);
            File.WriteAllText(vsConfigPath, vsContent);

            // Also write .vscode/mcp.json for VS Code
            var vscodeDir = Path.Combine(targetDirectory, ".vscode");
            Directory.CreateDirectory(vscodeDir);
            var vscodeConfigPath = Path.Combine(vscodeDir, "mcp.json");
            var vscodeContent = BuildMcpJson(mcpExePath);
            File.WriteAllText(vscodeConfigPath, vscodeContent);

            _logger.LogInformation("MCP server installed for repo at {Directory}", targetDirectory);
            return new McpInstallResult(true, vsConfigPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install MCP server for repo at {Directory}", targetDirectory);
            return new McpInstallResult(false, string.Empty, ex.Message);
        }
    }

    /// <inheritdoc />
    public McpInstallResult InstallForVisualStudioGlobal()
    {
        var mcpExePath = ResolveMcpServerExePath();
        if (mcpExePath is null)
        {
            return new McpInstallResult(false, string.Empty,
                $"MCP server executable ({McpServerExeName}) not found alongside GremlinStudio.");
        }

        try
        {
            // Visual Studio global: %USERPROFILE%/.mcp.json
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configPath = Path.Combine(userProfile, ".mcp.json");

            var content = MergeIntoExistingMcpJson(configPath, mcpExePath);
            File.WriteAllText(configPath, content);

            _logger.LogInformation("MCP server installed globally for Visual Studio at {Path}", configPath);
            return new McpInstallResult(true, configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install MCP server globally for Visual Studio");
            return new McpInstallResult(false, string.Empty, ex.Message);
        }
    }

    /// <inheritdoc />
    public McpInstallResult InstallForVsCodeGlobal()
    {
        var mcpExePath = ResolveMcpServerExePath();
        if (mcpExePath is null)
        {
            return new McpInstallResult(false, string.Empty,
                $"MCP server executable ({McpServerExeName}) not found alongside GremlinStudio.");
        }

        try
        {
            // VS Code global: %APPDATA%/Code/User/settings.json
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var vscodeUserDir = Path.Combine(appData, "Code", "User");
            Directory.CreateDirectory(vscodeUserDir);

            var settingsPath = Path.Combine(vscodeUserDir, "settings.json");

            var settings = ReadOrCreateJsonObject(settingsPath);

            // Ensure mcp.servers section exists
            if (settings["mcp.servers"] is not JsonObject mcpServers)
            {
                mcpServers = new JsonObject();
                settings["mcp.servers"] = mcpServers;
            }

            // Add or update GremlinStudio entry
            mcpServers[McpServerId] = BuildServerNode(mcpExePath);

            var json = settings.ToJsonString(SerializerOptions);
            File.WriteAllText(settingsPath, json);

            _logger.LogInformation("MCP server installed globally for VS Code at {Path}", settingsPath);
            return new McpInstallResult(true, settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install MCP server globally for VS Code");
            return new McpInstallResult(false, string.Empty, ex.Message);
        }
    }

    private static string BuildMcpJson(string mcpExePath)
    {
        var root = new JsonObject
        {
            ["inputs"] = new JsonArray(),
            ["servers"] = new JsonObject
            {
                [McpServerId] = BuildServerNode(mcpExePath)
            }
        };

        return root.ToJsonString(SerializerOptions);
    }

    private static JsonObject BuildServerNode(string mcpExePath)
    {
        return new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = mcpExePath.Replace('\\', '/')
        };
    }

    /// <summary>
    /// Merges GremlinStudio server entry into an existing .mcp.json file,
    /// preserving other server entries.
    /// </summary>
    private static string MergeIntoExistingMcpJson(string configPath, string mcpExePath)
    {
        var root = ReadOrCreateJsonObject(configPath);

        if (root["servers"] is not JsonObject servers)
        {
            servers = new JsonObject();
            root["servers"] = servers;
        }

        // Add or overwrite GremlinStudio entry
        servers[McpServerId] = BuildServerNode(mcpExePath);

        if (root["inputs"] is null)
        {
            root["inputs"] = new JsonArray();
        }

        return root.ToJsonString(SerializerOptions);
    }

    private static JsonObject ReadOrCreateJsonObject(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                var text = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
                }
            }
            catch
            {
                // File is corrupt or unreadable; start fresh
            }
        }

        return new JsonObject();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };
}
