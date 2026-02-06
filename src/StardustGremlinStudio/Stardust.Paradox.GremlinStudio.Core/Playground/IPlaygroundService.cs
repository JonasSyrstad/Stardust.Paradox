using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Playground;

/// <summary>
/// Interface for managing the local in-memory playground.
/// </summary>
public interface IPlaygroundService
{
    /// <summary>
    /// Gets the current state of the playground.
    /// </summary>
    PlaygroundState State { get; }

    /// <summary>
    /// Gets the connector for the playground (null if not running).
    /// </summary>
    IGremlinLanguageConnector? Connector { get; }

    /// <summary>
    /// Gets all available scenarios.
    /// </summary>
    /// <returns>List of scenario information.</returns>
    IReadOnlyList<ScenarioInfo> GetAvailableScenarios();

    /// <summary>
    /// Starts the playground with an empty graph.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the playground and disposes resources.
    /// </summary>
    void Stop();

    /// <summary>
    /// Loads a scenario into the playground.
    /// </summary>
    /// <param name="scenarioName">The name of the scenario to load.</param>
    void LoadScenario(string scenarioName);

    /// <summary>
    /// Resets the current scenario (reloads it).
    /// </summary>
    void ResetScenario();

    /// <summary>
    /// Refreshes the playground state (vertex/edge counts, labels).
    /// </summary>
    /// <returns>The updated state.</returns>
    Task<PlaygroundState> RefreshStateAsync();

    /// <summary>
    /// Event raised when the playground state changes.
    /// </summary>
    event EventHandler<PlaygroundState>? StateChanged;
}
