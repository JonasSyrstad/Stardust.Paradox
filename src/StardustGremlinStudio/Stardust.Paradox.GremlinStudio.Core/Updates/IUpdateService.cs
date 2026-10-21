namespace Stardust.Paradox.GremlinStudio.Core.Updates;

/// <summary>
/// Represents information about an available application update.
/// </summary>
public sealed record UpdateInfo(
    string Version,
    string? ReleaseNotes,
    DateTimeOffset? PublishedDate,
    long? SizeBytes);

/// <summary>
/// Service for checking and applying application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Gets the current application version.
    /// </summary>
    string CurrentVersion { get; }
    
    /// <summary>
    /// Gets whether an update is currently available.
    /// </summary>
    bool IsUpdateAvailable { get; }
    
    /// <summary>
    /// Gets information about the available update, if any.
    /// </summary>
    UpdateInfo? AvailableUpdate { get; }
    
    /// <summary>
    /// Gets whether an update check is in progress.
    /// </summary>
    bool IsChecking { get; }
    
    /// <summary>
    /// Gets whether an update download is in progress.
    /// </summary>
    bool IsDownloading { get; }
    
    /// <summary>
    /// Gets the download progress (0-100) when downloading an update.
    /// </summary>
    int DownloadProgress { get; }
    
    /// <summary>
    /// Checks for available updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an update is available, false otherwise.</returns>
    Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads and applies the available update, then restarts the application.
    /// </summary>
    /// <param name="progress">Optional progress callback (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DownloadAndApplyUpdateAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when update state changes.
    /// </summary>
    event EventHandler? StateChanged;
}
