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
    
    // ========== Cosmos DB Emulation Settings ==========
    
    /// <summary>
    /// Enable Cosmos DB Gremlin API emulation mode.
    /// When true, restricts operations to Cosmos-supported features and 
    /// simulates Cosmos DB behavior including RU charges and rate limiting.
    /// </summary>
    public bool CosmosDbEmulationMode { get; set; } = false;
    
    /// <summary>
    /// Partition key path for Cosmos DB emulation (e.g., "/pk", "/tenantId").
    /// Required when CosmosDbEmulationMode is true for partition-aware operations.
    /// </summary>
    public string PartitionKeyPath { get; set; }
    
    /// <summary>
    /// Enforce cross-partition query restrictions.
    /// When true, queries without partition key filter will be flagged/limited.
    /// </summary>
    public bool EnforceCrossPartitionQueryRestrictions { get; set; } = false;
    
    /// <summary>
    /// Maximum Request Units (RU) budget per second.
    /// When exceeded, simulates 429 (Too Many Requests) responses.
    /// </summary>
    public double MaxRequestUnitsPerSecond { get; set; } = 10000;
    
    /// <summary>
    /// Simulate RU charges for operations.
    /// </summary>
    public bool SimulateRequestCharges { get; set; } = true;
    
    /// <summary>
    /// RU cost multipliers for different operations
    /// </summary>
    public CosmosDbRUCostSettings RUCostSettings { get; set; } = new CosmosDbRUCostSettings();
    
    /// <summary>
    /// Maximum results per query response (Cosmos DB default: 1000).
    /// </summary>
    public int MaxItemsPerQuery { get; set; } = 1000;
    
    /// <summary>
    /// Maximum query execution time in milliseconds (Cosmos DB: 5000ms default).
    /// </summary>
    public int MaxQueryExecutionTimeMs { get; set; } = 5000;
    
    /// <summary>
    /// Throw exceptions for unsupported Gremlin steps in Cosmos mode.
    /// When false, logs warnings instead.
    /// </summary>
    public bool ThrowOnUnsupportedStep { get; set; } = true;
    
    /// <summary>
    /// Enable strict TinkerPop 3.5.x compliance checking.
    /// Validates query syntax and step ordering.
    /// </summary>
    public bool StrictTinkerPopCompliance { get; set; } = false;
    
    /// <summary>
    /// Enable rate limiting simulation for Cosmos DB emulation.
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;
}

/// <summary>
/// RU cost configuration for Cosmos DB emulation.
/// Based on Azure Cosmos DB Gremlin API pricing model.
/// </summary>
public class CosmosDbRUCostSettings
{
    /// <summary>
    /// Base RU cost per point read (single vertex/edge by ID)
    /// </summary>
    public double PointReadRU { get; set; } = 1.0;
    
    /// <summary>
    /// Base RU cost per vertex creation
    /// </summary>
    public double CreateVertexRU { get; set; } = 5.0;
    
    /// <summary>
    /// Base RU cost per edge creation
    /// </summary>
    public double CreateEdgeRU { get; set; } = 5.0;
    
    /// <summary>
    /// Base RU cost per vertex update
    /// </summary>
    public double UpdateVertexRU { get; set; } = 5.0;
    
    /// <summary>
    /// Base RU cost per edge update
    /// </summary>
    public double UpdateEdgeRU { get; set; } = 5.0;
    
    /// <summary>
    /// Base RU cost per vertex deletion
    /// </summary>
    public double DeleteVertexRU { get; set; } = 5.0;
    
    /// <summary>
    /// Base RU cost per edge deletion
    /// </summary>
    public double DeleteEdgeRU { get; set; } = 5.0;
    
    /// <summary>
    /// RU cost per KB of data read
    /// </summary>
    public double ReadPerKBRU { get; set; } = 0.5;
    
    /// <summary>
    /// RU cost per KB of data written
    /// </summary>
    public double WritePerKBRU { get; set; } = 5.0;
    
    /// <summary>
    /// Cross-partition query RU multiplier
    /// </summary>
    public double CrossPartitionMultiplier { get; set; } = 2.5;
    
    /// <summary>
    /// Scan query RU multiplier (no index usage)
    /// </summary>
    public double ScanQueryMultiplier { get; set; } = 10.0;
    
    /// <summary>
    /// Base RU cost for traversal steps (out, in, both, etc.)
    /// </summary>
    public double TraversalStepRU { get; set; } = 1.0;
    
    /// <summary>
    /// Base RU cost for filter steps (has, hasLabel, etc.)
    /// </summary>
    public double FilterStepRU { get; set; } = 0.5;
    
    /// <summary>
    /// Base RU cost for aggregation steps (count, sum, etc.)
    /// </summary>
    public double AggregationStepRU { get; set; } = 1.0;
}
