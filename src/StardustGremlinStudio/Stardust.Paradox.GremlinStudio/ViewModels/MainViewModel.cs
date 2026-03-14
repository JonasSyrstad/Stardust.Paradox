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
using Stardust.Paradox.GremlinStudio.Core.Snippets;
using Stardust.Paradox.GremlinStudio.Core.Updates;
using Stardust.Paradox.GremlinStudio.Core.Storage;
using Stardust.Paradox.GremlinStudio.Core.Variables;
using Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;
using Stardust.Paradox.GremlinStudio.Dialogs;
using Stardust.Paradox.GremlinStudio.Editor;
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
    private readonly IQuerySnippetStore _snippetStore;
    private readonly IQueryVariableStore _variableStore;
    private readonly ILogger<MainViewModel> _logger;

    private IGremlinLanguageConnector? _activeConnector;
    private CancellationTokenSource? _queryCts;
    private CancellationTokenSource? _exportCts;
    private Task? _connectionLoadingTask;

    /// <summary>
    /// Tracks InMemory connectors by connection ID so each InMemory connection maintains its own state.
    /// </summary>
    private readonly Dictionary<string, IGremlinLanguageConnector> _inMemoryConnectors = new();

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
        IQuerySnippetStore snippetStore,
        IQueryVariableStore variableStore,
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
        _snippetStore = snippetStore;
        _variableStore = variableStore;
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
        await LoadSnippetsAsync().ConfigureAwait(true);
        await LoadVariableSetsAsync().ConfigureAwait(true);
    }

    private async Task LoadQueryHistoryAsync()
    {
        await _queryHistoryService.LoadAsync();
        RefreshQueryHistory();
    }

    private bool _isRefreshingHistory;

    private void RefreshQueryHistory()
    {
        RefreshQueryHistoryForConnection(SelectedTab?.ConnectionMetadata?.Id);
    }

    private void RefreshQueryHistoryForConnection(string? connectionId)
    {
        // Guard: prevent ComboBox auto-selection from resetting QueryText
        // when the collection changes.
        _isRefreshingHistory = true;
        try
        {
            var items = _queryHistoryService.GetHistory(connectionId);

            // Update the existing collection in-place rather than replacing it.
            // Replacing the ObservableCollection while the ComboBox popup is open
            // can crash WPF's ItemContainerGenerator.
            QueryHistory.Clear();
            foreach (var item in items)
            {
                QueryHistory.Add(item);
            }
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

        // Update connection-type-related properties
        OnPropertyChanged(nameof(IsSelectedConnectionInMemory));
        OnPropertyChanged(nameof(IsSelectedConnectionCosmosDb));
        OnPropertyChanged(nameof(SelectedConnectionScenarioName));

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

    /// <summary>
    /// Whether the currently selected connection is an InMemory playground connection.
    /// </summary>
    public bool IsSelectedConnectionInMemory => SelectedConnection?.Kind == GremlinConnectionKind.InMemory;

    /// <summary>
    /// Whether the currently selected connection is a Cosmos DB connection.
    /// </summary>
    public bool IsSelectedConnectionCosmosDb => SelectedConnection?.Kind == GremlinConnectionKind.CosmosDb;

    /// <summary>
    /// Scenario name for the currently selected InMemory connection.
    /// </summary>
    public string? SelectedConnectionScenarioName => SelectedConnection?.ScenarioName;


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
        if (SelectedConnection != null)
        {
            if (SelectedConnection.Kind == GremlinConnectionKind.InMemory)
            {
                // InMemory connections don't need secret loading; handled by CreateNewTab
                CreateNewTab();
                return;
            }

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

        // If the selected connection is InMemory, apply its connector to the new tab
        if (SelectedConnection?.Kind == GremlinConnectionKind.InMemory
            && _inMemoryConnectors.TryGetValue(SelectedConnection.Id, out var inMemoryConnector))
        {
            var settings = new GremlinConnectionSettings(SelectedConnection, string.Empty);
            tab.ApplyInMemoryConnection(settings, inMemoryConnector);
        }
        // Legacy: if playground is running, use it for the new tab
        else if (IsPlaygroundRunning && _playgroundService.Connector != null)
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
            var dialogViewModel = new NewConnectionDialogViewModel(_discoveryService, _playgroundService);
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

    /// <summary>
    /// Opens a dialog to edit the currently selected connection's settings.
    /// </summary>
    [RelayCommand]
    private async Task EditConnectionAsync()
    {
        if (SelectedConnection is null)
        {
            StatusText = "Select a connection to edit";
            return;
        }

        try
        {
            // Load the secret for the selected connection
            var secret = EditSecret;
            if (SelectedConnection.Kind != GremlinConnectionKind.InMemory && string.IsNullOrEmpty(secret))
            {
                var existing = await _connectionStore.GetConnectionWithSecretAsync(SelectedConnection.Id).ConfigureAwait(true);
                if (existing is not null)
                {
                    secret = existing.Secret;
                }
            }

            var dialogViewModel = new EditConnectionDialogViewModel(SelectedConnection, secret, _connectionTester);
            var dialog = new EditConnectionDialog(dialogViewModel);

            var activeWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive);

            if (activeWindow != null && activeWindow != dialog)
            {
                dialog.Owner = activeWindow;
            }

            if (dialog.ShowDialog() == true)
            {
                var updatedSettings = dialogViewModel.GetUpdatedSettings();
                await _connectionStore.SaveConnectionAsync(updatedSettings).ConfigureAwait(true);

                var savedId = updatedSettings.Id;
                await LoadConnectionsAsync().ConfigureAwait(true);

                // Re-select the edited connection
                var match = Connections.FirstOrDefault(c => c.Id == savedId);
                if (match != null)
                {
                    SelectedConnection = match;
                }

                StatusText = $"Updated connection: {updatedSettings.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit connection");
            StatusText = $"Error editing connection: {ex.Message}";
        }
    }

    /// <summary>
    /// Clones the selected Cosmos DB connection, allowing the user to pick a different graph
    /// in the same database account.
    /// </summary>
    [RelayCommand]
    private async Task CloneConnectionAsync()
    {
        if (SelectedConnection is null || SelectedConnection.Kind != GremlinConnectionKind.CosmosDb)
        {
            StatusText = "Select a Cosmos DB connection to clone";
            return;
        }

        try
        {
            // Load the secret for the source connection
            var sourceSettings = await _connectionStore.GetConnectionWithSecretAsync(SelectedConnection.Id).ConfigureAwait(true);
            if (sourceSettings is null)
            {
                StatusText = "Failed to load connection details for cloning";
                return;
            }

            var sourceMetadata = sourceSettings.Metadata;

            // Pre-populate the new connection dialog with the source connection's details
            var dialogViewModel = new NewConnectionDialogViewModel(_discoveryService, _playgroundService);
            dialogViewModel.PrepopulateFromCosmosDb(sourceMetadata, sourceSettings.Secret);

            var dialog = new Dialogs.NewConnectionDialog(dialogViewModel);

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

                await _connectionStore.SaveConnectionAsync(settings).ConfigureAwait(true);
                await LoadConnectionsAsync().ConfigureAwait(true);

                var newConnection = Connections.FirstOrDefault(c => c.Id == settings.Id);
                if (newConnection != null)
                {
                    SelectedConnection = newConnection;
                }

                StatusText = $"Cloned connection: {settings.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clone connection");
            StatusText = $"Error cloning connection: {ex.Message}";
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
    private async Task ImportConnectionsAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Import Connections"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var imported = await _connectionExporter.ImportAsync(dialog.FileName).ConfigureAwait(true);

            if (imported.Count == 0)
            {
                StatusText = "No connections found in file";
                return;
            }

            foreach (var metadata in imported)
            {
                var settings = new GremlinConnectionSettings(metadata, string.Empty);
                await _connectionStore.SaveConnectionAsync(settings).ConfigureAwait(true);
            }

            await LoadConnectionsAsync().ConfigureAwait(true);
            StatusText = $"Imported {imported.Count} connection(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import connections");
            StatusText = $"Import error: {ex.Message}";
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

    /// <summary>
    /// Resets the currently selected InMemory connection to its initial state.
    /// If a scenario was configured, the scenario data is reloaded. Otherwise resets to an empty graph.
    /// </summary>
    [RelayCommand]
    private void ResetInMemoryConnection()
    {
        if (SelectedConnection is null || SelectedConnection.Kind != GremlinConnectionKind.InMemory)
        {
            StatusText = "Select an InMemory connection to reset";
            return;
        }

        try
        {
            var connectionId = SelectedConnection.Id;

            // Dispose old connector if it exists
            if (_inMemoryConnectors.TryGetValue(connectionId, out var oldConnector))
            {
                if (oldConnector is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _inMemoryConnectors.Remove(connectionId);
            }

            // Re-create the connector (with scenario if applicable)
            var settings = new GremlinConnectionSettings(SelectedConnection, string.Empty);
            var newConnector = _connectorFactory.CreateConnector(settings);
            _inMemoryConnectors[connectionId] = newConnector;
            _activeConnector = newConnector;

            // Update the current tab
            if (SelectedTab != null)
            {
                SelectedTab.ApplyInMemoryConnection(settings, newConnector);
            }

            StatusText = string.IsNullOrEmpty(SelectedConnection.ScenarioName)
                ? $"Reset: {SelectedConnection.Name} (empty)"
                : $"Reset: {SelectedConnection.Name} (scenario: {SelectedConnection.ScenarioName})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset InMemory connection");
            StatusText = $"Error resetting: {ex.Message}";
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

        // Resolve variable substitutions before execution.
        // The original query (with ${var} tokens) is kept for history;
        // the resolved query is used for execution and the log.
        string? resolvedQuery = null;
        if (SelectedVariableSet != null && !string.IsNullOrWhiteSpace(SelectedTab.QueryText))
        {
            try
            {
                var mergedJson = SelectedVariableSet.Set.GetMergedJson(SelectedConnection?.Id);
                var substituted = QueryVariableSubstitutor.Substitute(SelectedTab.QueryText, mergedJson);
                if (substituted != SelectedTab.QueryText)
                {
                    resolvedQuery = substituted;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve variables");
                StatusText = $"Variable resolution failed: {ex.Message}";
                return;
            }
        }

        await SelectedTab.ExecuteQueryAsync(resolvedQuery);
    }

    private bool CanRunQuery() => SelectedTab != null && !SelectedTab.IsExecuting;

    [RelayCommand]
    private void Cancel()
    {
        SelectedTab?.CancelCommand.Execute(null);
        StatusText = "Cancelling...";
    }

    #region Query File Save/Open

    [RelayCommand]
    private async Task SaveQueryAsync()
    {
        if (SelectedTab == null) return;

        // If already has a file path, save directly
        if (!string.IsNullOrEmpty(SelectedTab.QueryFilePath))
        {
            await SaveQueryToFileAsync(SelectedTab.QueryFilePath).ConfigureAwait(true);
            return;
        }

        // Otherwise show Save As dialog
        await SaveQueryAsAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveQueryAsAsync()
    {
        if (SelectedTab == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Save Query",
            Filter = "Gremlin files (*.gremlin)|*.gremlin|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".gremlin",
            FileName = string.IsNullOrEmpty(SelectedTab.QueryFilePath)
                ? "query.gremlin"
                : System.IO.Path.GetFileName(SelectedTab.QueryFilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            await SaveQueryToFileAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private async Task SaveQueryToFileAsync(string filePath)
    {
        try
        {
            await File.WriteAllTextAsync(filePath, SelectedTab!.QueryText).ConfigureAwait(true);
            SelectedTab.QueryFilePath = filePath;
            SelectedTab.IsDirty = false;
            StatusText = $"Saved: {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save query file");
            StatusText = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenQueryAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Query",
            Filter = "Gremlin files (*.gremlin)|*.gremlin|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".gremlin"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var content = await File.ReadAllTextAsync(dialog.FileName).ConfigureAwait(true);

            // Open in current tab if it's empty/default, otherwise create new tab
            if (SelectedTab != null && !SelectedTab.IsDirty && SelectedTab.QueryText == "g.V().limit(10)")
            {
                SelectedTab.QueryText = content;
                SelectedTab.QueryFilePath = dialog.FileName;
                SelectedTab.IsDirty = false;
            }
            else
            {
                var tab = CreateNewTab(SelectedTab?.ConnectionSettings);
                tab.QueryText = content;
                tab.QueryFilePath = dialog.FileName;
                tab.IsDirty = false;
            }

            StatusText = $"Opened: {System.IO.Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open query file");
            StatusText = $"Open error: {ex.Message}";
        }
    }

    #endregion


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
    private void TogglePinQuery(object? parameter)
    {
        if (parameter is not QueryHistoryItem item) return;

        _queryHistoryService.SetPinned(item.Id, !item.IsPinned);
        _ = _queryHistoryService.SaveAsync();
        RefreshQueryHistory();
    }

    [RelayCommand]
    private void RemoveFromHistory(object? parameter)
    {
        if (parameter is not QueryHistoryItem item) return;

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
        OnPropertyChanged(nameof(IsSelectedConnectionInMemory));
        OnPropertyChanged(nameof(IsSelectedConnectionCosmosDb));
        OnPropertyChanged(nameof(SelectedConnectionScenarioName));

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

        if (value.Kind == GremlinConnectionKind.InMemory)
        {
            // For InMemory connections, create or reuse a per-connection connector
            _connectionLoadingTask = ApplyInMemoryConnectionAsync(value);
        }
        else
        {
            // Load secret asynchronously and apply to current tab (always apply, even if playground is running)
            _connectionLoadingTask = LoadSelectedConnectionSecretAsync(value.Id);
        }

        // Refresh history for the newly selected connection
        RefreshQueryHistoryForConnection(value.Id);

        // Reset and reload statistics for the new connection
        ResetAndRefreshStatistics();

        // Update variable context for the new connection's overrides
        RefreshActiveVariables();
    }

    private Task ApplyInMemoryConnectionAsync(GremlinConnectionMetadata metadata)
    {
        if (!_inMemoryConnectors.TryGetValue(metadata.Id, out var connector))
        {
            // Create a new InMemory connector with its scenario
            var settings = new GremlinConnectionSettings(metadata, string.Empty);
            connector = _connectorFactory.CreateConnector(settings);
            _inMemoryConnectors[metadata.Id] = connector;
        }

        _activeConnector = connector;
        EditSecret = string.Empty;

        if (SelectedTab != null)
        {
            var settings = new GremlinConnectionSettings(metadata, string.Empty);
            SelectedTab.ApplyInMemoryConnection(settings, connector);
        }

        StatusText = string.IsNullOrEmpty(metadata.ScenarioName)
            ? $"Connected: {metadata.Name} (empty playground)"
            : $"Connected: {metadata.Name} (scenario: {metadata.ScenarioName})";

        return Task.CompletedTask;
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
        // Prefer InMemory connection if selected
        if (SelectedConnection?.Kind == GremlinConnectionKind.InMemory
            && _inMemoryConnectors.TryGetValue(SelectedConnection.Id, out var inMemoryConnector))
        {
            return inMemoryConnector;
        }

        // Fallback: legacy playground if running
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

    #region Snippets

    /// <summary>
    /// Collection of saved query snippets.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SnippetItemViewModel> _snippets = new();

    /// <summary>
    /// The currently selected snippet.
    /// </summary>
    [ObservableProperty]
    private SnippetItemViewModel? _selectedSnippet;

    private async Task LoadSnippetsAsync()
    {
        try
        {
            var all = await _snippetStore.GetAllAsync().ConfigureAwait(true);
            Snippets.Clear();
            foreach (var snippet in all)
            {
                Snippets.Add(new SnippetItemViewModel(snippet));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load snippets");
        }
    }

    /// <summary>
    /// Saves the current query as a new snippet, prompting for a name via dialog.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsSnippetAsync()
    {
        if (SelectedTab == null || string.IsNullOrWhiteSpace(SelectedTab.QueryText))
        {
            StatusText = "Enter a query first";
            return;
        }

        try
        {
            var dialog = new Dialogs.SnippetNameDialog();

            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.IsLoaded)
            {
                dialog.Owner = mainWindow;
            }

            // Suggest a name from the first line of the query
            var firstLine = SelectedTab.QueryText.Split('\n')[0].Trim();
            if (firstLine.Length > 50)
                firstLine = firstLine[..50] + "...";
            dialog.SetSuggestedName(firstLine);

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SnippetName))
                return;

            var snippet = new QuerySnippet(
                Guid.NewGuid().ToString(),
                dialog.SnippetName,
                SelectedTab.QueryText,
                Array.Empty<string>(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            await _snippetStore.SaveAsync(snippet).ConfigureAwait(true);
            await LoadSnippetsAsync().ConfigureAwait(true);
            StatusText = $"Saved snippet: {dialog.SnippetName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save snippet");
            StatusText = $"Error saving snippet: {ex.Message}";
        }
    }

    /// <summary>
    /// Inserts the selected snippet into the current query editor.
    /// </summary>
    [RelayCommand]
    private void InsertSnippet()
    {
        if (SelectedSnippet == null || SelectedTab == null) return;
        SelectedTab.QueryText = SelectedSnippet.Query;
        StatusText = $"Inserted snippet: {SelectedSnippet.Name}";
    }

    /// <summary>
    /// Deletes the selected snippet.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSnippetAsync()
    {
        if (SelectedSnippet == null)
        {
            StatusText = "Select a snippet to delete";
            return;
        }

        try
        {
            var name = SelectedSnippet.Name;
            await _snippetStore.DeleteAsync(SelectedSnippet.Id).ConfigureAwait(true);
            await LoadSnippetsAsync().ConfigureAwait(true);
            StatusText = $"Deleted snippet: {name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete snippet");
            StatusText = $"Error deleting snippet: {ex.Message}";
        }
    }

    #endregion

    #region Variables

    /// <summary>
    /// Collection of saved variable sets.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<VariableSetItemViewModel> _variableSets = new();

    /// <summary>
    /// The currently selected variable set.
    /// </summary>
    [ObservableProperty]
    private VariableSetItemViewModel? _selectedVariableSet;

    partial void OnSelectedVariableSetChanged(VariableSetItemViewModel? value)
    {
        SaveLastVariableSetPreference(value?.Id);
        RefreshActiveVariables();
    }

    /// <summary>
    /// Rebuilds the active variable context and pushes it to the editor's
    /// autocomplete and tooltip provider. Merges global variables with
    /// connection-specific overrides for the active connection.
    /// </summary>
    private void RefreshActiveVariables()
    {
        var variableSet = SelectedVariableSet?.Set;
        if (variableSet == null)
        {
            GremlinCompletionProvider.SetActiveVariables(
                Array.Empty<(string, string, bool)>());
            return;
        }

        var connectionId = SelectedConnection?.Id;
        var entries = new List<(string Key, string Value, bool IsConnectionScoped)>();

        // Parse global variables
        ParseJsonVariables(variableSet.Json, isConnectionScoped: false, entries);

        // Parse connection-scoped variables (overrides globals with same key)
        if (!string.IsNullOrWhiteSpace(connectionId)
            && variableSet.ConnectionVariables != null
            && variableSet.ConnectionVariables.TryGetValue(connectionId, out var connJson))
        {
            ParseJsonVariables(connJson, isConnectionScoped: true, entries);
        }

        GremlinCompletionProvider.SetActiveVariables(entries);
    }

    private static void ParseJsonVariables(
        string? json,
        bool isConnectionScoped,
        List<(string Key, string Value, bool IsConnectionScoped)> entries)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // If a connection-scoped variable overrides a global, remove the global
                // and any nested paths under it
                if (isConnectionScoped)
                {
                    entries.RemoveAll(e =>
                        string.Equals(e.Key, prop.Name, StringComparison.Ordinal) ||
                        e.Key.StartsWith(prop.Name + ".", StringComparison.Ordinal));
                }

                var value = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.GetRawText();

                entries.Add((prop.Name, value, isConnectionScoped));

                // Recursively flatten nested objects into dotted-path entries
                // so autocomplete can suggest paths like "server.host"
                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    FlattenJsonObject(prop.Value, prop.Name, isConnectionScoped, entries);
                }
            }
        }
        catch
        {
            // Ignore malformed JSON
        }
    }

    private static void FlattenJsonObject(
        System.Text.Json.JsonElement element,
        string prefix,
        bool isConnectionScoped,
        List<(string Key, string Value, bool IsConnectionScoped)> entries)
    {
        foreach (var prop in element.EnumerateObject())
        {
            var key = $"{prefix}.{prop.Name}";

            var value = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                ? prop.Value.GetString() ?? string.Empty
                : prop.Value.GetRawText();

            entries.Add((key, value, isConnectionScoped));

            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                FlattenJsonObject(prop.Value, key, isConnectionScoped, entries);
            }
        }
    }

    private async Task LoadVariableSetsAsync()
    {
        try
        {
            var all = await _variableStore.GetAllAsync().ConfigureAwait(true);
            VariableSets.Clear();
            foreach (var v in all)
            {
                VariableSets.Add(new VariableSetItemViewModel(v));
            }

            // Auto-select the last-used variable set (or the first available)
            if (SelectedVariableSet is null && VariableSets.Count > 0)
            {
                var lastId = LoadLastVariableSetPreference();
                var match = !string.IsNullOrWhiteSpace(lastId)
                    ? VariableSets.FirstOrDefault(v => v.Id == lastId)
                    : null;
                SelectedVariableSet = match ?? VariableSets[0];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load variable sets");
        }
    }

    private static void SaveLastVariableSetPreference(string? variableSetId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(variableSetId))
            {
                if (File.Exists(AppDataPaths.LastVariableSetPreferenceFilePath))
                {
                    File.Delete(AppDataPaths.LastVariableSetPreferenceFilePath);
                }
                return;
            }

            File.WriteAllText(AppDataPaths.LastVariableSetPreferenceFilePath, variableSetId);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private static string? LoadLastVariableSetPreference()
    {
        try
        {
            if (!File.Exists(AppDataPaths.LastVariableSetPreferenceFilePath))
                return null;

            var id = File.ReadAllText(AppDataPaths.LastVariableSetPreferenceFilePath).Trim();
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Opens the variable set editor dialog to create a new variable set.
    /// </summary>
    [RelayCommand]
    private async Task NewVariableSetAsync()
    {
        try
        {
            var dialogVm = new VariableSetDialogViewModel(Connections.ToList());
            var dialog = new VariableSetDialog(dialogVm);

            var activeWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
            dialog.Owner = activeWindow;

            if (dialog.ShowDialog() == true)
            {
                var result = dialogVm.BuildResult();
                await _variableStore.SaveAsync(result).ConfigureAwait(true);
                await LoadVariableSetsAsync().ConfigureAwait(true);
                SelectedVariableSet = VariableSets.FirstOrDefault(v => v.Id == result.Id);
                StatusText = $"Created variable set: {result.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create variable set");
            StatusText = $"Error creating variable set: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the variable set editor dialog to edit the selected variable set.
    /// </summary>
    [RelayCommand]
    private async Task EditVariableSetAsync()
    {
        if (SelectedVariableSet == null)
        {
            StatusText = "Select a variable set to edit";
            return;
        }

        try
        {
            var dialogVm = new VariableSetDialogViewModel(Connections.ToList(), SelectedVariableSet.Set);
            var dialog = new VariableSetDialog(dialogVm);

            var activeWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
            dialog.Owner = activeWindow;

            if (dialog.ShowDialog() == true)
            {
                var result = dialogVm.BuildResult();
                await _variableStore.SaveAsync(result).ConfigureAwait(true);
                await LoadVariableSetsAsync().ConfigureAwait(true);
                SelectedVariableSet = VariableSets.FirstOrDefault(v => v.Id == result.Id);
                StatusText = $"Updated variable set: {result.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit variable set");
            StatusText = $"Error editing variable set: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes the selected variable set.
    /// </summary>
    [RelayCommand]
    private async Task DeleteVariableSetAsync()
    {
        if (SelectedVariableSet == null)
        {
            StatusText = "Select a variable set to delete";
            return;
        }

        try
        {
            var name = SelectedVariableSet.Name;

            var result = System.Windows.MessageBox.Show(
                $"Delete variable set '{name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            await _variableStore.DeleteAsync(SelectedVariableSet.Id).ConfigureAwait(true);
            await LoadVariableSetsAsync().ConfigureAwait(true);
            StatusText = $"Deleted variables: {name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete variable set");
            StatusText = $"Error deleting variables: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates variable resolution and previews the substituted query in the status bar.
    /// The <c>${var}</c> tokens in the editor are preserved; actual substitution happens
    /// at execution time so that history retains the original query template.
    /// </summary>
    [RelayCommand]
    private void ApplyVariables()
    {
        if (SelectedVariableSet == null)
        {
            StatusText = "Select a variable set first";
            return;
        }

        if (SelectedTab == null || string.IsNullOrWhiteSpace(SelectedTab.QueryText))
        {
            StatusText = "Enter a query first";
            return;
        }

        try
        {
            var mergedJson = SelectedVariableSet.Set.GetMergedJson(SelectedConnection?.Id);
            var substituted = QueryVariableSubstitutor.Substitute(SelectedTab.QueryText, mergedJson);

            if (substituted == SelectedTab.QueryText)
            {
                StatusText = "No ${var} tokens found in query";
                return;
            }

            // Show a preview without modifying the editor text
            var preview = substituted.Length > 120
                ? substituted[..120] + "…"
                : substituted;
            StatusText = $"Preview: {preview}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error resolving variables: {ex.Message}";
        }
    }

    #endregion

    #region Undo / Redo

    /// <summary>
    /// Undoes the last edit in the active query editor.
    /// </summary>
    [RelayCommand]
    private void UndoQuery()
    {
        Controls.TextEditorHelper.Undo();
    }

    /// <summary>
    /// Redoes the last undone edit in the active query editor.
    /// </summary>
    [RelayCommand]
    private void RedoQuery()
    {
        Controls.TextEditorHelper.Redo();
    }

    #endregion

    #region Query Formatting

    /// <summary>
    /// Formats the current query with proper indentation.
    /// </summary>
    [RelayCommand]
    private void FormatQuery()
    {
        if (SelectedTab == null || string.IsNullOrWhiteSpace(SelectedTab.QueryText))
        {
            StatusText = "Enter a query first";
            return;
        }


        SelectedTab.QueryText = GremlinQueryFormatter.Format(SelectedTab.QueryText);
        StatusText = "Query formatted";
    }

    /// <summary>
    /// Resolves a parameterized query (from Stardust ORM logging) by inlining the
    /// __pN parameter values from the accompanying JSON map.
    /// </summary>
    [RelayCommand]
    private void ResolveParameters()
    {
        if (SelectedTab == null || string.IsNullOrWhiteSpace(SelectedTab.QueryText))
        {
            StatusText = "Enter a query first";
            return;
        }

        if (!ParameterizedQueryResolver.IsParameterizedQuery(SelectedTab.QueryText))
        {
            StatusText = "No parameterized query detected – expected __pN tokens followed by a JSON block";
            return;
        }

        SelectedTab.QueryText = ParameterizedQueryResolver.Resolve(SelectedTab.QueryText);
        StatusText = "Parameters resolved";
    }

    /// <summary>
    /// Generates a detailed explanation of the current query and displays it in the Explain tab.
    /// </summary>
    /// <summary>
    /// Tab index of the Explain results tab inside the results TabControl.
    /// </summary>
    private const int ExplainTabIndex = 6;

    [RelayCommand]
    private void ExplainQuery()
    {
        if (SelectedTab == null || string.IsNullOrWhiteSpace(SelectedTab.QueryText))
        {
            StatusText = "Enter a query first";
            return;
        }

        SelectedTab.QueryExplanation = GremlinQueryExplainer.Explain(SelectedTab.QueryText);
        SelectedTab.SelectedResultViewIndex = ExplainTabIndex;
        StatusText = "Query explained";
    }

    #endregion

    #region Database Statistics

    /// <summary>
    /// Resets statistics on the current tab and triggers a background refresh
    /// after the connection has been fully established.
    /// </summary>
    private void ResetAndRefreshStatistics()
    {
        if (SelectedTab == null) return;

        // Clear stale statistics immediately
        SelectedTab.HasStatistics = false;
        SelectedTab.TotalVertexCount = 0;
        SelectedTab.TotalEdgeCount = 0;
        SelectedTab.VertexLabelCounts.Clear();
        SelectedTab.EdgeLabelCounts.Clear();
        SelectedTab.StatisticsStatusText = string.Empty;

        // Wait for connection to be ready, then load stats
        _ = RefreshStatisticsAfterConnectionAsync();
    }

    private async Task RefreshStatisticsAfterConnectionAsync()
    {
        try
        {
            // Wait for any pending connection loading to complete
            if (_connectionLoadingTask != null)
            {
                await _connectionLoadingTask.ConfigureAwait(true);
            }

            await LoadDatabaseStatisticsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-refresh statistics after connection change");
        }
    }

    /// <summary>
    /// Loads database statistics from the active connection.
    /// Uses two-phase loading: counts first (fast) then labels (slower).
    /// </summary>
    [RelayCommand]
    private async Task LoadDatabaseStatisticsAsync()
    {
        var tab = SelectedTab;
        if (tab == null)
        {
            StatusText = "No active tab";
            return;
        }

        var connector = tab.GetActiveConnector();
        if (connector == null)
        {
            StatusText = "No active connection";
            tab.StatisticsStatusText = "Connect to a database first";
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var errors = new List<string>();

        try
        {
            tab.IsLoadingStatistics = true;

            // Phase 1: counts — these are fast single-value queries
            tab.StatisticsStatusText = "Loading vertex count...";
            var vCountResult = await _queryExecutor.ExecuteAsync(connector, "g.V().count()", cancellationToken: cts.Token).ConfigureAwait(true);
            tab.TotalVertexCount = vCountResult.IsSuccess ? ParseCount(vCountResult.ResultJson) : 0;
            if (!vCountResult.IsSuccess)
                errors.Add($"Vertex count: {vCountResult.ErrorMessage}");

            tab.StatisticsStatusText = "Loading edge count...";
            var eCountResult = await _queryExecutor.ExecuteAsync(connector, "g.E().count()", cancellationToken: cts.Token).ConfigureAwait(true);
            tab.TotalEdgeCount = eCountResult.IsSuccess ? ParseCount(eCountResult.ResultJson) : 0;
            if (!eCountResult.IsSuccess)
                errors.Add($"Edge count: {eCountResult.ErrorMessage}");

            // Show counts immediately so the user sees progress
            tab.HasStatistics = true;
            tab.StatisticsStatusText = $"{tab.TotalVertexCount:N0} vertices, {tab.TotalEdgeCount:N0} edges — loading labels...";

            // Phase 2: label distributions.
            // For Cosmos DB, use a hybrid approach:
            //   - Gremlin dedup() to get distinct labels (cheap, small result set)
            //   - SQL API SELECT VALUE COUNT(1) per label (gateway-supported cross-partition count)
            // For other connections, use Gremlin groupCount() directly.
            if (tab.ConnectionSettings?.Kind == GremlinConnectionKind.CosmosDb)
            {
                tab.StatisticsStatusText = "Loading labels (SQL API)...";
                try
                {
                    // Run vertex and edge label loading concurrently — pure SQL API, no Gremlin
                    var vertexTask = LoadLabelCountsViaSqlApiAsync(tab.ConnectionSettings, isEdge: false, cts.Token);
                    var edgeTask = LoadLabelCountsViaSqlApiAsync(tab.ConnectionSettings, isEdge: true, cts.Token);

                    await Task.WhenAll(vertexTask, edgeTask).ConfigureAwait(true);

                    tab.VertexLabelCounts = new ObservableCollection<LabelCountItem>(vertexTask.Result);
                    tab.EdgeLabelCounts = new ObservableCollection<LabelCountItem>(edgeTask.Result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Hybrid label count failed, falling back to Gremlin");

                    tab.StatisticsStatusText = "Loading labels (Gremlin fallback)...";
                    var vFallback = await _queryExecutor.ExecuteAsync(connector, "g.V().groupCount().by(label)", cancellationToken: cts.Token).ConfigureAwait(true);
                    if (vFallback.IsSuccess)
                        tab.VertexLabelCounts = new ObservableCollection<LabelCountItem>(ParseGroupCount(vFallback.ResultJson));
                    else
                        errors.Add($"Vertex labels: {vFallback.ErrorMessage}");

                    var eFallback = await _queryExecutor.ExecuteAsync(connector, "g.E().groupCount().by(label)", cancellationToken: cts.Token).ConfigureAwait(true);
                    if (eFallback.IsSuccess)
                        tab.EdgeLabelCounts = new ObservableCollection<LabelCountItem>(ParseGroupCount(eFallback.ResultJson));
                    else
                        errors.Add($"Edge labels: {eFallback.ErrorMessage}");
                }
            }
            else
            {
                tab.StatisticsStatusText = "Loading vertex labels...";
                var vLabelsResult = await _queryExecutor.ExecuteAsync(connector, "g.V().groupCount().by(label)", cancellationToken: cts.Token).ConfigureAwait(true);
                if (vLabelsResult.IsSuccess)
                {
                    tab.VertexLabelCounts = new ObservableCollection<LabelCountItem>(ParseGroupCount(vLabelsResult.ResultJson));
                }
                else
                {
                    _logger.LogWarning("Vertex labels query failed: {Error}", vLabelsResult.ErrorMessage);
                    errors.Add($"Vertex labels: {vLabelsResult.ErrorMessage}");
                }

                tab.StatisticsStatusText = "Loading edge labels...";
                var eLabelsResult = await _queryExecutor.ExecuteAsync(connector, "g.E().groupCount().by(label)", cancellationToken: cts.Token).ConfigureAwait(true);
                if (eLabelsResult.IsSuccess)
                {
                    tab.EdgeLabelCounts = new ObservableCollection<LabelCountItem>(ParseGroupCount(eLabelsResult.ResultJson));
                }
                else
                {
                    _logger.LogWarning("Edge labels query failed: {Error}", eLabelsResult.ErrorMessage);
                    errors.Add($"Edge labels: {eLabelsResult.ErrorMessage}");
                }
            }

            if (errors.Count > 0)
            {
                tab.StatisticsStatusText = $"{tab.TotalVertexCount:N0} vertices, {tab.TotalEdgeCount:N0} edges (partial — {errors.Count} query failed)";
                StatusText = $"Statistics loaded with errors: {string.Join("; ", errors)}";
            }
            else
            {
                tab.StatisticsStatusText = $"Loaded: {tab.TotalVertexCount:N0} vertices, {tab.TotalEdgeCount:N0} edges";
                StatusText = tab.StatisticsStatusText;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Database statistics query timed out");
            tab.StatisticsStatusText = "Timed out — try again or check connection";
            StatusText = "Statistics query timed out";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database statistics");
            tab.StatisticsStatusText = $"Error: {ex.Message}";
            StatusText = $"Statistics error: {ex.Message}";
        }
        finally
        {
            tab.IsLoadingStatistics = false;
        }
    }

    private static long ParseCount(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            // Results come as a JSON array, e.g. [4] or [{"@type":"g:Int64","@value":4}]
            var arr = Newtonsoft.Json.JsonConvert.DeserializeObject<List<object>>(json);
            if (arr != null && arr.Count > 0)
            {
                return Convert.ToInt64(arr[0]);
            }
        }
        catch { }
        return 0;
    }

    private static List<LabelCountItem> ParseGroupCount(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            // Results come as [{"person":3,"software":2}] or similar map structure
            var arr = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(json);
            if (arr != null && arr.Count > 0)
            {
                return arr[0].Properties()
                    .Select(p => new LabelCountItem(p.Name, Convert.ToInt64(p.Value)))
                    .OrderByDescending(x => x.Count)
                    .ToList();
            }
        }
        catch { }
        return new();
    }

    /// <summary>
    /// Loads label counts entirely via SQL API — no Gremlin required.
    /// Uses per-partition GROUP BY queries, then merges results client-side.
    /// </summary>
    private async Task<List<LabelCountItem>> LoadLabelCountsViaSqlApiAsync(
        GremlinConnectionSettings settings,
        bool isEdge,
        CancellationToken cancellationToken)
    {
        var (endpoint, database, container) = ResolveCosmosDbSqlEndpoint(settings);
        var counts = await _discoveryService.DiscoverLabelCountsAsync(
            endpoint, settings.Secret, database, container, isEdge, cancellationToken).ConfigureAwait(true);

        return counts
            .Select(kvp => new LabelCountItem(kvp.Key, kvp.Value))
            .OrderByDescending(x => x.Count)
            .ToList();
    }

    /// <summary>
    /// Converts a Gremlin connection to the Cosmos DB documents endpoint, database, and container.
    /// </summary>
    private static (string Endpoint, string Database, string Container) ResolveCosmosDbSqlEndpoint(
        GremlinConnectionSettings settings)
    {
        var documentsHost = settings.Host.Replace(".gremlin.cosmos.azure.com", ".documents.azure.com");
        var endpoint = $"https://{documentsHost}";
        var database = settings.DatabaseName
            ?? throw new InvalidOperationException("Database name is required for Cosmos DB SQL API queries");
        var container = settings.GraphName
            ?? throw new InvalidOperationException("Graph name is required for Cosmos DB SQL API queries");
        return (endpoint, database, container);
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
