namespace Stardust.Paradox.GremlinStudio.Core.McpInstall;

/// <summary>
/// Target scope for MCP server installation.
/// </summary>
public enum McpInstallTarget
{
    /// <summary>
    /// Install into a specific solution or repository folder.
    /// </summary>
    SolutionOrRepo,

    /// <summary>
    /// Install globally for Visual Studio.
    /// </summary>
    VisualStudioGlobal,

    /// <summary>
    /// Install globally for Visual Studio Code.
    /// </summary>
    VsCodeGlobal
}

/// <summary>
/// Result of an MCP server installation operation.
/// </summary>
public sealed record McpInstallResult(
    bool IsSuccess,
    string ConfigFilePath,
    string? ErrorMessage = null);

/// <summary>
/// Service for installing the Gremlin Studio MCP server configuration into
/// Visual Studio, VS Code, or a specific repository.
/// </summary>
public interface IMcpServerInstallService
{
    /// <summary>
    /// Resolves the path to the bundled MCP server executable.
    /// Returns null if the executable is not found alongside the running application.
    /// </summary>
    string? ResolveMcpServerExePath();

    /// <summary>
    /// Installs the MCP server configuration for a specific solution or repository folder.
    /// Creates <c>.mcp.json</c> for Visual Studio and <c>.vscode/mcp.json</c> for VS Code
    /// at the repository root.
    /// </summary>
    /// <param name="targetDirectory">The solution or repository directory.</param>
    /// <returns>The installation result.</returns>
    McpInstallResult InstallForSolutionOrRepo(string targetDirectory);

    /// <summary>
    /// Installs the MCP server configuration globally for Visual Studio.
    /// Writes to <c>%USERPROFILE%/.mcp.json</c>.
    /// </summary>
    /// <returns>The installation result.</returns>
    McpInstallResult InstallForVisualStudioGlobal();

    /// <summary>
    /// Installs the MCP server configuration globally for Visual Studio Code.
    /// Writes to <c>%APPDATA%/Code/User/settings.json</c> (mcp servers section).
    /// </summary>
    /// <returns>The installation result.</returns>
    McpInstallResult InstallForVsCodeGlobal();
}
