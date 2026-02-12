using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Stardust.Paradox.GremlinStudio.Services;

// Alias to avoid ambiguity with Velopack.UpdateInfo
using AppUpdateInfo = Stardust.Paradox.GremlinStudio.Core.Updates.UpdateInfo;

/// <summary>
/// Update service implementation using Velopack with GitHub releases.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly UpdateManager _updateManager;
    private AppUpdateInfo? _availableUpdate;
    private bool _isChecking;
    private bool _isDownloading;
    private int _downloadProgress;

    private const string GitHubRepoId = "JonasSyrstad/Stardust.Paradox";
    private const string VelopackChannel = "win";
    
    // TODO: Set to false before release - enables fake update for testing UI
    private const bool SimulateUpdateAvailable = false;
    private const string SimulatedVersion = "99.0.0";

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        
        // Configure Velopack with GitHub releases as the update source
        var source = new GithubSource(GitHubRepoId, accessToken: null, prerelease: false);
        _updateManager = new UpdateManager(source, new UpdateOptions { ExplicitChannel = VelopackChannel });
    }

    /// <inheritdoc />
    public string CurrentVersion => _updateManager.CurrentVersion?.ToString() ?? "Development";

    /// <inheritdoc />
    public bool IsUpdateAvailable => _availableUpdate is not null;

    /// <inheritdoc />
    public AppUpdateInfo? AvailableUpdate => _availableUpdate;
    
    // Explicit interface implementation for the interface type
    Core.Updates.UpdateInfo? IUpdateService.AvailableUpdate => _availableUpdate;

    /// <inheritdoc />
    public bool IsChecking => _isChecking;

    /// <inheritdoc />
    public bool IsDownloading => _isDownloading;

    /// <inheritdoc />
    public int DownloadProgress => _downloadProgress;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (_isChecking || _isDownloading)
            return false;

        try
        {
            _isChecking = true;
            OnStateChanged();

            _logger.LogInformation("Checking for updates...");

            // For testing: simulate an update being available
            if (SimulateUpdateAvailable)
            {
                await Task.Delay(1500, cancellationToken).ConfigureAwait(false); // Simulate network delay
                _availableUpdate = new AppUpdateInfo(
                    Version: SimulatedVersion,
                    ReleaseNotes: "This is a simulated update for testing the auto-update UI.",
                    PublishedDate: DateTimeOffset.Now,
                    SizeBytes: 150_000_000);
                
                _logger.LogInformation("SIMULATED update available: {Version}", _availableUpdate.Version);
                OnStateChanged();
                return true;
            }

            var updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
            
            if (updateInfo is not null)
            {
                _availableUpdate = new AppUpdateInfo(
                    Version: updateInfo.TargetFullRelease.Version.ToString(),
                    ReleaseNotes: null, // Velopack doesn't provide release notes in the check
                    PublishedDate: null,
                    SizeBytes: updateInfo.TargetFullRelease.Size);

                _logger.LogInformation("Update available: {Version}", _availableUpdate.Version);
                OnStateChanged();
                return true;
            }

            _logger.LogInformation("No updates available. Current version: {Version}", CurrentVersion);
            _availableUpdate = null;
            OnStateChanged();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates");
            _availableUpdate = null;
            OnStateChanged();
            return false;
        }
        finally
        {
            _isChecking = false;
            OnStateChanged();
        }
    }

    /// <inheritdoc />
    public async Task DownloadAndApplyUpdateAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_availableUpdate is null || _isDownloading)
            return;

        try
        {
            _isDownloading = true;
            _downloadProgress = 0;
            OnStateChanged();

            _logger.LogInformation("Downloading update {Version}...", _availableUpdate.Version);

            // For testing: simulate download progress
            if (SimulateUpdateAvailable)
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    _downloadProgress = i;
                    progress?.Report(i);
                    OnStateChanged();
                }
                
                _logger.LogInformation("SIMULATED download complete. In real scenario, app would restart.");
                
                // Show a message instead of actually restarting
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"Simulated update to v{_availableUpdate.Version} complete!\n\n" +
                        "In a real scenario, the application would restart now.\n\n" +
                        "Set SimulateUpdateAvailable = false in UpdateService.cs to disable this test mode.",
                        "Update Simulation",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                });
                
                _availableUpdate = null;
                OnStateChanged();
                return;
            }

            var updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (updateInfo is null)
            {
                _logger.LogWarning("Update no longer available");
                return;
            }

            // Download the update with progress reporting
            await _updateManager.DownloadUpdatesAsync(
                updateInfo,
                p =>
                {
                    _downloadProgress = p;
                    progress?.Report(p);
                    OnStateChanged();
                }).ConfigureAwait(false);

            _logger.LogInformation("Update downloaded. Applying and restarting...");

            // Apply the update and restart the application
            _updateManager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and apply update");
            throw;
        }
        finally
        {
            _isDownloading = false;
            _downloadProgress = 0;
            OnStateChanged();
        }
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
