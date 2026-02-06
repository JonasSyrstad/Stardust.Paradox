namespace Stardust.Paradox.GremlinStudio.Core.Playground;

/// <summary>
/// Represents the state of the local playground.
/// </summary>
public sealed class PlaygroundState
{
    /// <summary>
    /// Whether the playground is currently running.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// The currently loaded scenario name.
    /// </summary>
    public string? LoadedScenarioName { get; init; }

    /// <summary>
    /// The number of vertices in the graph.
    /// </summary>
    public int VertexCount { get; init; }

    /// <summary>
    /// The number of edges in the graph.
    /// </summary>
    public int EdgeCount { get; init; }

    /// <summary>
    /// The vertex labels present in the graph.
    /// </summary>
    public IReadOnlyList<string> VertexLabels { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The edge labels present in the graph.
    /// </summary>
    public IReadOnlyList<string> EdgeLabels { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates an empty/stopped state.
    /// </summary>
    public static PlaygroundState Stopped => new()
    {
        IsRunning = false
    };
}
