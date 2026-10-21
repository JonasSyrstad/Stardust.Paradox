namespace Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;

/// <summary>
/// Service that manages the welcome guide lifecycle, progress persistence,
/// and detection of new features for returning users.
/// </summary>
public interface IWelcomeGuideService
{
    /// <summary>
    /// Gets all defined guide steps in display order.
    /// </summary>
    IReadOnlyList<WelcomeGuideStep> GetAllSteps();

    /// <summary>
    /// Gets steps the user has not yet seen (supports new-feature highlighting).
    /// </summary>
    Task<IReadOnlyList<WelcomeGuideStep>> GetUnseenStepsAsync();

    /// <summary>
    /// Returns true when the user has completed or skipped the guide at least once.
    /// </summary>
    Task<bool> IsGuideCompletedAsync();

    /// <summary>
    /// Returns true when new features have been added since the user last completed the guide.
    /// </summary>
    Task<bool> HasUnseenFeaturesAsync();

    /// <summary>
    /// Marks a single step as completed.
    /// </summary>
    Task CompleteStepAsync(string stepId);

    /// <summary>
    /// Marks the entire guide as completed/skipped and records the latest feature version.
    /// </summary>
    Task FinishGuideAsync();

    /// <summary>
    /// Resets all progress so the guide starts from the beginning.
    /// </summary>
    Task ResetProgressAsync();
}
