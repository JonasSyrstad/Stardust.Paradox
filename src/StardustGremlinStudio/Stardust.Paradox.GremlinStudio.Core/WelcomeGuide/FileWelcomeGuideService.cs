using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;

/// <summary>
/// File-backed implementation of <see cref="IWelcomeGuideService"/>.
/// Persists guide progress to a JSON file in the user's AppData directory.
/// </summary>
public sealed class FileWelcomeGuideService : IWelcomeGuideService
{
    private readonly ILogger<FileWelcomeGuideService> _logger;
    private readonly List<WelcomeGuideStep> _steps;
    private WelcomeGuideState? _cachedState;

    public FileWelcomeGuideService(ILogger<FileWelcomeGuideService> logger)
    {
        _logger = logger;
        _steps = BuildSteps();
    }

    /// <inheritdoc />
    public IReadOnlyList<WelcomeGuideStep> GetAllSteps() => _steps.AsReadOnly();

    /// <inheritdoc />
    public async Task<IReadOnlyList<WelcomeGuideStep>> GetUnseenStepsAsync()
    {
        var state = await LoadStateAsync().ConfigureAwait(false);
        var unseen = _steps
            .Where(s => !state.CompletedStepIds.Contains(s.Id))
            .ToList();
        return unseen.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<bool> IsGuideCompletedAsync()
    {
        var state = await LoadStateAsync().ConfigureAwait(false);
        return state.IsGuideCompleted;
    }

    /// <inheritdoc />
    public async Task<bool> HasUnseenFeaturesAsync()
    {
        var state = await LoadStateAsync().ConfigureAwait(false);

        if (!state.IsGuideCompleted)
            return false;

        var latestVersion = GetLatestFeatureVersion();
        if (string.IsNullOrEmpty(latestVersion) || string.IsNullOrEmpty(state.LastSeenFeatureVersion))
            return _steps.Any(s => !state.CompletedStepIds.Contains(s.Id));

        return CompareVersions(latestVersion, state.LastSeenFeatureVersion) > 0;
    }

    /// <inheritdoc />
    public async Task CompleteStepAsync(string stepId)
    {
        var state = await LoadStateAsync().ConfigureAwait(false);

        if (!state.CompletedStepIds.Contains(stepId))
        {
            state.CompletedStepIds.Add(stepId);
            await SaveStateAsync(state).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task FinishGuideAsync()
    {
        var state = await LoadStateAsync().ConfigureAwait(false);
        state.IsGuideCompleted = true;
        state.LastSeenFeatureVersion = GetLatestFeatureVersion();

        // Mark all current steps as completed
        foreach (var step in _steps)
        {
            if (!state.CompletedStepIds.Contains(step.Id))
                state.CompletedStepIds.Add(step.Id);
        }

        await SaveStateAsync(state).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResetProgressAsync()
    {
        _cachedState = new WelcomeGuideState();
        await SaveStateAsync(_cachedState).ConfigureAwait(false);
    }

    private string GetLatestFeatureVersion()
    {
        return _steps.Count > 0
            ? _steps.Max(s => s.FeatureVersion) ?? string.Empty
            : string.Empty;
    }

    private static int CompareVersions(string a, string b)
    {
        if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb))
            return va.CompareTo(vb);
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<WelcomeGuideState> LoadStateAsync()
    {
        if (_cachedState is not null)
            return _cachedState;

        var path = AppDataPaths.WelcomeGuideFilePath;

        try
        {
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                _cachedState = JsonConvert.DeserializeObject<WelcomeGuideState>(json) ?? new WelcomeGuideState();
            }
            else
            {
                _cachedState = new WelcomeGuideState();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load welcome guide state, starting fresh");
            _cachedState = new WelcomeGuideState();
        }

        return _cachedState;
    }

    private async Task SaveStateAsync(WelcomeGuideState state)
    {
        _cachedState = state;
        var path = AppDataPaths.WelcomeGuideFilePath;

        try
        {
            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save welcome guide state");
        }
    }

    /// <summary>
    /// Defines all welcome guide steps. New features should be added here with
    /// the version they were introduced in. The guide automatically detects
    /// new steps for returning users.
    /// </summary>
    private static List<WelcomeGuideStep> BuildSteps()
    {
        return
        [
            new WelcomeGuideStep(
                "connections",
                "Manage Connections",
                "Create and manage connections to Gremlin databases. Click 'New' to add a connection, fill in host, port, and credentials, then 'Save'. Use 'Test' to verify connectivity.",
                "1.0.0",
                "GuideTarget_NewConnectionButton",
                GuideNavigationAction.EnsureSettingsPanelOpen,
                BubbleArrowSide.Left),

            new WelcomeGuideStep(
                "query-editor",
                "Write Gremlin Queries",
                "Type Gremlin queries here with syntax highlighting and autocomplete. Press Tab for suggestions, Ctrl+Enter or F5 to execute, Escape to cancel.",
                "1.0.0",
                "GuideTarget_RunButton",
                GuideNavigationAction.None,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "query-history",
                "Query History",
                "Previously executed queries appear in this dropdown. Select one to reload it. Pin important queries to keep them when clearing history.",
                "1.0.0",
                "GuideTarget_HistoryDropdown",
                GuideNavigationAction.None,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "query-tabs",
                "Multiple Query Tabs",
                "Click '+' to open more tabs and work on different queries simultaneously. Each tab has its own query, results, and connection.",
                "1.0.0",
                "GuideTarget_NewTabButton",
                GuideNavigationAction.None,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "result-json",
                "JSON Results",
                "Raw query results appear here as formatted JSON. Use the 'Copy All' button to copy results to your clipboard.",
                "1.0.0",
                "GuideTarget_JsonTab",
                GuideNavigationAction.SwitchToJsonTab,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "result-table",
                "Table View",
                "See results in a sortable table with column reordering. Export data to CSV or Excel with the toolbar buttons.",
                "1.0.0",
                "GuideTarget_TableTab",
                GuideNavigationAction.SwitchToTableTab,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "result-tree",
                "Tree & Schema Discovery",
                "Browse results as a tree, or switch to Schema mode to auto-discover your graph structure. Generate C# interfaces from the schema.",
                "1.0.0",
                "GuideTarget_TreeTab",
                GuideNavigationAction.SwitchToTreeTab,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "graph-view",
                "Interactive Graph",
                "Visualize query results as an interactive graph. Drag nodes, zoom with Ctrl+Scroll, and click to inspect properties.",
                "1.0.0",
                "GuideTarget_GraphTab",
                GuideNavigationAction.SwitchToGraphTab,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "playground",
                "Local Playground",
                "Start a local in-memory graph database for experimentation without a remote server. Load built-in scenarios for sample data.",
                "1.0.0",
                "GuideTarget_StartPlaygroundButton",
                GuideNavigationAction.EnsureSettingsPanelOpen,
                BubbleArrowSide.Left),

            new WelcomeGuideStep(
                "scenario-export",
                "Export Scenarios",
                "Capture query results as reusable test scenarios. Export as JSON or C# code for the InMemory testing framework.",
                "1.0.0",
                "GuideTarget_ExportTab",
                GuideNavigationAction.SwitchToExportTab,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "themes",
                "Theme Selection",
                "Choose from multiple themes: Light, Dark, MoonLight, Dark Forrest, and Muddy River.",
                "1.0.0",
                "GuideTarget_ThemeDropdown",
                GuideNavigationAction.EnsureSettingsPanelOpen,
                BubbleArrowSide.Left),

            new WelcomeGuideStep(
                "keyboard-shortcuts",
                "Keyboard Shortcuts",
                "Press F1 to see all shortcuts. Ctrl+B toggles the settings panel. Ctrl+Enter runs queries.",
                "1.0.0",
                "GuideTarget_ShortcutsHint",
                GuideNavigationAction.ShowKeyboardShortcutsDialog,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "agent-skills",
                "Download Agent Skills",
                "Download reusable InMemory scenario skills for AI agent frameworks from the Stardust.Paradox repository.",
                "1.3.0",
                "GuideTarget_ExportTab",
                GuideNavigationAction.SwitchToExportTab,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "query-file-open-save",
                "Open & Save Queries",
                "Save queries to .gremlin files (Ctrl+S) and open them later (Ctrl+O). Use Ctrl+Shift+S to Save As. Files are loaded into a new tab or the current empty tab.",
                "1.5.0",
                "GuideTarget_OpenQueryButton",
                GuideNavigationAction.None,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "execution-log",
                "Execution Log",
                "The Log tab records every query you run during this session. See the query text, execution time, result count, RU cost, and any errors at a glance. Use Clear Log to reset.",
                "1.5.0",
                "GuideTarget_LogTab",
                GuideNavigationAction.SwitchToLogTab,
                BubbleArrowSide.Top),

            new WelcomeGuideStep(
                "find-replace",
                "Find & Replace",
                "Press Ctrl+F to find text in the query editor, or Ctrl+H to open find and replace. The panel is draggable and supports wrap-around search.",
                "1.5.0",
                "GuideTarget_RunButton",
                GuideNavigationAction.None,
                BubbleArrowSide.Top),
        ];
    }
}
