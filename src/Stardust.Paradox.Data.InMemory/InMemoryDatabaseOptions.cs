using System;

namespace Stardust.Paradox.Data.InMemory;

/// <summary>
/// Configuration options for the in-memory database
/// </summary>
public class InMemoryDatabaseOptions
{
    public bool LogQueries { get; set; } = false;
    public double SimulatedRUPerQuery { get; set; } = 1.0;
    public bool EnableQueryLogging { get; set; } = false;
    public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(30);
        
    // New properties for scenario framework
    public bool EnableDebugLogging { get; set; } = false;
    public bool AutoGenerateIds { get; set; } = true;
    public bool ValidateEdgeVertices { get; set; } = true;
    public bool CaseSensitiveProperties { get; set; } = false;
    public bool CaseSensitiveLabels { get; set; } = false;
    public int MaxVertexCount { get; set; } = 0;
    public int MaxEdgeCount { get; set; } = 0;
    public bool TrackStatistics { get; set; } = false;
    public string VertexIdPrefix { get; set; } = "v";
    public string EdgeIdPrefix { get; set; } = "e";
    public bool AllowDuplicateEdges { get; set; } = true;
    public bool CascadeDeleteEdges { get; set; } = true;
}