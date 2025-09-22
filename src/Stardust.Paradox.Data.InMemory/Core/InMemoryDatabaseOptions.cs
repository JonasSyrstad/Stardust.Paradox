using System;

namespace Stardust.Paradox.Data.InMemory.Core;

/// <summary>
/// Configuration options for the in-memory database
/// </summary>
public class InMemoryDatabaseOptions
{
    /// <summary>
    /// Enable query logging to console
    /// </summary>
    public bool EnableQueryLogging { get; set; } = false;
    
    /// <summary>
    /// Simulated Request Units consumed per query
    /// </summary>
    public double SimulatedRUPerQuery { get; set; } = 1.0;
    
    /// <summary>
    /// Query execution timeout
    /// </summary>
    public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(30);
        
    // New properties for scenario framework
    /// <summary>
    /// Enable debug logging with detailed information
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
    
    /// <summary>
    /// Automatically generate IDs for vertices and edges
    /// </summary>
    public bool AutoGenerateIds { get; set; } = true;
    
    /// <summary>
    /// Validate that edge vertices exist before creating edges
    /// </summary>
    public bool ValidateEdgeVertices { get; set; } = true;
    
    /// <summary>
    /// Case sensitive property matching
    /// </summary>
    public bool CaseSensitiveProperties { get; set; } = false;
    
    /// <summary>
    /// Case sensitive label matching
    /// </summary>
    public bool CaseSensitiveLabels { get; set; } = false;
    
    /// <summary>
    /// Maximum number of vertices allowed (0 = unlimited)
    /// </summary>
    public int MaxVertexCount { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of edges allowed (0 = unlimited)
    /// </summary>
    public int MaxEdgeCount { get; set; } = 0;
    
    /// <summary>
    /// Track statistics about the database
    /// </summary>
    public bool TrackStatistics { get; set; } = false;
    
    /// <summary>
    /// Prefix for auto-generated vertex IDs
    /// </summary>
    public string VertexIdPrefix { get; set; } = "v";
    
    /// <summary>
    /// Prefix for auto-generated edge IDs
    /// </summary>
    public string EdgeIdPrefix { get; set; } = "e";
    
    /// <summary>
    /// Allow multiple edges with the same label between the same vertices
    /// </summary>
    public bool AllowDuplicateEdges { get; set; } = true;
    
    /// <summary>
    /// When deleting a vertex, also delete all connected edges
    /// </summary>
    public bool CascadeDeleteEdges { get; set; } = true;

    /// <summary>
    /// Legacy property for backward compatibility - use EnableQueryLogging instead
    /// </summary>
    [Obsolete("Use EnableQueryLogging instead", false)]
    public bool LogQueries 
    { 
        get => EnableQueryLogging; 
        set => EnableQueryLogging = value; 
    }
}
