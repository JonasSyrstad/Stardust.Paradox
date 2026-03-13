using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stardust.Paradox.GremlinStudio.Core.Connections;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// ViewModel for the edit connection dialog.
/// </summary>
public partial class EditConnectionDialogViewModel : ObservableObject
{
    private readonly IGremlinConnectionTester _connectionTester;
    private readonly GremlinConnectionMetadata _originalMetadata;
    private readonly string _originalSecret;

    public EditConnectionDialogViewModel(
        GremlinConnectionMetadata metadata,
        string secret,
        IGremlinConnectionTester connectionTester)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(connectionTester);

        _connectionTester = connectionTester;
        _originalMetadata = metadata;
        _originalSecret = secret;

        // Populate fields from metadata
        _connectionName = metadata.Name;
        _host = metadata.Host;
        _port = metadata.Port;
        _username = metadata.Username;
        _secret = secret;
        _database = metadata.DatabaseName ?? string.Empty;
        _graph = metadata.GraphName ?? string.Empty;
    }

    public event Action<bool>? RequestClose;

    /// <summary>
    /// Whether this is a server-based connection (not InMemory).
    /// </summary>
    public bool IsServerConnection => _originalMetadata.Kind != GremlinConnectionKind.InMemory;

    /// <summary>
    /// Display text for the connection kind.
    /// </summary>
    public string KindDescription => _originalMetadata.Kind switch
    {
        GremlinConnectionKind.CosmosDb => "Azure Cosmos DB (Gremlin API)",
        GremlinConnectionKind.GremlinServer => "Generic Gremlin Server",
        GremlinConnectionKind.InMemory => "Local Playground (InMemory)",
        _ => _originalMetadata.Kind.ToString()
    };

    /// <summary>
    /// Display text for the InMemory scenario.
    /// </summary>
    public string ScenarioDisplay => string.IsNullOrEmpty(_originalMetadata.ScenarioName)
        ? "Empty playground"
        : _originalMetadata.ScenarioName;

    [ObservableProperty]
    private string _connectionName;

    [ObservableProperty]
    private string _host;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _secret;

    [ObservableProperty]
    private string _database;

    [ObservableProperty]
    private string _graph;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    /// <summary>
    /// Builds updated connection settings from the dialog fields.
    /// </summary>
    public GremlinConnectionSettings GetUpdatedSettings()
    {
        var metadata = _originalMetadata.Clone();
        metadata.Name = ConnectionName;
        metadata.Host = Host;
        metadata.Port = Port;
        metadata.Username = Username;
        metadata.DatabaseName = string.IsNullOrWhiteSpace(Database) ? null : Database;
        metadata.GraphName = string.IsNullOrWhiteSpace(Graph) ? null : Graph;
        metadata.LastModifiedAt = DateTimeOffset.UtcNow;

        return new GremlinConnectionSettings(metadata, Secret);
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            ErrorMessage = "Host is required to test connection";
            return;
        }

        ErrorMessage = string.Empty;
        IsTesting = true;

        try
        {
            var settings = GetUpdatedSettings();
            var result = await _connectionTester.TestConnectionAsync(settings).ConfigureAwait(true);

            ErrorMessage = result.IsSuccess
                ? $"✔ Connection successful ({result.Latency.TotalMilliseconds:F0}ms)"
                : $"Connection failed: {result.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Test error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            ErrorMessage = "Connection name is required";
            return;
        }

        RequestClose?.Invoke(true);
    }
}
