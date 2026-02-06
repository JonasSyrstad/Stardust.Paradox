using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.CosmosDb;

/// <summary>
/// Defines the Gremlin step limitations for Azure Cosmos DB Gremlin API.
/// Based on TinkerPop 3.5.x subset supported by Cosmos DB.
/// Reference: https://docs.microsoft.com/en-us/azure/cosmos-db/gremlin/support
/// </summary>
public static class CosmosDbGremlinLimitations
{
    /// <summary>
    /// Steps NOT supported by Cosmos DB Gremlin API (TinkerPop 3.5.x subset)
    /// </summary>
    public static readonly HashSet<string> UnsupportedSteps = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        // Lambda-based operations (Cosmos DB doesn't support Groovy lambdas)
        "map",          // Lambda version - traversal version IS supported via project/select
        "flatMap",      // Lambda version
        "filter",       // Lambda version (traversal version IS supported)
        "sideEffect",   // Lambda-based side effects
        
        // Side-effect steps (require server-side state)
        "aggregate",    // Uses side-effect variables (global scope)
        "store",        // Uses side-effect variables
        "sack",         // Sack operations require server state
        "cap",          // Side-effect capture
        "subgraph",     // Subgraph extraction
        
        // Path/Analysis steps with limitations
        "cyclicPath",   // Complex path analysis
        "sample",       // Random sampling
        "coin",         // Random filtering
        "tail",         // Not supported
        
        // Branch operations with limitations
        "optional",     // Not supported in Cosmos DB
        "branch",       // Not supported
        
        // Analytics/OLAP steps (require GraphComputer)
        "pageRank",
        "peerPressure",
        "connectedComponent",
        "shortestPath",
        "program",
        
        // Other unsupported operations
        "math",         // Math expressions
        "match",        // Pattern matching (complex)
        "profile",      // Query profiling
        "explain",      // Query explanation
        "io",           // Graph I/O operations
        "call",         // Custom procedure calls
        "timeLimit",    // Time limits not supported
        
        // TinkerPop 3.6+ steps (not in Cosmos DB which uses 3.5.x)
        "mergeV",
        "mergeE",
        "element",
        "fail",
        "none",
        
        // Property-related limitations
        "propertyMap",  // Use valueMap instead
    };
    
    /// <summary>
    /// Steps with LIMITED support in Cosmos DB - these work but have restrictions
    /// </summary>
    public static readonly Dictionary<string, string> LimitedSteps = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        { "drop", "Cosmos DB requires explicit id for drop operations on large datasets" },
        { "property", "Cosmos DB has limitations on property cardinality (single only for edges)" },
        { "repeat", "Cosmos DB has depth limits on repeat traversals (default: 100)" },
        { "tree", "Cosmos DB has result size limits for tree operations" },
        { "path", "Cosmos DB has path length limits" },
        { "local", "Limited support - some operations inside local() may not work" },
        { "group", "Large group operations may timeout or exceed RU limits" },
        { "order", "Ordering large result sets consumes significant RUs" }
    };
    
    /// <summary>
    /// Steps that ARE fully supported by Cosmos DB Gremlin API
    /// </summary>
    public static readonly HashSet<string> SupportedSteps = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        // Start steps
        "V", "E", "addV", "addE", "inject",
        
        // Traversal steps
        "out", "in", "both", "outE", "inE", "bothE",
        "outV", "inV", "bothV", "otherV",
        
        // Filter steps
        "has", "hasLabel", "hasId", "hasKey", "hasValue", "hasNot",
        "is", "and", "or", "not", "where",
        "dedup", "range", "limit", "skip",
        "simplePath",
        
        // Map steps
        "id", "label", "constant", "values", "properties",
        "valueMap", "elementMap", "select", "project",
        "unfold", "fold", "path", "order", "coalesce",
        
        // Side-effect steps (supported subset)
        "as", "property",
        
        // Branch steps (supported subset)
        "union", "choose", "repeat", "until", "emit", "times", "loops",
        
        // Terminal/Reduce steps
        "count", "sum", "max", "min", "mean",
        "group", "groupCount", "tree",
        
        // Mutation steps
        "drop",
        
        // Barrier steps
        "barrier",
        
        // By modulator
        "by",
        
        // From/To modulators
        "from", "to",
        
        // Option modulator
        "option"
    };
    
    /// <summary>
    /// Predicates supported by Cosmos DB
    /// </summary>
    public static readonly HashSet<string> SupportedPredicates = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "lt", "lte", "gt", "gte",
        "inside", "outside", "between",
        "within", "without"
    };
    
    /// <summary>
    /// TextP predicates supported by Cosmos DB
    /// </summary>
    public static readonly HashSet<string> SupportedTextPredicates = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "startingWith", "endingWith", "containing",
        "notStartingWith", "notEndingWith", "notContaining"
    };
    
    /// <summary>
    /// Predicates NOT supported by Cosmos DB
    /// </summary>
    public static readonly HashSet<string> UnsupportedPredicates = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "regex"  // Regular expressions not supported
    };
    
    /// <summary>
    /// Maximum repeat depth in Cosmos DB
    /// </summary>
    public const int MaxRepeatDepth = 100;
    
    /// <summary>
    /// Maximum path length in Cosmos DB
    /// </summary>
    public const int MaxPathLength = 1000;
    
    /// <summary>
    /// Default maximum items per query response
    /// </summary>
    public const int DefaultMaxItemsPerQuery = 1000;
    
    /// <summary>
    /// Default query timeout in milliseconds
    /// </summary>
    public const int DefaultQueryTimeoutMs = 5000;
    
    /// <summary>
    /// Maximum query size in bytes
    /// </summary>
    public const int MaxQuerySizeBytes = 256 * 1024; // 256 KB
    
    /// <summary>
    /// Check if a step is supported by Cosmos DB
    /// </summary>
    public static bool IsStepSupported(string stepName)
    {
        if (string.IsNullOrEmpty(stepName))
            return false;
            
        return SupportedSteps.Contains(stepName) && !UnsupportedSteps.Contains(stepName);
    }
    
    /// <summary>
    /// Check if a step has limited support (works but with restrictions)
    /// </summary>
    public static bool HasLimitedSupport(string stepName, out string limitation)
    {
        if (string.IsNullOrEmpty(stepName))
        {
            limitation = null;
            return false;
        }
        
        return LimitedSteps.TryGetValue(stepName, out limitation);
    }
    
    /// <summary>
    /// Check if a predicate is supported by Cosmos DB
    /// </summary>
    public static bool IsPredicateSupported(string predicateName)
    {
        if (string.IsNullOrEmpty(predicateName))
            return false;
            
        return SupportedPredicates.Contains(predicateName) || 
               SupportedTextPredicates.Contains(predicateName);
    }
    
    /// <summary>
    /// Get the reason why a step is not supported
    /// </summary>
    public static string GetUnsupportedReason(string stepName)
    {
        if (string.IsNullOrEmpty(stepName))
            return "Step name is empty";
            
        var normalizedStep = stepName.ToLowerInvariant();
        
        return normalizedStep switch
        {
            "map" or "flatmap" or "filter" when !SupportedSteps.Contains(stepName) 
                => "Lambda-based operations are not supported. Use traversal-based alternatives.",
            "sideeffect" => "Side-effect lambdas are not supported in Cosmos DB.",
            "aggregate" or "store" or "cap" 
                => "Side-effect variables are not supported in Cosmos DB.",
            "sack" => "Sack operations require server-side state not available in Cosmos DB.",
            "subgraph" => "Subgraph extraction is not supported in Cosmos DB.",
            "cyclicpath" => "Cyclic path detection is not supported.",
            "sample" or "coin" => "Random sampling operations are not supported.",
            "tail" => "The tail() step is not supported. Use range() or limit() instead.",
            "optional" => "The optional() step is not supported in Cosmos DB.",
            "branch" => "The branch() step is not supported. Use choose() instead.",
            "pagerank" or "peerpressure" or "connectedcomponent" or "shortestpath" 
                => "Graph analytics/OLAP operations require GraphComputer which is not available.",
            "math" => "Math expressions are not supported.",
            "match" => "Pattern matching with match() is not supported.",
            "profile" or "explain" => "Query profiling/explanation is not available in Cosmos DB.",
            "io" => "Graph I/O operations are not supported.",
            "call" => "Custom procedure calls are not supported.",
            "timelimit" => "Query time limits are not configurable via Gremlin.",
            "mergev" or "mergee" => "Merge operations are TinkerPop 3.6+ and not supported.",
            "propertymap" => "Use valueMap() instead of propertyMap().",
            _ => $"The step '{stepName}' is not supported by Cosmos DB Gremlin API."
        };
    }
}
