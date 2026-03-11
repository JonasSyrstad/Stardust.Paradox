namespace Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;

/// <summary>
/// Which side of the target element the bubble arrow points from.
/// </summary>
public enum BubbleArrowSide
{
    Bottom,
    Top,
    Left,
    Right
}

/// <summary>
/// UI navigation action to perform before showing a guide step.
/// </summary>
public enum GuideNavigationAction
{
    None,
    EnsureSettingsPanelOpen,
    SwitchToJsonTab,
    SwitchToTableTab,
    SwitchToTreeTab,
    SwitchToGraphTab,
    SwitchToExportTab,
    SwitchToLogTab,
    ShowKeyboardShortcutsDialog
}

/// <summary>
/// Represents a single step in the welcome guide that highlights a feature.
/// </summary>
/// <param name="Id">Unique stable identifier for this step (used for tracking completion).</param>
/// <param name="Title">Short title displayed in the guide bubble.</param>
/// <param name="Description">Longer explanation of the feature and how to use it.</param>
/// <param name="FeatureVersion">The application version that introduced this feature (e.g. "1.0.0").
/// Used to detect new features for returning users.</param>
/// <param name="TargetElementName">Name of the UI element the bubble should point to.</param>
/// <param name="NavigationAction">UI action to perform before showing the bubble.</param>
/// <param name="ArrowSide">Which side of the bubble the arrow appears on (points toward the target).</param>
public sealed record WelcomeGuideStep(
    string Id,
    string Title,
    string Description,
    string FeatureVersion,
    string TargetElementName,
    GuideNavigationAction NavigationAction = GuideNavigationAction.None,
    BubbleArrowSide ArrowSide = BubbleArrowSide.Top);
