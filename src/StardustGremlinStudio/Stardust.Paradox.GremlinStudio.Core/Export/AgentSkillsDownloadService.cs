using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.GremlinStudio.Core.Export;

/// <summary>
/// Downloads Stardust.Paradox agent skills from the GitHub repository
/// using the GitHub Contents API (no authentication required for public repos).
/// Places skills under <c>.github/skills</c> or <c>.claude/skills</c> per the
/// GitHub Copilot agent skills specification.
/// </summary>
public sealed class AgentSkillsDownloadService : IAgentSkillsDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentSkillsDownloadService> _logger;

    private const string GitHubApiBase = "https://api.github.com/repos/JonasSyrstad/Stardust.Paradox/contents";
    private const string SkillsPath = "skills";
    private const string DefaultBranch = "V2.6";

    public AgentSkillsDownloadService(HttpClient httpClient, ILogger<AgentSkillsDownloadService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // GitHub API requires a User-Agent header
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GremlinStudio/1.0");
        }
    }

    /// <inheritdoc />
    public SkillsDirectoryResolution ResolveSkillsDirectory(string selectedDirectory)
    {
        ArgumentNullException.ThrowIfNull(selectedDirectory);

        var repoRoot = FindGitRepoRoot(selectedDirectory);
        if (repoRoot is null)
        {
            _logger.LogWarning("Selected directory {Dir} is not inside a git repository", selectedDirectory);
            return new SkillsDirectoryResolution(
                IsGitRepo: false,
                SkillsDirectory: null,
                AgentConfigFolder: null,
                ErrorMessage: "The selected folder is not inside a git repository. Please select a folder within a git repository.");
        }

        _logger.LogInformation("Found git repository root at {Root}", repoRoot);

        // Detect existing agent config directories per the GitHub Copilot spec
        var githubDir = Path.Combine(repoRoot, ".github");
        var claudeDir = Path.Combine(repoRoot, ".claude");

        var hasGitHub = Directory.Exists(githubDir);
        var hasClaude = Directory.Exists(claudeDir);

        string agentConfigFolder;
        string skillsDirectory;

        if (hasGitHub)
        {
            agentConfigFolder = ".github";
            skillsDirectory = Path.Combine(githubDir, "skills");
            _logger.LogInformation("Using existing .github directory for skills at {Path}", skillsDirectory);
        }
        else if (hasClaude)
        {
            agentConfigFolder = ".claude";
            skillsDirectory = Path.Combine(claudeDir, "skills");
            _logger.LogInformation("Using existing .claude directory for skills at {Path}", skillsDirectory);
        }
        else
        {
            // Default to .github per GitHub Copilot convention
            agentConfigFolder = ".github";
            skillsDirectory = Path.Combine(githubDir, "skills");
            _logger.LogInformation("No agent config directory found, will create .github/skills at {Path}", skillsDirectory);
        }

        return new SkillsDirectoryResolution(
            IsGitRepo: true,
            SkillsDirectory: skillsDirectory,
            AgentConfigFolder: agentConfigFolder);
    }

    /// <inheritdoc />
    public async Task<AgentSkillsDownloadResult> DownloadSkillsAsync(
        string targetDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetDirectory);

        try
        {
            progress?.Report("Connecting to GitHub repository...");
            _logger.LogInformation("Downloading agent skills to {Target}", targetDirectory);

            Directory.CreateDirectory(targetDirectory);

            var fileCount = await DownloadDirectoryAsync(
                SkillsPath,
                targetDirectory,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (fileCount == 0)
            {
                _logger.LogWarning("No skill files found in repository");
                return new AgentSkillsDownloadResult(false, 0, targetDirectory, "No skill files found in the repository.");
            }

            progress?.Report($"Downloaded {fileCount} file(s) successfully.");
            _logger.LogInformation("Downloaded {Count} skill files to {Target}", fileCount, targetDirectory);

            return new AgentSkillsDownloadResult(true, fileCount, targetDirectory);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Agent skills download was cancelled");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download agent skills from GitHub");
            return new AgentSkillsDownloadResult(false, 0, targetDirectory, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading agent skills");
            return new AgentSkillsDownloadResult(false, 0, targetDirectory, ex.Message);
        }
    }

    /// <summary>
    /// Recursively downloads a directory from GitHub Contents API.
    /// </summary>
    private async Task<int> DownloadDirectoryAsync(
        string repoPath,
        string localDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var url = $"{GitHubApiBase}/{repoPath}?ref={DefaultBranch}";
        _logger.LogDebug("Listing: {Url}", url);

        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("GitHub API returned {Status}: {Body}", response.StatusCode, body);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return 0;
            }

            response.EnsureSuccessStatusCode(); // throws
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var items = JArray.Parse(json);

        var fileCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var type = item.Value<string>("type");
            var name = item.Value<string>("name");

            if (string.IsNullOrEmpty(name))
                continue;

            if (type == "dir")
            {
                var subDir = Path.Combine(localDirectory, name);
                Directory.CreateDirectory(subDir);

                var subPath = item.Value<string>("path") ?? $"{repoPath}/{name}";
                fileCount += await DownloadDirectoryAsync(subPath, subDir, progress, cancellationToken).ConfigureAwait(false);
            }
            else if (type == "file")
            {
                var downloadUrl = item.Value<string>("download_url");
                if (string.IsNullOrEmpty(downloadUrl))
                    continue;

                progress?.Report($"Downloading {name}...");
                _logger.LogDebug("Downloading file: {Name}", name);

                var fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                var localPath = Path.Combine(localDirectory, name);
                await File.WriteAllBytesAsync(localPath, fileBytes, cancellationToken).ConfigureAwait(false);

                fileCount++;
            }
        }

        return fileCount;
    }

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a <c>.git</c> directory.
    /// Returns the repository root path, or <c>null</c> if not inside a git repo.
    /// </summary>
    private static string? FindGitRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
