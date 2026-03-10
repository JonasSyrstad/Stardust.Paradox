using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Stardust.Paradox.Data;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Execution;
using Stardust.Paradox.GremlinStudio.Core.Export;
using Stardust.Paradox.GremlinStudio.Core.History;
using Stardust.Paradox.GremlinStudio.Core.Playground;
using Stardust.Paradox.GremlinStudio.Core.Schema;
using Stardust.Paradox.GremlinStudio.Core.Updates;
using Stardust.Paradox.GremlinStudio.Core.Storage;
using Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;
using Stardust.Paradox.GremlinStudio.Dialogs;
using Stardust.Paradox.GremlinStudio.Services;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// Main view model for the Gremlin Studio application.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IGremlinConnectionStore _connectionStore;
    private readonly IGremlinConnectorFactory _connectorFactory;
    private readonly IGremlinConnectionTester _connectionTester;
    private readonly IGremlinConnectionExporter _connectionExporter;
    private readonly IGremlinQueryExecutor _queryExecutor;
    private readonly IPlaygroundService _playgroundService;
    private readonly IQueryHistoryService _queryHistoryService;
    private readonly IScenarioExportService _scenarioExportService;
    private readonly ISchemaDiscoveryService _schemaDiscoveryService;
    private readonly ISchemaExportService _schemaExportService;
    private readonly IThemeService _themeService;
    private readonly ICosmosDbDiscoveryService _discoveryService;
    private readonly IAgentSkillsDownloadService _agentSkillsDownloadService;
    private readonly IWelcomeGuideService _welcomeGuideService;
    private readonly ILogger<MainViewModel> _logger;

    private IGremlinLanguageConnector? _activeConnector;
    private CancellationTokenSource? _queryCts;
    private CancellationTokenSource? _exportCts;
    private Task? _connectionLoadingTask;

    public MainViewModel(
        IGremlinConnectionStore connectionStore,
        IGremlinConnectorFactory connectorFactory,
        IGremlinConnectionTester connectionTester,
        IGremlinConnectionExporter connectionExporter,
        IGremlinQueryExecutor queryExecutor,
        IPlaygroundService playgroundService,
        IQueryHistoryService queryHistoryService,
        IScenarioExportService scenarioExportService,
        ISchemaDiscoveryService schemaDiscoveryService,
        ISchemaExportService schemaExportService,
        IThemeService themeService,
        ICosmosDbDiscoveryService discoveryService,
        IUpdateService updateService,
        IAgentSkillsDownloadService agentSkillsDownloadService,
        IWelcomeGuideService welcomeGuideService,
        ILogger<MainViewModel> logger)
    {
        _connectionStore = connectionStore;
        _connectorFactory = connectorFactory;
        _connectionTester = connectionTester;
        _connectionExporter = connectionExporter;
        _queryExecutor = queryExecutor;
        _playgroundService = playgroundService;
        _queryHistoryService = queryHistoryService;
        _scenarioExportService = scenarioExportService;
        _schemaDiscoveryService = schemaDiscoveryService;
        _schemaExportService = schemaExportService;
        _themeService = themeService;
        _discoveryService = discoveryService;
        _agentSkillsDownloadService = agentSkillsDownloadService;
        _welcomeGuideService = welcomeGuideService;
        _updateService = updateService;
        _logger = logger;

        // Initialize welcome guide
        WelcomeGuide = new WelcomeGuideViewModel(_welcomeGuideService);

        // Reset tab counter for fresh start
        QueryTabViewModel.ResetTabCounter();

        // Initialize collections
        Connections = new ObservableCollection<GremlinConnectionMetadata>();
        QueryTabs = new ObservableCollection<QueryTabViewModel>();
        ConnectionKinds = Enum.GetValues<GremlinConnectionKind>().ToList();
        ScenarioNames = _playgroundService.GetAvailableScenarios().Select(s => s.Name).ToList();
        ThemeModes = Enum.GetValues<ThemeMode>().ToList();
        ExportFormats = Enum.GetValues<ScenarioExportFormat>().ToList();
        _selectedThemeMode = _themeService.CurrentMode;

        // Subscribe to theme changes
        _themeService.ThemeChanged += (_, mode) => SelectedThemeMode = mode;
        
        // Subscribe to update service changes
        _updateService.StateChanged += OnUpdateStateChanged;


        // Default values
        EditConnectionKind = GremlinConnectionKind.GremlinServer;
        EditPort = 8182;
        StatusText = "Ready";


        // Subscribe to playground state changes
        _playgroundService.StateChanged += OnPlaygroundStateChanged;

        // Load connections and query history on startup
        _ = InitializeStartupStateAsync();
        
        // Check for updates in background on startup
        _ = CheckForUpdatesOnStartupAsync();

        // Create initial tab
        CreateNewTab();

        // Show welcome guide on startup (first run or new features)
        _ = WelcomeGuide.CheckAndShowOnStartupAsync();
    }

    private async Task InitializeStartupStateAsync()
    {
        _connectionLoadingTask = LoadConnectionsAsync();
        await _connectionLoadingTask.ConfigureAwait(true);

        await LoadQueryHistoryAsync().ConfigureAwait(true);
    }

    private async Task LoadQueryHistoryAsync()
    {
        await _queryHistoryService.LoadAsync();
        RefreshQueryHistory();
    }

    private bool _isRefreshingHistory;

    private void RefreshQueryHistory()
    {
        // Guard: prevent ComboBox auto-selection from resetting QueryText
        // when the ItemsSource collection is replaced.
        _isRefreshingHistory = true;
        try
        {
            var connectionId = SelectedTab?.ConnectionMetadata?.Id;
            var newHistory = new ObservableCollection<QueryHistoryItem>(
                _queryHistoryService.GetHistory(connectionId));
            QueryHistory = newHistory;
        }
        finally
        {
            _isRefreshingHistory = false;
        }
    }

    #region Properties

    #region Query Tabs

    /// <summary>
    /// Collection of open query tabs.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<QueryTabViewModel> _queryTabs;

    /// <summary>
    /// The currently selected query tab.
    /// </summary>
    [ObservableProperty]
    private QueryTabViewModel? _selectedTab;

    /// <summary>
    /// Handles tab selection changes - syncs connection panel with tab's connection.
    /// </summary>
    partial void OnSelectedTabChanged(QueryTabViewModel? oldValue, QueryTabViewModel? newValue)
    {
        if (newValue == null) return;

        // Sync connection panel with the tab's connection
        if (newValue.ConnectionMetadata != null)
        {
            // Find matching connection in the list
            var matchingConnection = Connections.FirstOrDefault(c => c.Id == newValue.ConnectionMetadata.Id);
            if (matchingConnection != null)
            {
                // Temporarily disable change handling
                _isSyncingTabConnection = true;
                SelectedConnection = matchingConnection;
                _isSyncingTabConnection = false;
            }
        }
        else if (newValue.IsUsingPlayground)
        {
            _isSyncingTabConnection = true;
            SelectedConnection = null;
            _isSyncingTabConnection = false;
        }
        
        // Update status from tab
        StatusText = newValue.StatusText;
        LastDurationText = newValue.LastDurationText;
        LastRequestUnitsText = newValue.LastRequestUnitsText;

        // Refresh history for the new tab's connection
        RefreshQueryHistory();
    }

    private bool _isSyncingTabConnection;

    #endregion

    [ObservableProperty]
    private ObservableCollection<QueryHistoryItem> _queryHistory = new();

    [ObservableProperty]
    private QueryHistoryItem? _selectedHistoryItem;

    partial void OnSelectedHistoryItemChanged(QueryHistoryItem? value)
    {
        if (_isRefreshingHistory)
        {
            // Suppress ComboBox auto-selection during collection refresh
            SelectedHistoryItem = null;
            return;
        }

        if (value != null && SelectedTab != null)
        {
            SelectedTab.QueryText = value.Query;
            SelectedHistoryItem = null;
        }
    }

    [ObservableProperty]
    private ObservableCollection<GremlinConnectionMetadata> _connections;

    [ObservableProperty]
    private GremlinConnectionMetadata? _selectedConnection;

    private bool _isLoadingConnections;


    [ObservableProperty]
    private List<GremlinConnectionKind> _connectionKinds;

    [ObservableProperty]
    private List<string> _scenarioNames;

    [ObservableProperty]
    private string? _selectedScenarioName;

    // Theme
    public List<ThemeMode> ThemeModes { get; }

    [ObservableProperty]
    private ThemeMode _selectedThemeMode;

    partial void OnSelectedThemeModeChanged(ThemeMode value)
    {
        _themeService.SetTheme(value);
    }

    private void SaveLastConnectionPreference(string? connectionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                if (File.Exists(AppDataPaths.LastConnectionPreferenceFilePath))
                {
                    File.Delete(AppDataPaths.LastConnectionPreferenceFilePath);
                }
                return;
            }

            File.WriteAllText(AppDataPaths.LastConnectionPreferenceFilePath, connectionId);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private string? LoadLastConnectionPreference()
    {
        try
        {
            if (!File.Exists(AppDataPaths.LastConnectionPreferenceFilePath))
                return null;

            var id = File.ReadAllText(AppDataPaths.LastConnectionPreferenceFilePath).Trim();
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch
        {
            return null;
        }
    }

    // Connection editing fields
    [ObservableProperty]
    private string _editConnectionName = string.Empty;

    [ObservableProperty]
    private GremlinConnectionKind _editConnectionKind;

    [ObservableProperty]
    private string _editHost = string.Empty;

    [ObservableProperty]
    private int _editPort;

    [ObservableProperty]
    private string _editUsername = string.Empty;

    [ObservableProperty]
    private string _editSecret = string.Empty;

    [ObservableProperty]
    private string _editDatabase = string.Empty;

    [ObservableProperty]
    private string _editGraph = string.Empty;

    // Status (shared across tabs, shows active tab status)
    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _lastDurationText = string.Empty;

    /// <summary>
    /// RU consumption text from the active tab (visible only for Cosmos DB connections).
    /// </summary>
    [ObservableProperty]
    private string _lastRequestUnitsText = string.Empty;

    [ObservableProperty]
    private bool _isPlaygroundRunning;


    [ObservableProperty]
    private string? _loadedScenarioName;

    public List<ScenarioExportFormat> ExportFormats { get; }

    /// <summary>
    /// Dictionary mapping column names to whether they represent properties (true) or instance values (false).
    /// Used for visual differentiation in the results DataGrid.
    /// </summary>
    public Dictionary<string, bool> ColumnIsProperty { get; } = new();

    #region Updates

    /// <summary>
    /// Gets the update service for checking and applying updates.
    /// </summary>
    public Core.Updates.IUpdateService UpdateService => _updateService;
    private readonly Core.Updates.IUpdateService _updateService;

    /// <summary>
    /// Gets whether an update is available.
    /// </summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    /// <summary>
    /// Gets the available update version string.
    /// </summary>
    [ObservableProperty]
    private string? _updateVersion;

    /// <summary>
    /// Gets whether an update check is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isCheckingForUpdates;

    /// <summary>
    /// Gets whether an update download is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isDownloadingUpdate;

    /// <summary>
    /// Gets the current update download progress (0-100).
    /// </summary>
    [ObservableProperty]
    private int _updateDownloadProgress;

    /// <summary>
    /// Gets or sets whether the update pane is open.
    /// </summary>
    [ObservableProperty]
    private bool _isUpdatePaneOpen;

    /// <summary>
    /// Gets the current application version from the update service.
    /// </summary>
    public string CurrentVersion => _updateService.CurrentVersion;

    #endregion

    #endregion

    #region Commands

    #region Tab Commands

    /// <summary>
    /// Creates a new query tab with the current connection settings from the left panel.
    /// </summary>
    [RelayCommand]
    private async Task NewTabAsync()
    {
        GremlinConnectionSettings? connectionSettings = null;

        // Get the currently selected connection settings from the left panel
        if (SelectedConnection != null && !IsPlaygroundRunning)
        {
            try
            {
                // Wait for any pending connection loading to complete
                if (_connectionLoadingTask != null)
                {
                    await _connectionLoadingTask.ConfigureAwait(true);
                }

                connectionSettings = await _connectionStore.GetConnectionWithSecretAsync(SelectedConnection.Id).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load connection settings for new tab");
            }
        }

        CreateNewTab(connectionSettings);
    }

    /// <summary>
    /// Closes the specified tab.
    /// </summary>
    [RelayCommand]
    private void CloseTab(QueryTabViewModel? tab)
    {
        if (tab == null || QueryTabs.Count <= 1) return;

        var index = QueryTabs.IndexOf(tab);
        QueryTabs.Remove(tab);

        // Select adjacent tab
        if (index >= QueryTabs.Count) index = QueryTabs.Count - 1;
        if (index >= 0) SelectedTab = QueryTabs[index];
    }

    private QueryTabViewModel CreateNewTab(GremlinConnectionSettings? connectionSettings = null)
    {
        var tab = new QueryTabViewModel(
            _queryExecutor,
            _queryHistoryService,
            _scenarioExportService,
            _connectorFactory,
            _schemaDiscoveryService,
            _schemaExportService,
            _logger,
            status =>
            {
                StatusText = status;
                LastRequestUnitsText = SelectedTab?.LastRequestUnitsText ?? string.Empty;
            },
            RefreshQueryHistory,
            connectionSettings);

        // If playground is running, use it for the new tab
        if (IsPlaygroundRunning && _playgroundService.Connector != null)
        {
            tab.UsePlaygroundConnector(_playgroundService.Connector, LoadedScenarioName);
        }

        QueryTabs.Add(tab);
        SelectedTab = tab;
        return tab;
    }

    #endregion



    [RelayCommand]
    private async Task NewConnectionAsync()
    {
        try
        {
            var dialogViewModel = new NewConnectionDialogViewModel(_discoveryService);
            var dialog = new Dialogs.NewConnectionDialog(dialogViewModel);
            
            // Find the active window to set as owner (avoid setting owner to itself)
            var activeWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive);
            
            if (activeWindow != null && activeWindow != dialog)
            {
                dialog.Owner = activeWindow;
            }

            if (dialog.ShowDialog() == true)
            {
                var settings = dialogViewModel.GetConnectionSettings();
                
                // Save the connection
                await _connectionStore.SaveConnectionAsync(settings).ConfigureAwait(true);
                await LoadConnectionsAsync().ConfigureAwait(true);

                // Select the new connection
                var newConnection = Connections.FirstOrDefault(c => c.Id == settings.Id);
                if (newConnection != null)
                {
                    SelectedConnection = newConnection;
                }

                StatusText = $"Created connection: {settings.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connection");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync()
    {
        if (SelectedConnection is null)
        {
            StatusText = "Select a connection to delete";
            return;
        }

        var connectionName = SelectedConnection.Name;
        var connectionId = SelectedConnection.Id;

        try
        {
            // Confirm deletion
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the connection '{connectionName}'?",
                "Delete Connection",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            await _connectionStore.DeleteConnectionAsync(connectionId).ConfigureAwait(true);
            
            // Clear selection and reload
            SelectedConnection = null;
            await LoadConnectionsAsync().ConfigureAwait(true);
            
            // Clear edit fields
            EditConnectionName = string.Empty;
            EditHost = string.Empty;
            EditPort = 8182;
            EditUsername = string.Empty;
            EditSecret = string.Empty;
            EditDatabase = string.Empty;
            EditGraph = string.Empty;
            
            StatusText = $"Deleted connection: {connectionName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete connection");
            StatusText = $"Error deleting connection: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(EditConnectionName))
        {
            StatusText = "Connection name is required";
            return;
        }

        try
        {
            var metadata = SelectedConnection?.Clone() ?? new GremlinConnectionMetadata();
            metadata.Name = EditConnectionName;
            metadata.Kind = EditConnectionKind;
            metadata.Host = EditHost;
            metadata.Port = EditPort;
            metadata.Username = EditUsername;
            metadata.DatabaseName = string.IsNullOrWhiteSpace(EditDatabase) ? null : EditDatabase;
            metadata.GraphName = string.IsNullOrWhiteSpace(EditGraph) ? null : EditGraph;

            var settings = new GremlinConnectionSettings(metadata, EditSecret);
            await _connectionStore.SaveConnectionAsync(settings).ConfigureAwait(true);

            await LoadConnectionsAsync().ConfigureAwait(true);
            StatusText = $"Saved connection: {metadata.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save connection");
            StatusText = $"Error saving connection: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(EditHost))
        {
            StatusText = "Host is required to test connection";
            return;
        }

        try
        {
            StatusText = "Testing connection...";

            var metadata = new GremlinConnectionMetadata
            {
                Name = EditConnectionName,
                Kind = EditConnectionKind,
                Host = EditHost,
                Port = EditPort,
                Username = EditUsername,
                DatabaseName = string.IsNullOrWhiteSpace(EditDatabase) ? null : EditDatabase,
                GraphName = string.IsNullOrWhiteSpace(EditGraph) ? null : EditGraph
            };

            var settings = new GremlinConnectionSettings(metadata, EditSecret);
            var result = await _connectionTester.TestConnectionAsync(settings).ConfigureAwait(true);

            StatusText = result.IsSuccess
                ? $"Connection successful ({result.Latency.TotalMilliseconds:F0}ms)"
                : $"Connection failed: {result.Message}";

            LastDurationText = $"Test: {result.Latency.TotalMilliseconds:F0}ms";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connection");
            StatusText = $"Test error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportConnectionsAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = "gremlin-connections.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await _connectionExporter.ExportAsync(dialog.FileName).ConfigureAwait(true);
            StatusText = $"Exported connections to: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export connections");
            StatusText = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StartPlayground()
    {
        try
        {
            _playgroundService.Start();
            _activeConnector = _playgroundService.Connector;
            IsPlaygroundRunning = true;

            // Clear saved connection selection to indicate playground is active
            _isSyncingTabConnection = true;
            SelectedConnection = null;
            _isSyncingTabConnection = false;

            // Apply playground connector to the current tab
            if (SelectedTab != null && _playgroundService.Connector != null)
            {
                SelectedTab.UsePlaygroundConnector(_playgroundService.Connector, LoadedScenarioName);
            }

            StatusText = "Playground started";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start playground");
            StatusText = $"Error starting playground: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopPlayground()
    {
        try
        {
            _playgroundService.Stop();
            _activeConnector = null;
            IsPlaygroundRunning = false;
            LoadedScenarioName = null;

            // Clear playground from all tabs that are using it
            foreach (var tab in QueryTabs)
            {
                if (tab.IsUsingPlayground)
                {
                    tab.UsePlaygroundConnector(null, null);
                }
            }

            StatusText = "Playground stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop playground");
            StatusText = $"Error stopping playground: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadScenarioAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedScenarioName))
        {
            StatusText = "Select a scenario first";
            return;
        }

        try
        {
            // Auto-start playground if not running
            if (!IsPlaygroundRunning)
            {
                _playgroundService.Start();
                _activeConnector = _playgroundService.Connector;
                IsPlaygroundRunning = true;
            }

            // Clear saved connection selection to indicate playground/scenario is active
            _isSyncingTabConnection = true;
            SelectedConnection = null;
            _isSyncingTabConnection = false;

            _playgroundService.LoadScenario(SelectedScenarioName);
            var state = await _playgroundService.RefreshStateAsync().ConfigureAwait(true);
            
            // Update the loaded scenario name
            LoadedScenarioName = SelectedScenarioName;
            
            // Apply playground with scenario to the current tab
            if (SelectedTab != null && _playgroundService.Connector != null)
            {
                SelectedTab.UsePlaygroundConnector(_playgroundService.Connector, SelectedScenarioName);
            }
            
            StatusText = $"Loaded scenario: {SelectedScenarioName} ({state.VertexCount} vertices, {state.EdgeCount} edges)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scenario");
            StatusText = $"Error loading scenario: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetScenarioAsync()
    {
        try
        {
            // Auto-start playground if not running
            if (!IsPlaygroundRunning)
            {
                _playgroundService.Start();
                _activeConnector = _playgroundService.Connector;
                IsPlaygroundRunning = true;
            }

            _playgroundService.ResetScenario();
            var state = await _playgroundService.RefreshStateAsync().ConfigureAwait(true);
            
            // Update the loaded scenario name
            LoadedScenarioName = state.LoadedScenarioName;
            
            // Update the current tab to reflect the reset
            if (SelectedTab != null && _playgroundService.Connector != null)
            {
                SelectedTab.UsePlaygroundConnector(_playgroundService.Connector, state.LoadedScenarioName);
            }
            
            StatusText = $"Reset scenario: {state.LoadedScenarioName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset scenario");
            StatusText = $"Error resetting scenario: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunQuery))]
    private async Task RunQueryAsync()
    {
        if (SelectedTab == null)
        {
            StatusText = "No active tab";
            return;
        }

        await SelectedTab.RunQueryCommand.ExecuteAsync(null);
    }

    private bool CanRunQuery() => SelectedTab != null && !SelectedTab.IsExecuting;

    [RelayCommand]
    private void Cancel()
    {
        SelectedTab?.CancelCommand.Execute(null);
        StatusText = "Cancelling...";
    }


    [RelayCommand]
    private void ZoomIn()
    {
        if (SelectedTab != null)
            SelectedTab.ZoomInCommand.Execute(null);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (SelectedTab != null)
            SelectedTab.ZoomOutCommand.Execute(null);
    }




    [RelayCommand]
    private void ResetGraph()
    {
        if (SelectedTab != null)
            SelectedTab.ResetGraphCommand.Execute(null);
    }


    [RelayCommand]
    private void SelectGraphNode(GraphNodeViewModel? node)
    {
        SelectedTab?.SelectGraphNodeCommand.Execute(node);
    }


    [RelayCommand]
    private void SelectGraphEdge(GraphEdgeViewModel? edge)
    {
        SelectedTab?.SelectGraphEdgeCommand.Execute(edge);
    }

    /// <summary>
    /// Event raised when the settings panel should be toggled.
    /// The MainWindow handles the actual collapse/expand logic.
    /// </summary>
    public event EventHandler? ToggleSettingsPanelRequested;

    [RelayCommand]
    private void ToggleSettingsPanel()
    {
        ToggleSettingsPanelRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ShowKeyboardShortcuts()
    {
        try
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            var dialog = new Dialogs.KeyboardShortcutsDialog();
            
            if (mainWindow != null && mainWindow.IsLoaded)
            {
                dialog.Owner = mainWindow;
            }
            
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show keyboard shortcuts dialog");
        }
    }

    /// <summary>
    /// View model for the welcome guide overlay.
    /// </summary>
    public WelcomeGuideViewModel WelcomeGuide { get; }

    /// <summary>
    /// Shows the full welcome guide from the beginning.
    /// </summary>
    [RelayCommand]
    private async Task ShowWelcomeGuideAsync()
    {
        await WelcomeGuide.StartFullGuideAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Resets guide progress and restarts from the beginning.
    /// </summary>
    [RelayCommand]
    private async Task ResetWelcomeGuideAsync()
    {
        await WelcomeGuide.ResetGuideCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    #endregion

    #region Query History Commands



    [RelayCommand]
    private void TogglePinQuery(QueryHistoryItem? item)
    {
        if (item == null) return;
        
        _queryHistoryService.SetPinned(item.Id, !item.IsPinned);
        _ = _queryHistoryService.SaveAsync();
        RefreshQueryHistory();
    }

    [RelayCommand]
    private void RemoveFromHistory(QueryHistoryItem? item)
    {
        if (item == null) return;
        
        _queryHistoryService.RemoveQuery(item.Id);
        _ = _queryHistoryService.SaveAsync();
        RefreshQueryHistory();
    }

    [RelayCommand]
    private void ClearQueryHistory()
    {
        var connectionId = SelectedTab?.ConnectionMetadata?.Id;
        _queryHistoryService.ClearUnpinned(connectionId);
        _ = _queryHistoryService.SaveAsync();
        RefreshQueryHistory();
    }

    #endregion

    #region Scenario Export Commands

    [RelayCommand]
    private async Task GenerateExportPreviewAsync()
    {
        if (SelectedTab != null)
            await SelectedTab.GenerateExportPreviewCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task SaveScenarioExportAsync()
    {
        if (SelectedTab == null || !SelectedTab.HasExportData)
        {
            StatusText = "No data to export";
            return;
        }

        var extension = SelectedTab.SelectedExportFormat == ScenarioExportFormat.Json ? "json" : "cs";
        var filter = SelectedTab.SelectedExportFormat == ScenarioExportFormat.Json
            ? "JSON files (*.json)|*.json|All files (*.*)|*.*"
            : "C# files (*.cs)|*.cs|All files (*.*)|*.*";

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Scenario File",
            Filter = filter,
            FileName = $"{SelectedTab.ExportScenarioName}.{extension}",
            DefaultExt = extension
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var connector = SelectedTab.GetActiveConnector();

                var scenarioData = _scenarioExportService.ParseQueryResults(
                    SelectedTab.ResultJson,
                    SelectedTab.ExportScenarioName,
                    SelectedTab.ExportScenarioDescription,
                    SelectedTab.ConnectionSettings?.Metadata.Name,
                    SelectedTab.QueryText);

                if (connector != null && scenarioData.Vertices.Count > 0)
                {
                    var progress = new Progress<string>(message => SelectedTab.ExportStatusText = message);
                    scenarioData = await _scenarioExportService.FetchEdgesAsync(scenarioData, connector, progress, CancellationToken.None);
                }

                await _scenarioExportService.SaveAsync(scenarioData, SelectedTab.SelectedExportFormat, saveDialog.FileName);
                StatusText = $"Scenario saved to {saveDialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to save scenario: {ex.Message}";
                _logger.LogError(ex, "Failed to save scenario export");
            }
        }
    }

    [RelayCommand]
    private void CancelExport()
    {
        SelectedTab?.CancelExportCommand.Execute(null);
    }

    [RelayCommand]
    private async Task DownloadAgentSkillsAsync()
    {
        if (SelectedTab == null)
        {
            StatusText = "No active tab";
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder inside a git repository",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        var selectedDir = dialog.FolderName;

        // Resolve the correct skills directory within the git repo
        var resolution = _agentSkillsDownloadService.ResolveSkillsDirectory(selectedDir);

        if (!resolution.IsGitRepo)
        {
            System.Windows.MessageBox.Show(
                resolution.ErrorMessage ?? "The selected folder is not inside a git repository.",
                "Not a Git Repository",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);

            StatusText = "Download cancelled — not a git repository";
            return;
        }

        var targetDir = resolution.SkillsDirectory!;
        StatusText = $"Skills will be placed in {resolution.AgentConfigFolder}/skills";

        try
        {
            SelectedTab.IsDownloadingSkills = true;
            SelectedTab.SkillsDownloadStatusText = $"Downloading to {resolution.AgentConfigFolder}/skills...";

            var progress = new Progress<string>(msg =>
            {
                SelectedTab.SkillsDownloadStatusText = msg;
            });

            var result = await _agentSkillsDownloadService.DownloadSkillsAsync(
                targetDir, progress, CancellationToken.None).ConfigureAwait(true);

            if (result.IsSuccess)
            {
                SelectedTab.SkillsDownloadStatusText = $"Downloaded {result.FileCount} file(s) to {resolution.AgentConfigFolder}/skills";
                StatusText = $"Agent skills downloaded to {result.TargetDirectory}";
            }
            else
            {
                SelectedTab.SkillsDownloadStatusText = $"Failed: {result.ErrorMessage}";
                StatusText = $"Download failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            SelectedTab.SkillsDownloadStatusText = $"Error: {ex.Message}";
            StatusText = $"Download error: {ex.Message}";
            _logger.LogError(ex, "Failed to download agent skills");
        }
        finally
        {
            SelectedTab.IsDownloadingSkills = false;
        }
    }

    #endregion

    #region Node Dragging (delegates to selected tab)

    /// <summary>
    /// Starts dragging a node in the selected tab.
    /// </summary>
    public void StartNodeDrag(GraphNodeViewModel node, double startX, double startY)
    {
        SelectedTab?.StartNodeDrag(node, startX, startY);
    }

    /// <summary>
    /// Updates the position of the dragging node in the selected tab.
    /// </summary>
    public void UpdateNodeDrag(double currentX, double currentY)
    {
        SelectedTab?.UpdateNodeDrag(currentX, currentY);
    }

    /// <summary>
    /// Ends node dragging in the selected tab.
    /// </summary>
    public void EndNodeDrag()
    {
        SelectedTab?.EndNodeDrag();
    }

    #endregion

    #region Private Methods


    private async Task LoadConnectionsAsync()
    {
        try
        {
            _isLoadingConnections = true;

            var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(true);
            Connections.Clear();
            foreach (var connection in connections)
            {
                Connections.Add(connection);
            }

            if (!IsPlaygroundRunning && SelectedConnection is null)
            {
                var lastConnectionId = LoadLastConnectionPreference();
                if (!string.IsNullOrWhiteSpace(lastConnectionId))
                {
                    var match = Connections.FirstOrDefault(c => c.Id == lastConnectionId);
                    if (match != null)
                    {
                        SelectedConnection = match;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load connections");
            StatusText = $"Error loading connections: {ex.Message}";
        }
        finally
        {
            _isLoadingConnections = false;
        }
    }

    partial void OnSelectedConnectionChanged(GremlinConnectionMetadata? value)
    {
        if (value is null)
        {
            return;
        }

        // Skip if we're syncing from tab selection (avoid circular updates)
        if (_isSyncingTabConnection)
        {
            return;
        }

        if (!_isLoadingConnections)
        {
            SaveLastConnectionPreference(value.Id);
        }

        // Populate edit fields from selected connection
        EditConnectionName = value.Name;
        EditConnectionKind = value.Kind;
        EditHost = value.Host;
        EditPort = value.Port;
        EditUsername = value.Username;
        EditDatabase = value.DatabaseName ?? string.Empty;
        EditGraph = value.GraphName ?? string.Empty;

        // Load secret asynchronously and apply to current tab (always apply, even if playground is running)
        _connectionLoadingTask = LoadSelectedConnectionSecretAsync(value.Id);

        // Refresh history for the newly selected connection
        RefreshQueryHistory();
    }

    private async Task LoadSelectedConnectionSecretAsync(string connectionId)
    {
        try
        {
            var settings = await _connectionStore.GetConnectionWithSecretAsync(connectionId).ConfigureAwait(true);
            if (settings is not null)
            {
                EditSecret = settings.Secret;

                // Always apply connection to the current tab when user explicitly selects a connection
                // This allows switching from playground to saved connection seamlessly
                if (SelectedTab != null)
                {
                    SelectedTab.ApplyConnectionSettings(settings);
                }

                // Update the global connector
                _activeConnector = _connectorFactory.CreateConnector(settings);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load connection secret");
        }
    }

    private IGremlinLanguageConnector? GetActiveConnector()
    {
        // Prefer playground if running
        if (IsPlaygroundRunning && _playgroundService.Connector is not null)
        {
            return _playgroundService.Connector;
        }

        return _activeConnector;
    }

    private void OnPlaygroundStateChanged(object? sender, PlaygroundState state)
    {
        IsPlaygroundRunning = state.IsRunning;
        LoadedScenarioName = state.LoadedScenarioName;
    }

    #endregion

    #region Update Commands

    /// <summary>
    /// Checks for available updates.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateAvailable = await _updateService.CheckForUpdatesAsync().ConfigureAwait(true);
            if (!updateAvailable)
            {
                StatusText = "You are running the latest version.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            StatusText = "Failed to check for updates.";
        }
    }

    /// <summary>
    /// Downloads and applies the available update.
    /// </summary>
    [RelayCommand]
    private async Task DownloadAndApplyUpdateAsync()
    {
        try
        {
            StatusText = "Downloading update...";
            var progress = new Progress<int>(p => UpdateDownloadProgress = p);
            await _updateService.DownloadAndApplyUpdateAsync(progress).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and apply update");
            StatusText = "Failed to apply update.";
        }
    }

    /// <summary>
    /// Dismisses the update notification.
    /// </summary>
    [RelayCommand]
    private void DismissUpdate()
    {
        IsUpdateAvailable = false;
    }

    /// <summary>
    /// Toggles the update pane visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleUpdatePane()
    {
        IsUpdatePaneOpen = !IsUpdatePaneOpen;
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            // Wait a bit before checking to avoid slowing startup
            await Task.Delay(5000).ConfigureAwait(false);
            await _updateService.CheckForUpdatesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Startup update check failed (non-critical)");
        }
    }

    private void OnUpdateStateChanged(object? sender, EventArgs e)
    {
        // Update UI properties from the service state
        IsUpdateAvailable = _updateService.IsUpdateAvailable;
        UpdateVersion = _updateService.AvailableUpdate?.Version;
        IsCheckingForUpdates = _updateService.IsChecking;
        IsDownloadingUpdate = _updateService.IsDownloading;
        UpdateDownloadProgress = _updateService.DownloadProgress;

        if (IsUpdateAvailable && UpdateVersion != null)
        {
            StatusText = $"Update available: v{UpdateVersion}";
        }
    }

    #endregion
}
