namespace Stardust.Paradox.GremlinStudio.Core.Export;

/// <summary>
/// Export format for scenario files.
/// </summary>
public enum ScenarioExportFormat
{
    /// <summary>
    /// JSON format compatible with InMemory scenario loader.
    /// </summary>
    Json,

    /// <summary>
    /// C# class inheriting from InMemoryScenarioProviderBase.
    /// </summary>
    CSharp
}

/// <summary>
/// Represents a vertex in the scenario export.
/// </summary>
public class ScenarioVertex
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, object?> Properties { get; set; } = new();
}

/// <summary>
/// Represents an edge in the scenario export.
/// </summary>
public class ScenarioEdge
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string OutVertexId { get; set; } = string.Empty;
    public string InVertexId { get; set; } = string.Empty;
    public Dictionary<string, object?> Properties { get; set; } = new();
}

/// <summary>
/// Complete scenario data for export.
/// </summary>
public class ScenarioData
{
    public string Name { get; set; } = "ExportedScenario";
    public string Description { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string? SourceConnection { get; set; }
    public string? ExportQuery { get; set; }
    public List<ScenarioVertex> Vertices { get; set; } = new();
    public List<ScenarioEdge> Edges { get; set; } = new();
    public ScenarioMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Metadata about the export.
/// </summary>
public class ScenarioMetadata
{
    public int VertexCount { get; set; }
    public int EdgeCount { get; set; }
    public string ExportedBy { get; set; } = "Stardust.Paradox.GremlinStudio";
}
