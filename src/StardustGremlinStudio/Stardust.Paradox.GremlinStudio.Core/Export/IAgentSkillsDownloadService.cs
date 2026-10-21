namespace Stardust.Paradox.GremlinStudio.Core.Export;

/// <summary>
/// Result of an agent skills download operation.
/// </summary>
public sealed record AgentSkillsDownloadResult(
    bool IsSuccess,
    int FileCount,
    string TargetDirectory,
    string? ErrorMessage = null);

/// <summary>
/// Result of resolving the skills directory within a git repository.
/// </summary>
public sealed record SkillsDirectoryResolution(
    bool IsGitRepo,
    string? SkillsDirectory,
    string? AgentConfigFolder,
    string? ErrorMessage = null);

/// <summary>
/// Service for downloading Stardust.Paradox agent skills from the GitHub repository.
/// </summary>
public interface IAgentSkillsDownloadService
{
    /// <summary>
    /// Resolves the correct skills directory for a given folder by verifying it is
    /// inside a git repository and detecting whether <c>.github</c> or <c>.claude</c>
    /// directories exist. Skills are placed under <c>.github/skills</c> or
    /// <c>.claude/skills</c> per the GitHub Copilot agent skills spec.
    /// </summary>
    /// <param name="selectedDirectory">The directory selected by the user.</param>
    /// <returns>The resolved skills directory information.</returns>
    SkillsDirectoryResolution ResolveSkillsDirectory(string selectedDirectory);

    /// <summary>
    /// Downloads the agent skills directory from the repository into the specified local folder.
    /// </summary>
    /// <param name="targetDirectory">The local directory to write skill files into.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The download result.</returns>
    Task<AgentSkillsDownloadResult> DownloadSkillsAsync(
        string targetDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
