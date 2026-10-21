namespace Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;

/// <summary>
/// Persisted state of the welcome guide progress.
/// </summary>
internal sealed class WelcomeGuideState
{
    /// <summary>
    /// Step IDs the user has already seen / completed.
    /// </summary>
    public List<string> CompletedStepIds { get; set; } = new();

    /// <summary>
    /// True when the user explicitly skipped or finished the full guide.
    /// </summary>
    public bool IsGuideCompleted { get; set; }

    /// <summary>
    /// The highest feature version the user has seen.
    /// New steps with a higher version trigger the "new features" prompt.
    /// </summary>
    public string LastSeenFeatureVersion { get; set; } = string.Empty;
}
