using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Playground;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// ViewModel for the new connection wizard dialog.
/// </summary>
public partial class NewConnectionDialogViewModel : ObservableObject
{
    private readonly ICosmosDbDiscoveryService _discoveryService;
    private readonly IPlaygroundService _playgroundService;
    private int _currentStep = 1;
    private string _parsedEndpoint = string.Empty;
    private string _parsedKey = string.Empty;

    public NewConnectionDialogViewModel(ICosmosDbDiscoveryService discoveryService, IPlaygroundService playgroundService)
    {
        _discoveryService = discoveryService;
        _playgroundService = playgroundService;
        _availableDatabases = new ObservableCollection<string>();
        _availableGraphs = new ObservableCollection<string>();

        // Populate available scenarios for InMemory connections
        var scenarios = _playgroundService.GetAvailableScenarios();
        AvailableScenarios = new ObservableCollection<string>(scenarios.Select(s => s.Name));
    }

    public event Action<bool>? RequestClose;

    #region Step Navigation

    [ObservableProperty]
    private string _stepDescription = "Choose your connection type";

    public bool IsStep1 => _currentStep == 1;
    public bool IsStep2CosmosDb => _currentStep == 2 && _isCosmosDb;
    public bool IsStep2Generic => _currentStep == 2 && _isGenericGremlin;
    public bool IsStep2InMemory => _currentStep == 2 && _isInMemory;
    public bool IsStep3 => _currentStep == 3;
    public bool CanGoBack => _currentStep > 1;
    public string NextButtonText => _currentStep == 3 ? "Create" : "Next";

    #endregion

    #region Step 1: Connection Type

    [ObservableProperty]
    private bool _isCosmosDb = true;

    [ObservableProperty]
    private bool _isGenericGremlin;

    [ObservableProperty]
    private bool _isInMemory;

    partial void OnIsCosmosDbChanged(bool value)
    {
        if (value)
        {
            _isGenericGremlin = false;
            _isInMemory = false;
            OnPropertyChanged(nameof(IsGenericGremlin));
            OnPropertyChanged(nameof(IsInMemory));
        }
    }

    partial void OnIsGenericGremlinChanged(bool value)
    {
        if (value)
        {
            _isCosmosDb = false;
            _isInMemory = false;
            OnPropertyChanged(nameof(IsCosmosDb));
            OnPropertyChanged(nameof(IsInMemory));
        }
    }

    partial void OnIsInMemoryChanged(bool value)
    {
        if (value)
        {
            _isCosmosDb = false;
            _isGenericGremlin = false;
            OnPropertyChanged(nameof(IsCosmosDb));
            OnPropertyChanged(nameof(IsGenericGremlin));
        }
    }

    #endregion

    #region Step 2a: Cosmos DB

    [ObservableProperty]
    private string _connectionString = string.Empty;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _availableDatabases;

    [ObservableProperty]
    private string? _selectedDatabase;

    [ObservableProperty]
    private ObservableCollection<string> _availableGraphs;

    [ObservableProperty]
    private string? _selectedGraph;

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public bool HasDatabases => _availableDatabases.Count > 0;

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnSelectedDatabaseChanged(string? value)
    {
        if (value != null)
        {
            _ = LoadGraphsForDatabaseAsync(value);
        }
    }

    [RelayCommand]
    private async Task DiscoverDatabasesAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            ErrorMessage = "Please enter a connection string.";
            return;
        }

        ErrorMessage = string.Empty;
        IsDiscovering = true;
        AvailableDatabases.Clear();
        AvailableGraphs.Clear();
        SelectedDatabase = null;
        SelectedGraph = null;

        try
        {
            var parsed = CosmosDbConnectionParser.Parse(_connectionString);
            if (parsed == null)
            {
                ErrorMessage = "Invalid connection string format. Expected: AccountEndpoint=...;AccountKey=...;";
                return;
            }

            _parsedEndpoint = parsed.Value.Endpoint;
            _parsedKey = parsed.Value.Key;

            var databases = await _discoveryService.DiscoverDatabasesAsync(
                parsed.Value.Endpoint, 
                parsed.Value.Key);

            foreach (var db in databases)
            {
                AvailableDatabases.Add(db);
            }

            if (AvailableDatabases.Count == 0)
            {
                ErrorMessage = "No databases found. Please create a database in Azure Portal first.";
            }
            else
            {
                SelectedDatabase = AvailableDatabases.First();
            }

            OnPropertyChanged(nameof(HasDatabases));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    private async Task LoadGraphsForDatabaseAsync(string database)
    {
        if (string.IsNullOrEmpty(_parsedEndpoint) || string.IsNullOrEmpty(_parsedKey))
        {
            return;
        }

        AvailableGraphs.Clear();
        SelectedGraph = null;

        try
        {
            var graphs = await _discoveryService.DiscoverGraphsAsync(
                _parsedEndpoint,
                _parsedKey,
                database);

            foreach (var graph in graphs)
            {
                AvailableGraphs.Add(graph);
            }

            if (AvailableGraphs.Count > 0)
            {
                SelectedGraph = AvailableGraphs.First();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load graphs: {ex.Message}";
        }
    }

    #endregion

    #region Step 2b: Generic Gremlin

    [ObservableProperty]
    private string _host = "localhost";

    [ObservableProperty]
    private int _port = 8182;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _useSsl;

    #endregion

    #region Step 2c: InMemory Playground

    /// <summary>
    /// Available scenarios for InMemory connections.
    /// </summary>
    public ObservableCollection<string> AvailableScenarios { get; }

    /// <summary>
    /// The selected scenario name, or null for an empty playground.
    /// </summary>
    [ObservableProperty]
    private string? _selectedScenario;

    /// <summary>
    /// Whether to start with an empty graph (no scenario).
    /// </summary>
    [ObservableProperty]
    private bool _useEmptyPlayground = true;

    partial void OnUseEmptyPlaygroundChanged(bool value)
    {
        if (value)
        {
            SelectedScenario = null;
        }
    }

    #endregion

    #region Step 3: Connection Name

    [ObservableProperty]
    private string _connectionName = string.Empty;

    public string SummaryText
    {
        get
        {
            if (_isCosmosDb)
            {
                return $"Type: Azure Cosmos DB\nDatabase: {_selectedDatabase}\nGraph: {_selectedGraph}";
            }
            if (_isInMemory)
            {
                var scenario = _useEmptyPlayground ? "Empty" : _selectedScenario ?? "Empty";
                return $"Type: InMemory Playground\nScenario: {scenario}";
            }
            return $"Type: Generic Gremlin\nHost: {_host}:{_port}\nSSL: {(_useSsl ? "Yes" : "No")}";
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void Back()
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateStepUI();
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (_currentStep == 3)
        {
            RequestClose?.Invoke(true);
            return;
        }

        if (!ValidateCurrentStep())
        {
            return;
        }

        _currentStep++;

        if (_currentStep == 3 && string.IsNullOrEmpty(_connectionName))
        {
            if (_isCosmosDb && !string.IsNullOrEmpty(_selectedDatabase))
            {
                ConnectionName = $"{_selectedDatabase}/{_selectedGraph}";
            }
            else if (_isInMemory)
            {
                ConnectionName = _useEmptyPlayground
                    ? "Playground (Empty)"
                    : $"Playground: {_selectedScenario}";
            }
            else if (!string.IsNullOrEmpty(_host))
            {
                ConnectionName = $"{_host}:{_port}";
            }
        }

        UpdateStepUI();
    }

    private bool ValidateCurrentStep()
    {
        ErrorMessage = string.Empty;

        if (_currentStep == 2)
        {
            if (_isCosmosDb)
            {
                if (string.IsNullOrEmpty(_selectedDatabase) || string.IsNullOrEmpty(_selectedGraph))
                {
                    ErrorMessage = "Please discover and select a database and graph.";
                    return false;
                }
            }
            else if (_isInMemory)
            {
                if (!_useEmptyPlayground && string.IsNullOrEmpty(_selectedScenario))
                {
                    ErrorMessage = "Please select a scenario or choose empty playground.";
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_host))
                {
                    ErrorMessage = "Host is required.";
                    return false;
                }
            }
        }

        return true;
    }

    private void UpdateStepUI()
    {
        StepDescription = _currentStep switch
        {
            1 => "Choose your connection type",
            2 when _isCosmosDb => "Enter your Cosmos DB connection string",
            2 when _isInMemory => "Configure your local playground",
            2 => "Configure Gremlin server settings",
            3 => "Review and name your connection",
            _ => ""
        };

        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2CosmosDb));
        OnPropertyChanged(nameof(IsStep2Generic));
        OnPropertyChanged(nameof(IsStep2InMemory));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(SummaryText));
    }

    #endregion

    #region Result

    /// <summary>
    /// Gets the connection settings from the dialog.
    /// </summary>
    public GremlinConnectionSettings GetConnectionSettings()
    {
        if (_isCosmosDb)
        {
            var host = _parsedEndpoint.Contains("://")
                ? new Uri(_parsedEndpoint).Host.Replace(".documents.azure.com", ".gremlin.cosmos.azure.com")
                : _clonedHost ?? string.Empty;

            var metadata = new GremlinConnectionMetadata
            {
                Id = Guid.NewGuid().ToString(),
                Name = _connectionName,
                Kind = GremlinConnectionKind.CosmosDb,
                Host = host,
                Port = 443,
                Username = $"/dbs/{_selectedDatabase}/colls/{_selectedGraph}",
                DatabaseName = _selectedDatabase,
                GraphName = _selectedGraph,
                EnableSsl = true
            };
            return new GremlinConnectionSettings(metadata, _parsedKey);
        }

        if (_isInMemory)
        {
            var metadata = new GremlinConnectionMetadata
            {
                Id = Guid.NewGuid().ToString(),
                Name = _connectionName,
                Kind = GremlinConnectionKind.InMemory,
                Host = "localhost",
                Port = 0,
                EnableSsl = false,
                ScenarioName = _useEmptyPlayground ? null : _selectedScenario
            };
            return new GremlinConnectionSettings(metadata, string.Empty);
        }

        {
            var metadata = new GremlinConnectionMetadata
            {
                Id = Guid.NewGuid().ToString(),
                Name = _connectionName,
                Kind = GremlinConnectionKind.GremlinServer,
                Host = _host,
                Port = _port,
                Username = _username,
                EnableSsl = _useSsl
            };
            return new GremlinConnectionSettings(metadata, _password);
        }
    }

    #endregion

    #region Clone Support

    private string? _clonedHost;

    /// <summary>
    /// Pre-populates the dialog for cloning an existing Cosmos DB connection.
    /// Selects the Cosmos DB type, stores the endpoint and key, and auto-triggers discovery
    /// so the user can pick a different database/graph within the same account.
    /// </summary>
    public void PrepopulateFromCosmosDb(GremlinConnectionMetadata source, string secret)
    {
        ArgumentNullException.ThrowIfNull(source);

        IsCosmosDb = true;

        // Reconstruct the documents endpoint from the Gremlin host
        var documentsHost = source.Host.Replace(".gremlin.cosmos.azure.com", ".documents.azure.com");
        _parsedEndpoint = $"https://{documentsHost}:443/";
        _parsedKey = secret;
        _clonedHost = source.Host;

        ConnectionString = $"AccountEndpoint={_parsedEndpoint};AccountKey={_parsedKey};";

        // Jump directly to step 2 (Cosmos DB) and auto-discover
        _currentStep = 2;
        UpdateStepUI();

        _ = DiscoverDatabasesAsync();
    }

    #endregion
}
