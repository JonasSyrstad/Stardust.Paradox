using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// View model for the welcome guide overlay that walks users through application features.
/// Supports both first-run full guides and new-feature highlights for returning users.
/// </summary>
public partial class WelcomeGuideViewModel : ObservableObject
{
    private readonly IWelcomeGuideService _guideService;

    public WelcomeGuideViewModel(IWelcomeGuideService guideService)
    {
        _guideService = guideService;
        Steps = new ObservableCollection<WelcomeGuideStep>();
    }

    /// <summary>
    /// The ordered list of steps being shown in this guide session.
    /// </summary>
    public ObservableCollection<WelcomeGuideStep> Steps { get; }

    /// <summary>
    /// Whether the guide overlay is currently visible.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// Zero-based index of the currently displayed step.
    /// </summary>
    [ObservableProperty]
    private int _currentStepIndex;

    /// <summary>
    /// The currently displayed step.
    /// </summary>
    [ObservableProperty]
    private WelcomeGuideStep? _currentStep;

    /// <summary>
    /// Display string for step progress (e.g. "3 / 12").
    /// </summary>
    [ObservableProperty]
    private string _stepProgressText = string.Empty;

    /// <summary>
    /// Whether the Previous button should be enabled.
    /// </summary>
    [ObservableProperty]
    private bool _canGoPrevious;

    /// <summary>
    /// Whether the user is on the last step (changes Next button text to Finish).
    /// </summary>
    [ObservableProperty]
    private bool _isLastStep;

    /// <summary>
    /// True when showing only new-feature steps for a returning user.
    /// </summary>
    [ObservableProperty]
    private bool _isNewFeaturesMode;

    /// <summary>
    /// Title displayed in the guide header. Changes between full guide and new-features mode.
    /// </summary>
    [ObservableProperty]
    private string _guideTitle = "Welcome to Gremlin Studio";

    #region Bubble Positioning

    /// <summary>
    /// Left position of the bubble in pixels relative to the window.
    /// </summary>
    [ObservableProperty]
    private double _bubbleLeft;

    /// <summary>
    /// Top position of the bubble in pixels relative to the window.
    /// </summary>
    [ObservableProperty]
    private double _bubbleTop;

    /// <summary>
    /// Which side the arrow points from (toward the target element).
    /// </summary>
    [ObservableProperty]
    private BubbleArrowSide _arrowSide;

    /// <summary>
    /// X center of the target element relative to the window. Used to aim the arrow.
    /// </summary>
    [ObservableProperty]
    private double _targetCenterX;

    /// <summary>
    /// Y center of the target element relative to the window. Used to aim the arrow.
    /// </summary>
    [ObservableProperty]
    private double _targetCenterY;

    /// <summary>
    /// Raised when a step changes and the UI needs to navigate and position the bubble.
    /// The MainWindow subscribes to this to perform element lookup and positioning.
    /// </summary>
    public event EventHandler<WelcomeGuideStep>? StepNavigationRequested;

    #endregion

    partial void OnCurrentStepIndexChanged(int value)
    {
        UpdateNavigationState();
    }

    /// <summary>
    /// Starts the full welcome guide showing all steps.
    /// </summary>
    public async Task StartFullGuideAsync()
    {
        var allSteps = _guideService.GetAllSteps();
        LoadSteps(allSteps);
        IsNewFeaturesMode = false;
        GuideTitle = "Welcome to Gremlin Studio";
        IsVisible = true;
    }

    /// <summary>
    /// Starts a guide session showing only new/unseen feature steps.
    /// </summary>
    public async Task StartNewFeaturesGuideAsync()
    {
        var unseenSteps = await _guideService.GetUnseenStepsAsync().ConfigureAwait(true);
        if (unseenSteps.Count == 0)
            return;

        LoadSteps(unseenSteps);
        IsNewFeaturesMode = true;
        GuideTitle = "New Features";
        IsVisible = true;
    }

    /// <summary>
    /// Checks whether the guide should be shown on startup and starts the appropriate mode.
    /// </summary>
    public async Task CheckAndShowOnStartupAsync()
    {
        var isCompleted = await _guideService.IsGuideCompletedAsync().ConfigureAwait(true);

        if (!isCompleted)
        {
            await StartFullGuideAsync().ConfigureAwait(true);
            return;
        }

        var hasUnseen = await _guideService.HasUnseenFeaturesAsync().ConfigureAwait(true);
        if (hasUnseen)
        {
            await StartNewFeaturesGuideAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Advances to the next step, or finishes the guide if on the last step.
    /// </summary>
    [RelayCommand]
    private async Task NextStepAsync()
    {
        if (CurrentStep is not null)
        {
            await _guideService.CompleteStepAsync(CurrentStep.Id).ConfigureAwait(true);
        }

        if (CurrentStepIndex < Steps.Count - 1)
        {
            CurrentStepIndex++;
        }
        else
        {
            await FinishGuideAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Goes back to the previous step.
    /// </summary>
    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
        }
    }

    /// <summary>
    /// Skips the guide entirely and marks it as completed.
    /// </summary>
    [RelayCommand]
    private async Task SkipGuideAsync()
    {
        await _guideService.FinishGuideAsync().ConfigureAwait(true);
        IsVisible = false;
    }

    /// <summary>
    /// Resets all progress and restarts the full guide.
    /// </summary>
    [RelayCommand]
    private async Task ResetGuideAsync()
    {
        await _guideService.ResetProgressAsync().ConfigureAwait(true);
        await StartFullGuideAsync().ConfigureAwait(true);
    }

    private async Task FinishGuideAsync()
    {
        await _guideService.FinishGuideAsync().ConfigureAwait(true);
        IsVisible = false;
    }

    private void LoadSteps(IReadOnlyList<WelcomeGuideStep> steps)
    {
        Steps.Clear();
        foreach (var step in steps)
        {
            Steps.Add(step);
        }

        CurrentStepIndex = 0;
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        if (Steps.Count == 0)
        {
            CurrentStep = null;
            StepProgressText = string.Empty;
            CanGoPrevious = false;
            IsLastStep = false;
            return;
        }

        CurrentStep = Steps[CurrentStepIndex];
        StepProgressText = $"{CurrentStepIndex + 1} / {Steps.Count}";
        CanGoPrevious = CurrentStepIndex > 0;
        IsLastStep = CurrentStepIndex == Steps.Count - 1;
        ArrowSide = CurrentStep.ArrowSide;

        StepNavigationRequested?.Invoke(this, CurrentStep);
    }
}
