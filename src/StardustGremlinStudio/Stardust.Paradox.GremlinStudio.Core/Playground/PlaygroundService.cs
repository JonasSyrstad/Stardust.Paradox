using Microsoft.Extensions.Logging;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.GremlinStudio.Core.Playground;

/// <summary>
/// Manages the local in-memory playground for testing Gremlin queries.
/// </summary>
public sealed class PlaygroundService : IPlaygroundService, IDisposable
{
    private readonly ILogger<PlaygroundService> _logger;
    private InMemoryGremlinLanguageConnector? _connector;
    private string? _loadedScenarioName;
    private PlaygroundState _state = PlaygroundState.Stopped;
    private bool _disposed;

    public PlaygroundService(ILogger<PlaygroundService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PlaygroundState State => _state;

    public IGremlinLanguageConnector? Connector => _connector;

    public event EventHandler<PlaygroundState>? StateChanged;

    public IReadOnlyList<ScenarioInfo> GetAvailableScenarios()
    {
        var scenarios = InMemoryScenarioRegistry.GetAllScenarios();
        return scenarios.Select(kvp => new ScenarioInfo(kvp.Key, kvp.Value.Description)).ToList();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connector is not null)
        {
            _logger.LogWarning("Playground already running, stopping first");
            Stop();
        }

        _connector = new InMemoryGremlinLanguageConnector();
        _loadedScenarioName = null;

        UpdateState(new PlaygroundState
        {
            IsRunning = true,
            LoadedScenarioName = null,
            VertexCount = 0,
            EdgeCount = 0
        });

        _logger.LogInformation("Playground started");
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connector is null)
        {
            return;
        }

        _connector.Dispose();
        _connector = null;
        _loadedScenarioName = null;

        UpdateState(PlaygroundState.Stopped);

        _logger.LogInformation("Playground stopped");
    }

    public void LoadScenario(string scenarioName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioName);

        if (_connector is null)
        {
            Start();
        }

        var scenario = InMemoryScenarioRegistry.GetScenario(scenarioName);
        if (scenario is null)
        {
            throw new ArgumentException($"Scenario '{scenarioName}' not found", nameof(scenarioName));
        }

        // Reset database and load scenario
        _connector!.Database.Clear();
        scenario.ConfigureScenario(_connector.Database);
        _loadedScenarioName = scenarioName;

        _ = RefreshStateAsync();

        _logger.LogInformation("Loaded scenario: {ScenarioName}", scenarioName);
    }

    public void ResetScenario()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_loadedScenarioName is null)
        {
            _logger.LogWarning("No scenario loaded to reset");
            return;
        }

        LoadScenario(_loadedScenarioName);
        _logger.LogInformation("Reset scenario: {ScenarioName}", _loadedScenarioName);
    }

    public async Task<PlaygroundState> RefreshStateAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connector is null)
        {
            return PlaygroundState.Stopped;
        }

        try
        {
            // Get vertex count
            var vertexCountResult = await _connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>())
                .ConfigureAwait(false);
            var vertexCount = vertexCountResult.FirstOrDefault() is long vc ? (int)vc : 0;

            // Get edge count
            var edgeCountResult = await _connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>())
                .ConfigureAwait(false);
            var edgeCount = edgeCountResult.FirstOrDefault() is long ec ? (int)ec : 0;


            // Get vertex labels
            var vertexLabelsResult = await _connector.ExecuteAsync("g.V().label().dedup()", new Dictionary<string, object>())
                .ConfigureAwait(false);
            var vertexLabels = vertexLabelsResult.Select(l => (string)(l?.ToString() ?? "unknown")).ToList();

            // Get edge labels
            var edgeLabelsResult = await _connector.ExecuteAsync("g.E().label().dedup()", new Dictionary<string, object>())
                .ConfigureAwait(false);
            var edgeLabels = edgeLabelsResult.Select(l => (string)(l?.ToString() ?? "unknown")).ToList();

            var newState = new PlaygroundState
            {
                IsRunning = true,
                LoadedScenarioName = _loadedScenarioName,
                VertexCount = vertexCount,
                EdgeCount = edgeCount,
                VertexLabels = vertexLabels,
                EdgeLabels = edgeLabels
            };

            UpdateState(newState);
            return newState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh playground state");
            return _state;
        }
    }

    private void UpdateState(PlaygroundState newState)
    {
        _state = newState;
        StateChanged?.Invoke(this, newState);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connector?.Dispose();
        _connector = null;
    }
}
