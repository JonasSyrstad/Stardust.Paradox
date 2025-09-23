using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory.Scenarios;
using Stardust.Paradox.Data.Providers.Gremlin;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Represents a vertex in the exported scenario
/// </summary>
public class ExportedVertex
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Represents an edge in the exported scenario
/// </summary>
public class ExportedEdge
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string OutVertexId { get; set; } = string.Empty;
    public string InVertexId { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Represents an exported scenario file
/// </summary>
public class ExportedScenario
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string SourceConnection { get; set; } = string.Empty;
    public string ExportQuery { get; set; } = string.Empty;
    public List<string>? VertexIds { get; set; }
    public List<ExportedVertex> Vertices { get; set; } = new();
    public List<ExportedEdge> Edges { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Service for exporting graph data from CosmosDB to scenario files
/// </summary>
public class ScenarioExporter
{
    private readonly GremlinNetLanguageConnector _connector;
    private readonly string _connectionName;
    public bool EnableDebugLogging { get; set; } = true; // Enable debug logging by default to help troubleshoot

    public ScenarioExporter(GremlinNetLanguageConnector connector, string connectionName)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _connectionName = connectionName ?? throw new ArgumentNullException(nameof(connectionName));
    }

    /// <summary>
    /// Export vertices found by a Gremlin query and their connected edges
    /// </summary>
    public async Task<ExportedScenario> ExportByQueryAsync(string gremlinQuery, string scenarioName, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(gremlinQuery))
            throw new ArgumentException("Gremlin query cannot be empty", nameof(gremlinQuery));

        if (string.IsNullOrWhiteSpace(scenarioName))
            throw new ArgumentException("Scenario name cannot be empty", nameof(scenarioName));

        Console.WriteLine($"Executing query to find vertices: {gremlinQuery}");
        
        // Execute the query to get vertices - use intelligent approach based on query complexity
        var vertices = new List<ExportedVertex>();
        
        try
        {
            // Analyze the query to determine if it's complex
            bool isComplexQuery = IsComplexQuery(gremlinQuery);
            
            if (isComplexQuery)
            {
                Console.WriteLine("Detected complex query - using specialized handling");
                vertices = await HandleComplexQueryExportAsync(gremlinQuery);
            }
            else
            {
                // Try to get vertices with properties in one query
                var queryWithProperties = $"{gremlinQuery}.valueMap(true)";
                Console.WriteLine($"Trying query with properties: {queryWithProperties}");
                
                var queryResults = await _connector.ExecuteAsync(queryWithProperties, new Dictionary<string, object>());
                var resultsList = queryResults.ToList();
                
                foreach (dynamic result in resultsList)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"\nDebug: Query with properties result: {JsonConvert.SerializeObject(result, Formatting.Indented)}");
                    
                    // Extract ID first to use as fallback
                    string vertexId = string.Empty;
                    var data = result as IDictionary<string, object>;
                    if (data != null && data.ContainsKey("id"))
                    {
                        var idValue = data["id"];
                        if (idValue is IEnumerable<object> idArray)
                            vertexId = idArray.FirstOrDefault()?.ToString() ?? string.Empty;
                        else
                            vertexId = idValue?.ToString() ?? string.Empty;
                    }
                    
                    var vertex = ParseVertexFromValueMap(result, vertexId);
                    if (vertex != null)
                        vertices.Add(vertex);
                }
                
                Console.WriteLine($"\nFound {vertices.Count} vertices with properties from enhanced query");
            }

            // Create consolidated progress bar with estimated counts
            var estimatedEdgeCount = vertices.Count * 2; // Rough estimate
            using (var progress = ConsolidatedProgressBarFactory.CreateScenarioExportProgress(vertices.Count, estimatedEdgeCount, 2))
            {
                // Mark vertices as processed in progress
                foreach (var vertex in vertices)
                {
                    progress.IncrementVertexProgress(vertex.Id);
                }

                // Get vertex IDs for edge discovery
                var vertexIds = vertices.Select(v => v.Id).ToList();

                // Find all edges between these vertices
                var edges = await FindEdgesBetweenVerticesAsync(vertexIds, progress);

                // Update file progress as we save
                progress.IncrementFileProgress("Preparing scenario data");
                
                var scenario = new ExportedScenario
                {
                    Name = scenarioName,
                    Description = description ?? $"Exported from query: {gremlinQuery}",
                    ExportedAt = DateTime.UtcNow,
                    SourceConnection = _connectionName,
                    ExportQuery = gremlinQuery,
                    Vertices = vertices,
                    Edges = edges,
                    Metadata = new Dictionary<string, object>
                    {
                        ["vertexCount"] = vertices.Count,
                        ["edgeCount"] = edges.Count,
                        ["queryType"] = isComplexQuery ? "complex" : "simple",
                        ["exportedBy"] = "Stardust.Paradox.Data.ScenarioConnector"
                    }
                };

                progress.IncrementFileProgress("Scenario ready");
                return scenario;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Enhanced query failed: {ex.Message}, falling back to basic query");
            
            // Fallback: Execute basic query and then fetch properties separately
            var queryResults = await _connector.ExecuteAsync(gremlinQuery, new Dictionary<string, object>());
            var resultsList = queryResults.ToList();
            
            // Create consolidated progress bar for fallback
            var estimatedEdgeCount = resultsList.Count * 2;
            using (var progress = ConsolidatedProgressBarFactory.CreateScenarioExportProgress(resultsList.Count, estimatedEdgeCount, 2))
            {
                foreach (dynamic result in resultsList)
                {
                    var vertex = ParseVertex(result);
                    if (vertex != null)
                    {
                        vertices.Add(vertex);
                        progress.IncrementVertexProgress(vertex.Id);
                    }
                    else
                    {
                        progress.IncrementVertexProgress();
                    }
                }

                // If vertices were found but have no properties, try to fetch them
                if (vertices.Any() && vertices.All(v => v.Properties.Count == 0))
                {
                    Console.WriteLine("\nNo properties found in query results, fetching properties separately...");
                    vertices = await EnhanceVerticesWithPropertiesAsync(vertices, progress);
                }

                Console.WriteLine($"\nFound {vertices.Count} vertices from query");

                // Get vertex IDs for edge discovery
                var vertexIds = vertices.Select(v => v.Id).ToList();

                // Find all edges between these vertices
                var edges = await FindEdgesBetweenVerticesAsync(vertexIds, progress);

                progress.IncrementFileProgress("Preparing scenario data");

                var scenario = new ExportedScenario
                {
                    Name = scenarioName,
                    Description = description ?? $"Exported from query: {gremlinQuery}",
                    ExportedAt = DateTime.UtcNow,
                    SourceConnection = _connectionName,
                    ExportQuery = gremlinQuery,
                    Vertices = vertices,
                    Edges = edges,
                    Metadata = new Dictionary<string, object>
                    {
                        ["vertexCount"] = vertices.Count,
                        ["edgeCount"] = edges.Count,
                        ["exportedBy"] = "Stardust.Paradox.Data.ScenarioConnector"
                    }
                };

                progress.IncrementFileProgress("Scenario ready");
                return scenario;
            }
        }
    }

    /// <summary>
    /// Determine if a query is complex and requires special handling
    /// </summary>
    private bool IsComplexQuery(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        // Patterns that indicate complex queries that don't return simple vertices
        var complexPatterns = new[]
        {
            "select(",        // select() operations
            ".as(",          // as() operations combined with other steps
            "project(",      // project() operations
            "group(",        // group() operations
            "fold(",         // fold() operations
            "path(",         // path() operations
            "union(",        // union() operations
            "coalesce(",     // coalesce() operations
        };

        // Additional patterns that suggest the query returns processed/transformed data
        var transformPatterns = new[]
        {
            ").select(",
            ").project(",
            ").group(",
            ").fold(",
            ").path(",
        };

        // Check for complex patterns
        foreach (var pattern in complexPatterns)
        {
            if (lowerQuery.Contains(pattern))
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Detected complex query pattern: {pattern}");
                return true;
            }
        }

        // Check for transformation patterns
        foreach (var pattern in transformPatterns)
        {
            if (lowerQuery.Contains(pattern))
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Detected transformation pattern: {pattern}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handle complex queries that might not return simple vertices
    /// </summary>
    private async Task<List<ExportedVertex>> HandleComplexQueryExportAsync(string gremlinQuery)
    {
        var vertices = new List<ExportedVertex>();

        try
        {
            // Step 1: Execute the original query to see what we get
            Console.WriteLine("Executing original complex query...");
            var originalResults = await _connector.ExecuteAsync(gremlinQuery, new Dictionary<string, object>());
            var resultsList = originalResults.ToList();

            if (EnableDebugLogging)
            {
                Console.WriteLine($"Debug: Complex query returned {resultsList.Count} results");
                for (int i = 0; i < Math.Min(3, resultsList.Count); i++)
                {
                    Console.WriteLine($"Debug: Result {i}: {JsonConvert.SerializeObject(resultsList[i], Formatting.Indented)}");
                }
            }

            // Step 2: Try to extract vertex information from the results
            var extractedVertexIds = new HashSet<string>();

            foreach (dynamic result in resultsList)
            {
                var vertexIds = ExtractVertexIdsFromResult(result);
                foreach (var id in vertexIds)
                {
                    extractedVertexIds.Add(id);
                }
            }

            if (EnableDebugLogging)
            {
                Console.WriteLine($"Debug: Extracted {extractedVertexIds.Count} unique vertex IDs from complex query results");
            }

            // Step 3: If we found vertex IDs, fetch the full vertex data
            if (extractedVertexIds.Count > 0)
            {
                Console.WriteLine($"Fetching full vertex data for {extractedVertexIds.Count} vertices...");
                vertices = await FetchVerticesWithPropertiesAsync(extractedVertexIds.ToList());
            }
            else
            {
                // Step 4: Fallback - try to modify the query to get vertices
                Console.WriteLine("No vertex IDs found in results, trying query modification...");
                var modifiedQuery = ModifyQueryForVertexExtraction(gremlinQuery);
                
                if (modifiedQuery != gremlinQuery && !string.IsNullOrEmpty(modifiedQuery))
                {
                    Console.WriteLine($"Trying modified query: {modifiedQuery}");
                    
                    try
                    {
                        var modifiedResults = await _connector.ExecuteAsync(modifiedQuery, new Dictionary<string, object>());
                        
                        foreach (dynamic result in modifiedResults)
                        {
                            var vertex = ParseVertex(result);
                            if (vertex != null)
                                vertices.Add(vertex);
                        }
                        
                        if (vertices.Count > 0)
                        {
                            Console.WriteLine($"Success with modified query: found {vertices.Count} vertices");
                        }
                    }
                    catch (Exception modEx)
                    {
                        Console.WriteLine($"Modified query failed: {modEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Complex query handling failed: {ex.Message}");
            throw new InvalidOperationException($"Failed to process complex query '{gremlinQuery}': {ex.Message}", ex);
        }

        return vertices;
    }

    /// <summary>
    /// Extract vertex IDs from complex query results
    /// </summary>
    private List<string> ExtractVertexIdsFromResult(dynamic result)
    {
        var vertexIds = new List<string>();

        try
        {
            // Handle different result types
            if (result is JObject jobj)
            {
                ExtractVertexIdsFromJObject(jobj, vertexIds);
            }
            else if (result is IDictionary<string, object> dict)
            {
                ExtractVertexIdsFromDictionary(dict, vertexIds);
            }
            else if (result is JArray jarray)
            {
                foreach (var item in jarray)
                {
                    var itemIds = ExtractVertexIdsFromResult(item);
                    vertexIds.AddRange(itemIds);
                }
            }
            else if (result is IEnumerable<object> enumerable && !(result is string))
            {
                foreach (var item in enumerable)
                {
                    var itemIds = ExtractVertexIdsFromResult(item);
                    vertexIds.AddRange(itemIds);
                }
            }
            else
            {
                // Check if the result itself might be a vertex or contains vertex information
                var vertex = TryParseAsVertex(result);
                if (vertex != null && !string.IsNullOrEmpty(vertex.Id))
                {
                    vertexIds.Add(vertex.Id);
                }
            }
        }
        catch (Exception ex)
        {
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Failed to extract vertex IDs from result: {ex.Message}");
        }

        return vertexIds;
    }

    /// <summary>
    /// Extract vertex IDs from JObject
    /// </summary>
    private void ExtractVertexIdsFromJObject(JObject jobj, List<string> vertexIds)
    {
        // Look for common vertex ID patterns
        var idProperties = new[] { "id", "vertexId", "@id", "vertex", "v" };
        
        foreach (var prop in jobj.Properties())
        {
            if (idProperties.Contains(prop.Name.ToLowerInvariant()))
            {
                var id = ExtractIdFromValue(prop.Value);
                if (!string.IsNullOrEmpty(id))
                    vertexIds.Add(id);
            }
            else if (prop.Value is JObject nestedObj)
            {
                ExtractVertexIdsFromJObject(nestedObj, vertexIds);
            }
            else if (prop.Value is JArray array)
            {
                foreach (var item in array)
                {
                    if (item is JObject itemObj)
                        ExtractVertexIdsFromJObject(itemObj, vertexIds);
                    else
                    {
                        var id = ExtractIdFromValue(item);
                        if (!string.IsNullOrEmpty(id))
                            vertexIds.Add(id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extract vertex IDs from Dictionary
    /// </summary>
    private void ExtractVertexIdsFromDictionary(IDictionary<string, object> dict, List<string> vertexIds)
    {
        var idProperties = new[] { "id", "vertexId", "@id", "vertex", "v" };
        
        foreach (var kvp in dict)
        {
            if (idProperties.Contains(kvp.Key.ToLowerInvariant()))
            {
                var id = ExtractIdFromValue(kvp.Value);
                if (!string.IsNullOrEmpty(id))
                    vertexIds.Add(id);
            }
            else if (kvp.Value is IDictionary<string, object> nestedDict)
            {
                ExtractVertexIdsFromDictionary(nestedDict, vertexIds);
            }
            else if (kvp.Value is IEnumerable<object> enumerable && !(kvp.Value is string))
            {
                foreach (var item in enumerable)
                {
                    if (item is IDictionary<string, object> itemDict)
                        ExtractVertexIdsFromDictionary(itemDict, vertexIds);
                    else
                    {
                        var id = ExtractIdFromValue(item);
                        if (!string.IsNullOrEmpty(id))
                            vertexIds.Add(id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extract ID from various value types
    /// </summary>
    private string ExtractIdFromValue(object value)
    {
        if (value == null) return string.Empty;

        if (value is JValue jval)
            return jval.Value?.ToString() ?? string.Empty;

        if (value is string str)
            return str;

        if (value is JArray jarray && jarray.Count > 0)
            return ExtractIdFromValue(jarray[0]);

        if (value is IEnumerable<object> enumerable && !(value is string))
        {
            var firstItem = enumerable.FirstOrDefault();
            if (firstItem != null)
                return ExtractIdFromValue(firstItem);
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Try to parse a result as a vertex
    /// </summary>
    private ExportedVertex? TryParseAsVertex(dynamic result)
    {
        try
        {
            // Try standard vertex parsing first
            var vertex = ParseVertex(result);
            if (vertex != null && !string.IsNullOrEmpty(vertex.Id))
                return vertex;

            // If that fails, try to extract vertex information from the structure
            if (result is JObject jobj)
            {
                var id = ExtractIdFromValue(jobj["id"]);
                if (!string.IsNullOrEmpty(id))
                {
                    return new ExportedVertex
                    {
                        Id = id,
                        Label = ExtractIdFromValue(jobj["label"]) ?? string.Empty
                    };
                }
            }
            else if (result is IDictionary<string, object> dict)
            {
                if (dict.ContainsKey("id"))
                {
                    var id = ExtractIdFromValue(dict["id"]);
                    if (!string.IsNullOrEmpty(id))
                    {
                        return new ExportedVertex
                        {
                            Id = id,
                            Label = dict.ContainsKey("label") ? ExtractIdFromValue(dict["label"]) ?? string.Empty : string.Empty
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Failed to parse as vertex: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Modify a complex query to extract vertices
    /// </summary>
    private string ModifyQueryForVertexExtraction(string originalQuery)
    {
        // Strategy 1: Remove final operations that might prevent vertex extraction
        var operationsToRemove = new[]
        {
            ".select(",
            ".project(",
            ".fold(",
            ".unfold(",
            ".count()",
            ".sum()",
            ".mean()",
            ".id()",
            ".label()",
            ".values(",
            ".valueMap("
        };

        var modifiedQuery = originalQuery;
        
        foreach (var operation in operationsToRemove)
        {
            var index = modifiedQuery.LastIndexOf(operation, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                if (operation.EndsWith("("))
                {
                    // Find the matching closing parenthesis
                    var openParen = 1;
                    var closingIndex = index + operation.Length;
                    while (closingIndex < modifiedQuery.Length && openParen > 0)
                    {
                        if (modifiedQuery[closingIndex] == '(') openParen++;
                        else if (modifiedQuery[closingIndex] == ')') openParen--;
                        closingIndex++;
                    }
                    if (openParen == 0)
                    {
                        modifiedQuery = modifiedQuery.Substring(0, index);
                        break;
                    }
                }
                else
                {
                    modifiedQuery = modifiedQuery.Substring(0, index);
                    break;
                }
            }
        }

        // Strategy 2: If that didn't help, try to extract the base vertex query
        if (modifiedQuery == originalQuery)
        {
            var patterns = new[]
            {
                @"g\.V\([^)]*\)",
                @"g\.V\(\)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(originalQuery, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    modifiedQuery = match.Value;
                    break;
                }
            }
        }

        return modifiedQuery;
    }

    /// <summary>
    /// Export vertices by their IDs and their connected edges
    /// </summary>
    public async Task<ExportedScenario> ExportByIdsAsync(List<string> vertexIds, string scenarioName, string? description = null)
    {
        if (vertexIds == null || vertexIds.Count == 0)
            throw new ArgumentException("Vertex IDs list cannot be null or empty", nameof(vertexIds));

        if (string.IsNullOrWhiteSpace(scenarioName))
            throw new ArgumentException("Scenario name cannot be empty", nameof(scenarioName));

        Console.WriteLine($"Exporting {vertexIds.Count} vertices by ID");

        // Create consolidated progress bar
        var estimatedEdgeCount = vertexIds.Count * 2; // Rough estimate
        using (var progress = ConsolidatedProgressBarFactory.CreateScenarioExportProgress(vertexIds.Count, estimatedEdgeCount, 2))
        {
            // Get vertices by IDs with properties
            var vertices = await FetchVerticesWithPropertiesAsync(vertexIds, progress);

            Console.WriteLine($"\nSuccessfully exported {vertices.Count} out of {vertexIds.Count} vertices");

            // Find all edges between these vertices
            var actualVertexIds = vertices.Select(v => v.Id).ToList();
            var edges = await FindEdgesBetweenVerticesAsync(actualVertexIds, progress);

            progress.IncrementFileProgress("Preparing scenario data");

            var scenario = new ExportedScenario
            {
                Name = scenarioName,
                Description = description ?? $"Exported vertices by IDs",
                ExportedAt = DateTime.UtcNow,
                SourceConnection = _connectionName,
                VertexIds = vertexIds,
                Vertices = vertices,
                Edges = edges,
                Metadata = new Dictionary<string, object>
                {
                    ["vertexCount"] = vertices.Count,
                    ["edgeCount"] = edges.Count,
                    ["requestedVertexCount"] = vertexIds.Count,
                    ["exportedBy"] = "Stardust.Paradox.Data.ScenarioConnector"
                }
            };

            progress.IncrementFileProgress("Scenario ready");
            return scenario;
        }
    }

    /// <summary>
    /// Fetch vertices with all their properties by IDs
    /// </summary>
    private async Task<List<ExportedVertex>> FetchVerticesWithPropertiesAsync(List<string> vertexIds, ConsolidatedProgressBar? progress = null)
    {
        var vertices = new List<ExportedVertex>();
        
        foreach (var vertexId in vertexIds)
        {
            try
            {
                // First, let's try a simple query to see what we get back
                var simpleQuery = BuildVertexByIdQuery(vertexId);
                if (EnableDebugLogging)
                    Console.WriteLine($"\nDebug: Trying simple query first: {simpleQuery}");
                
                var simpleResult = await _connector.ExecuteAsync(simpleQuery, new Dictionary<string, object>());
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Simple query returned {simpleResult.Count()} results");
                
                foreach (dynamic vertexData in simpleResult)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Simple vertex data structure: {JsonConvert.SerializeObject(vertexData, Formatting.Indented)}");
                }

                // Now try with valueMap(true)
                var propertyQuery = BuildVertexByIdQuery(vertexId) + ".valueMap(true)";
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Trying property query: {propertyQuery}");
                
                var result = await _connector.ExecuteAsync(propertyQuery, new Dictionary<string, object>());
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Property query returned {result.Count()} results");
                
                foreach (dynamic vertexData in result)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Property vertex data structure: {JsonConvert.SerializeObject(vertexData, Formatting.Indented)}");
                    
                    var vertex = ParseVertexFromValueMap(vertexData, vertexId);
                    if (vertex != null)
                        vertices.Add(vertex);
                }
                
                progress?.IncrementVertexProgress(vertexId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nWarning: Failed to fetch properties for vertex '{vertexId}': {ex.Message}");
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Exception details: {ex}");
                
                // Fallback: try basic vertex query without properties
                try
                {
                    var query = BuildVertexByIdQuery(vertexId);
                    var result = await _connector.ExecuteAsync(query, new Dictionary<string, object>());
                    
                    foreach (dynamic vertexData in result)
                    {
                        var vertex = ParseVertex(vertexData);
                        if (vertex != null)
                            vertices.Add(vertex);
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Warning: Failed to fetch vertex '{vertexId}' with fallback method: {ex2.Message}");
                }
                
                progress?.IncrementVertexProgress(vertexId);
            }
        }
        
        return vertices;
    }

    /// <summary>
    /// Build a query to get a vertex by ID, handling potential composite key formats
    /// </summary>
    private string BuildVertexByIdQuery(string vertexId)
    {
        // Check if the ID contains composite key format indicators
        // CosmosDB composite keys might be in format like "partitionKey|vertexId" or similar
        if (vertexId.Contains('|') || vertexId.Contains(','))
        {
            // Try to parse as composite key
            var parts = vertexId.Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var partitionKey = EscapeGremlinString(parts[0].Trim());
                var id = EscapeGremlinString(parts[1].Trim());
                return $"g.V(['{partitionKey}', '{id}'])";
            }
        }
        
        // Default to simple vertex ID
        return $"g.V('{EscapeGremlinString(vertexId)}')";
    }

    /// <summary>
    /// Find all edges between a set of vertices
    /// </summary>
    private async Task<List<ExportedEdge>> FindEdgesBetweenVerticesAsync(List<string> vertexIds, ConsolidatedProgressBar? progress = null)
    {
        if (vertexIds.Count == 0)
            return new List<ExportedEdge>();

        Console.WriteLine($"\nFinding edges between {vertexIds.Count} vertices...");

        var edges = new List<ExportedEdge>();

        try
        {
            // Strategy 1: Use individual vertex queries to avoid composite key issues
            // First get basic edge structure, then try to get properties
            foreach (var vertexId in vertexIds)
            {
                try
                {
                    // Get basic edge structure first
                    var escapedId = EscapeGremlinString(vertexId);
                    var vertexEdgeQuery = $"g.V('{escapedId}').bothE()";
                    
                    var vertexEdgeResults = await _connector.ExecuteAsync(vertexEdgeQuery, new Dictionary<string, object>());
                    
                    foreach (dynamic edge in vertexEdgeResults)
                    {
                        var exportedEdge = ParseEdge(edge);
                        if (exportedEdge != null)
                        {
                            // Only include edges where both endpoints are in our vertex set
                            if (vertexIds.Contains(exportedEdge.OutVertexId) && vertexIds.Contains(exportedEdge.InVertexId))
                            {
                                // Try to get properties for this edge
                                await TryFetchEdgeProperties(exportedEdge);
                                edges.Add(exportedEdge);
                            }
                        }
                    }
                    
                    progress?.IncrementEdgeProgress(vertexId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nWarning: Failed to get edges for vertex '{vertexId}': {ex.Message}");
                    progress?.IncrementEdgeProgress(vertexId);
                }
            }

            // Remove duplicates (edges will be found multiple times)
            var uniqueEdges = edges.GroupBy(e => e.Id)
                                  .Select(g => g.First())
                                  .ToList();

            Console.WriteLine($"\nFound {uniqueEdges.Count} edges between vertices");
            return uniqueEdges;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nWarning: Failed to find edges: {ex.Message}");
            
            // Fallback: Try alternative approach with simpler queries
            return await FindEdgesBetweenVerticesAlternativeAsync(vertexIds, progress);
        }
    }

    /// <summary>
    /// Alternative approach for finding edges when the main method fails
    /// </summary>
    private async Task<List<ExportedEdge>> FindEdgesBetweenVerticesAlternativeAsync(List<string> vertexIds, ConsolidatedProgressBar? progress = null)
    {
        Console.WriteLine("Trying alternative edge discovery approach...");
        var edges = new List<ExportedEdge>();

        try
        {
            // Strategy 2: Get all edges and filter in memory
            var allEdgesQuery = "g.E()";
            Console.WriteLine("Fetching all edges from database...");
            var allEdgeResults = await _connector.ExecuteAsync(allEdgesQuery, new Dictionary<string, object>());
            var edgesList = allEdgeResults.ToList();

            var processedCount = 0;
            foreach (dynamic edge in edgesList)
            {
                var exportedEdge = ParseEdge(edge);
                if (exportedEdge != null)
                {
                    // Only include edges where both endpoints are in our vertex set
                    if (vertexIds.Contains(exportedEdge.OutVertexId) && vertexIds.Contains(exportedEdge.InVertexId))
                    {
                        // Try to get properties for this edge
                        await TryFetchEdgeProperties(exportedEdge);
                        edges.Add(exportedEdge);
                    }
                }
                
                processedCount++;
                if (processedCount % 10 == 0) // Update progress every 10 edges to avoid too frequent updates
                {
                    progress?.UpdateEdgeProgress(processedCount, exportedEdge?.Id ?? "unknown");
                }
            }

            Console.WriteLine($"\nFound {edges.Count} edges using alternative approach");
            return edges;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nWarning: Alternative edge discovery also failed: {ex.Message}");
            
            // Final fallback without properties
            try
            {
                var allEdgesQuery = "g.E()";
                Console.WriteLine("Trying final fallback approach...");
                var allEdgeResults = await _connector.ExecuteAsync(allEdgesQuery, new Dictionary<string, object>());
                var edgesList = allEdgeResults.ToList();

                var processedCount = 0;
                foreach (dynamic edge in edgesList)
                {
                    var exportedEdge = ParseEdge(edge);
                    if (exportedEdge != null)
                    {
                        // Only include edges where both endpoints are in our vertex set
                        if (vertexIds.Contains(exportedEdge.OutVertexId) && vertexIds.Contains(exportedEdge.InVertexId))
                        {
                            edges.Add(exportedEdge);
                        }
                    }
                    
                    processedCount++;
                    if (processedCount % 10 == 0)
                    {
                        progress?.UpdateEdgeProgress(processedCount, exportedEdge?.Id ?? "unknown");
                    }
                }

                Console.WriteLine($"\nFound {edges.Count} edges using final fallback approach (without properties)");
                return edges;
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"\nWarning: All edge discovery methods failed: {ex2.Message}");
                return new List<ExportedEdge>();
            }
        }
    }

    /// <summary>
    /// Parse a vertex from Gremlin result
    /// </summary>
    private ExportedVertex? ParseVertex(dynamic vertex)
    {
        try
        {
            var exportedVertex = new ExportedVertex
            {
                Id = vertex.id?.ToString() ?? string.Empty,
                Label = vertex.label?.ToString() ?? string.Empty
            };

            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Parsing vertex {exportedVertex.Id} from Gremlin result");

            // Parse properties
            if (vertex.properties != null)
            {
                var props = vertex.properties as IDictionary<string, object>;
                if (props != null)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Vertex {exportedVertex.Id} has {props.Count} property entries from Gremlin result");
                    
                    foreach (var prop in props)
                    {
                        try
                        {
                            var extractedValue = ExtractPropertyValue(prop.Value);
                            exportedVertex.Properties[prop.Key] = extractedValue;
                            if (EnableDebugLogging)
                                Console.WriteLine($"Debug: Added vertex property {prop.Key} = {extractedValue} (type: {extractedValue?.GetType().Name})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Failed to parse property '{prop.Key}': {ex.Message}");
                            exportedVertex.Properties[prop.Key] = prop.Value;
                        }
                    }
                }
                else
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Vertex {exportedVertex.Id} properties is not a dictionary");
                }
            }
            else
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Vertex {exportedVertex.Id} has no properties object");
            }

            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Vertex {exportedVertex.Id} final property count: {exportedVertex.Properties.Count}");
            return exportedVertex;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse vertex: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse an edge from Gremlin result
    /// </summary>
    private ExportedEdge? ParseEdge(dynamic edge)
    {
        try
        {
            var exportedEdge = new ExportedEdge
            {
                Id = edge.id?.ToString() ?? string.Empty,
                Label = edge.label?.ToString() ?? string.Empty,
                OutVertexId = edge.outV?.ToString() ?? string.Empty,
                InVertexId = edge.inV?.ToString() ?? string.Empty
            };

            // Parse properties
            if (edge.properties != null)
            {
                var props = edge.properties as IDictionary<string, object>;
                if (props != null)
                {
                    foreach (var prop in props)
                    {
                        try
                        {
                            exportedEdge.Properties[prop.Key] = ExtractPropertyValue(prop.Value);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Failed to parse edge property '{prop.Key}': {ex.Message}");
                            exportedEdge.Properties[prop.Key] = prop.Value;
                        }
                    }
                }
            }

            return exportedEdge;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse edge: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse vertex from valueMap(true) result
    /// </summary>
    private ExportedVertex? ParseVertexFromValueMap(dynamic vertexData, string vertexId)
    {
        try
        {
            var vertex = new ExportedVertex
            {
                Id = vertexId
            };
            
            // Handle both JObject and IDictionary cases
            IDictionary<string, object>? data = null;
            
            if (vertexData is JObject jobj)
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Parsing vertex {vertexId} from JObject with {jobj.Properties().Count()} properties");
                
                // Convert JObject to dictionary
                data = jobj.ToObject<Dictionary<string, object>>();
            }
            else if (vertexData is IDictionary<string, object> dict)
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Parsing vertex {vertexId} from Dictionary with {dict.Count} entries");
                data = dict;
            }
            
            if (data != null && data.Count > 0)
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Processing vertex {vertexId} with {data.Count} data entries");
                
                // ValueMap(true) includes id, label, and properties
                if (data.ContainsKey("label"))
                {
                    var labelValue = data["label"];
                    if (labelValue is JArray labelArray)
                        vertex.Label = labelArray.FirstOrDefault()?.ToString() ?? string.Empty;
                    else if (labelValue is IEnumerable<object> labelEnumerable)
                        vertex.Label = labelEnumerable.FirstOrDefault()?.ToString() ?? string.Empty;
                    else
                        vertex.Label = labelValue?.ToString() ?? string.Empty;
                }
                
                // Handle id field if present (for cases where vertexId parameter is empty)
                if (string.IsNullOrEmpty(vertexId) && data.ContainsKey("id"))
                {
                    var idValue = data["id"];
                    if (idValue is JArray idArray)
                        vertex.Id = idArray.FirstOrDefault()?.ToString() ?? string.Empty;
                    else if (idValue is IEnumerable<object> idEnumerable)
                        vertex.Id = idEnumerable.FirstOrDefault()?.ToString() ?? string.Empty;
                    else
                        vertex.Id = idValue?.ToString() ?? string.Empty;
                }
                
                // All other keys are properties
                foreach (var kvp in data)
                {
                    if (kvp.Key != "id" && kvp.Key != "label")
                    {
                        var extractedValue = ExtractPropertyValue(kvp.Value);
                        vertex.Properties[kvp.Key] = extractedValue;
                        if (EnableDebugLogging)
                            Console.WriteLine($"Debug: Added property {kvp.Key} = {extractedValue} (type: {extractedValue?.GetType().Name})");
                    }
                }
                
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Vertex {vertex.Id} has {vertex.Properties.Count} properties");
            }
            else
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Vertex data for {vertexId} is empty or null");
                
                // If data is empty but we have a vertexId, still return a basic vertex
                if (!string.IsNullOrEmpty(vertexId))
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Returning basic vertex structure for {vertexId}");
                    return vertex; // Return vertex with just the ID
                }
            }
            
            return vertex;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse vertex from valueMap: {ex.Message}");
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: ParseVertexFromValueMap exception: {ex}");
            
            // Fallback: return basic vertex with just ID if available
            if (!string.IsNullOrEmpty(vertexId))
            {
                return new ExportedVertex { Id = vertexId };
            }
            return null;
        }
    }

    /// <summary>
    /// Try to fetch properties for an edge
    /// </summary>
    private async Task TryFetchEdgeProperties(ExportedEdge edge)
    {
        try
        {
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Fetching properties for edge {edge.Id}");
            
            // First try simple edge query to get basic structure
            var simpleEdgeQuery = $"g.E('{EscapeGremlinString(edge.Id)}')";
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Simple edge query: {simpleEdgeQuery}");
            
            var simpleEdgeResult = await _connector.ExecuteAsync(simpleEdgeQuery, new Dictionary<string, object>());
            if (EnableDebugLogging)
            {
                Console.WriteLine($"Debug: Simple edge query returned {simpleEdgeResult.Count()} results");
                foreach (dynamic edgeData in simpleEdgeResult)
                {
                    Console.WriteLine($"Debug: Simple edge data structure: {JsonConvert.SerializeObject(edgeData, Formatting.Indented)}");
                    
                    // Parse any properties directly from the basic edge structure
                    if (edgeData.properties != null)
                    {
                        var directProps = edgeData.properties as IDictionary<string, object>;
                        if (directProps != null && directProps.Count > 0)
                        {
                            Console.WriteLine($"Debug: Found {directProps.Count} properties in basic edge structure");
                            foreach (var prop in directProps)
                            {
                                try
                                {
                                    var extractedValue = ExtractPropertyValue(prop.Value);
                                    edge.Properties[prop.Key] = extractedValue;
                                    if (EnableDebugLogging)
                                        Console.WriteLine($"Debug: Added edge property from basic structure {prop.Key} = {extractedValue}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Warning: Failed to parse edge property '{prop.Key}' from basic structure: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            
            // If we already have properties from the basic structure, skip valueMap
            if (edge.Properties.Count > 0)
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Edge {edge.Id} already has {edge.Properties.Count} properties from basic structure, skipping valueMap");
                return;
            }
            
            // Try to get edge properties using valueMap() only if no properties found yet
            var edgePropsQuery = $"g.E('{EscapeGremlinString(edge.Id)}').valueMap()";
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Edge property query: {edgePropsQuery}");
            
            var propsResult = await _connector.ExecuteAsync(edgePropsQuery, new Dictionary<string, object>());
            
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Edge property query returned {propsResult.Count()} results");
            
            foreach (dynamic props in propsResult)
            {
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Edge property data structure: {JsonConvert.SerializeObject(props, Formatting.Indented)}");
                
                // Handle both JObject and IDictionary cases
                IDictionary<string, object>? propsData = null;
                
                if (props is JObject jobj)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Edge {edge.Id} properties from JObject with {jobj.Properties().Count()} properties");
                    propsData = jobj.ToObject<Dictionary<string, object>>();
                }
                else if (props is IDictionary<string, object> dict)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Edge {edge.Id} properties from Dictionary with {dict.Count} entries");
                    propsData = dict;
                }
                
                if (propsData != null && propsData.Count > 0)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Edge {edge.Id} has {propsData.Count} property entries from valueMap");
                    
                    foreach (var kvp in propsData)
                    {
                        var extractedValue = ExtractPropertyValue(kvp.Value);
                        edge.Properties[kvp.Key] = extractedValue;
                        if (EnableDebugLogging)
                            Console.WriteLine($"Debug: Added edge property from valueMap {kvp.Key} = {extractedValue} (type: {extractedValue?.GetType().Name})");
                    }
                }
                else
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Edge valueMap returned empty or null data for edge {edge.Id}");
                }
                break; // Only process first result
            }
            
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Edge {edge.Id} final property count: {edge.Properties.Count}");
        }
        catch (Exception ex)
        {
            // Properties fetch failed, but we still have the basic edge structure
            Console.WriteLine($"Warning: Failed to fetch properties for edge '{edge.Id}': {ex.Message}");
            if (EnableDebugLogging)
                Console.WriteLine($"Debug: Edge property fetch exception: {ex}");
        }
    }

    /// <summary>
    /// Detects if the database uses partition keys by examining vertex structure
    /// </summary>
    private async Task<bool> DetectPartitionKeyUsageAsync()
    {
        try
        {
            // Try to get a sample vertex to analyze its structure
            var sampleQuery = "g.V().limit(1)";
            var results = await _connector.ExecuteAsync(sampleQuery, new Dictionary<string, object>());
            
            foreach (dynamic vertex in results)
            {
                var id = vertex.id?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    // CosmosDB composite keys are often in array format or contain special characters
                    return id.StartsWith("[") || id.Contains(",") || id.Contains("|");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not detect partition key usage: {ex.Message}");
        }
        
        return false;
    }

    /// <summary>
    /// Extract property value handling CosmosDB property arrays and Newtonsoft.Json types
    /// </summary>
    private object ExtractPropertyValue(object value)
    {
        if (value == null)
            return null!;

        // Handle JValue (wraps primitive types)
        if (value is JValue jvalue)
            return jvalue.Value ?? null!;

        // Handle JArray
        if (value is JArray jarray)
        {
            if (jarray.Count > 0)
            {
                var firstValue = jarray[0];
                // Handle CosmosDB property format [{"id": "x", "value": "y"}]
                if (firstValue is JObject jobj && jobj.ContainsKey("value"))
                {
                    return ExtractPropertyValue(jobj["value"]);
                }
                return ExtractPropertyValue(firstValue);
            }
            return null!;
        }

        // Handle JObject
        if (value is JObject jobject)
        {
            if (jobject.ContainsKey("value"))
                return ExtractPropertyValue(jobject["value"]);
            
            // If it's a single key-value pair, return the value
            if (jobject.Properties().Count() == 1)
                return ExtractPropertyValue(jobject.Properties().First().Value);
                
            // Otherwise convert to dictionary
            return jobject.ToObject<Dictionary<string, object>>();
        }

        // Handle string values directly
        if (value is string stringValue)
            return stringValue;

        // Handle primitive types directly
        if (value is bool || value is int || value is long || value is float || value is double || value is DateTime)
            return value;

        // Handle arrays/collections
        if (value is IEnumerable<object> valueArray)
        {
            var firstValue = valueArray.FirstOrDefault();
            if (firstValue != null)
            {
                // Handle CosmosDB property format [{"id": "x", "value": "y"}]
                if (firstValue is IDictionary<string, object> propObj)
                {
                    if (propObj.ContainsKey("value"))
                    {
                        return propObj["value"];
                    }
                    // If no "value" key, return the whole object or try to extract meaningful data
                    return propObj.Count == 1 ? propObj.Values.FirstOrDefault() : propObj;
                }
                return firstValue;
            }
        }

        // Handle dictionary objects directly
        if (value is IDictionary<string, object> dictValue)
        {
            if (dictValue.ContainsKey("value"))
                return dictValue["value"];
            
            // If it's a single key-value pair, return the value
            if (dictValue.Count == 1)
                return dictValue.Values.FirstOrDefault();
                
            // Otherwise return the whole dictionary
            return dictValue;
        }

        return value;
    }

    /// <summary>
    /// Escape special characters in Gremlin strings
    /// </summary>
    private static string EscapeGremlinString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.Replace("'", "\\'")
                   .Replace("\"", "\\\"")
                   .Replace("\\", "\\\\");
    }

    /// <summary>
    /// Save scenario to file
    /// </summary>
    public static async Task SaveScenarioAsync(ExportedScenario scenario, string filePath)
    {
        var json = JsonConvert.SerializeObject(scenario, Formatting.Indented);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Load scenario from file
    /// </summary>
    public static async Task<ExportedScenario?> LoadScenarioAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonConvert.DeserializeObject<ExportedScenario>(json);
    }

    /// <summary>
    /// Test method to debug property fetching for a single vertex
    /// </summary>
    public async Task<ExportedVertex?> TestVertexPropertyFetchAsync(string vertexId)
    {
        EnableDebugLogging = true; // Force debug logging for this test
        Console.WriteLine($"=== Testing property fetch for vertex: {vertexId} ===");
        
        try
        {
            // Test 1: Basic vertex query
            Console.WriteLine("\n--- Test 1: Basic vertex query ---");
            var basicQuery = BuildVertexByIdQuery(vertexId);
            Console.WriteLine($"Query: {basicQuery}");
            
            var basicResult = await _connector.ExecuteAsync(basicQuery, new Dictionary<string, object>());
            Console.WriteLine($"Results count: {basicResult.Count()}");
            
            foreach (dynamic result in basicResult)
            {
                Console.WriteLine($"Basic result structure: {JsonConvert.SerializeObject(result, Formatting.Indented)}");
                var vertex = ParseVertex(result);
                if (vertex != null)
                {
                    Console.WriteLine($"Parsed vertex: ID={vertex.Id}, Label={vertex.Label}, Properties={vertex.Properties.Count}");
                }
            }
            
            // Test 2: ValueMap query
            Console.WriteLine("\n--- Test 2: ValueMap query ---");
            var valueMapQuery = BuildVertexByIdQuery(vertexId) + ".valueMap(true)";
            Console.WriteLine($"Query: {valueMapQuery}");
            
            var valueMapResult = await _connector.ExecuteAsync(valueMapQuery, new Dictionary<string, object>());
            Console.WriteLine($"Results count: {valueMapResult.Count()}");
            
            foreach (dynamic result in valueMapResult)
            {
                Console.WriteLine($"ValueMap result structure: {JsonConvert.SerializeObject(result, Formatting.Indented)}");
                var vertex = ParseVertexFromValueMap(result, vertexId);
                if (vertex != null)
                {
                    Console.WriteLine($"Parsed vertex: ID={vertex.Id}, Label={vertex.Label}, Properties={vertex.Properties.Count}");
                    foreach (var prop in vertex.Properties)
                    {
                        Console.WriteLine($"  Property: {prop.Key} = {prop.Value} ({prop.Value?.GetType().Name})");
                    }
                    return vertex;
                }
            }
            
            // Test 3: Properties only query
            Console.WriteLine("\n--- Test 3: Properties only query ---");
            var propsQuery = BuildVertexByIdQuery(vertexId) + ".properties()";
            Console.WriteLine($"Query: {propsQuery}");
            
            var propsResult = await _connector.ExecuteAsync(propsQuery, new Dictionary<string, object>());
            Console.WriteLine($"Results count: {propsResult.Count()}");
            
            foreach (dynamic result in propsResult)
            {
                Console.WriteLine($"Properties result structure: {JsonConvert.SerializeObject(result, Formatting.Indented)}");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            Console.WriteLine($"Exception details: {ex}");
        }
        
        Console.WriteLine("=== End of property fetch test ===\n");
        return null;
    }

    /// <summary>
    /// Enhance existing vertices with properties without losing their basic information
    /// </summary>
    private async Task<List<ExportedVertex>> EnhanceVerticesWithPropertiesAsync(List<ExportedVertex> vertices, ConsolidatedProgressBar? progress = null)
    {
        foreach (var vertex in vertices)
        {
            try
            {
                // Try to fetch properties for this vertex while preserving existing label
                var propertyQuery = BuildVertexByIdQuery(vertex.Id) + ".valueMap(true)";
                if (EnableDebugLogging)
                    Console.WriteLine($"\nDebug: Enhancing vertex {vertex.Id} with query: {propertyQuery}");
                
                var result = await _connector.ExecuteAsync(propertyQuery, new Dictionary<string, object>());
                
                foreach (dynamic vertexData in result)
                {
                    if (EnableDebugLogging)
                        Console.WriteLine($"Debug: Enhancement result for {vertex.Id}: {JsonConvert.SerializeObject(vertexData, Formatting.Indented)}");
                    
                    // Handle both JObject and IDictionary cases
                    IDictionary<string, object>? data = null;
                    
                    if (vertexData is JObject jobj)
                    {
                        if (EnableDebugLogging)
                            Console.WriteLine($"Debug: Enhancement data is JObject with {jobj.Properties().Count()} properties");
                        data = jobj.ToObject<Dictionary<string, object>>();
                    }
                    else if (vertexData is IDictionary<string, object> dict)
                    {
                        if (EnableDebugLogging)
                            Console.WriteLine($"Debug: Enhancement data is Dictionary with {dict.Count} entries");
                        data = dict;
                    }
                    
                    if (data != null && data.Count > 0)
                    {
                        // Only update label if it's currently empty and we have a better one
                        if (string.IsNullOrEmpty(vertex.Label) && data.ContainsKey("label"))
                        {
                            var labelValue = data["label"];
                            if (labelValue is JArray labelJArray)
                                vertex.Label = labelJArray.FirstOrDefault()?.ToString() ?? vertex.Label;
                            else if (labelValue is IEnumerable<object> labelArray)
                                vertex.Label = labelArray.FirstOrDefault()?.ToString() ?? vertex.Label;
                            else
                                vertex.Label = labelValue?.ToString() ?? vertex.Label;
                        }
                        
                        // Add properties
                        foreach (var kvp in data)
                        {
                            if (kvp.Key != "id" && kvp.Key != "label")
                            {
                                var extractedValue = ExtractPropertyValue(kvp.Value);
                                vertex.Properties[kvp.Key] = extractedValue;
                                if (EnableDebugLogging)
                                    Console.WriteLine($"Debug: Enhanced vertex {vertex.Id} with property {kvp.Key} = {extractedValue}");
                            }
                        }
                    }
                    else
                    {
                        if (EnableDebugLogging)
                            Console.WriteLine($"Debug: No enhancement data for vertex {vertex.Id}");
                    }
                    break; // Only process first result
                }
                
                progress?.IncrementVertexProgress(vertex.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nWarning: Failed to enhance vertex '{vertex.Id}' with properties: {ex.Message}");
                if (EnableDebugLogging)
                    Console.WriteLine($"Debug: Enhancement exception: {ex}");
                
                progress?.IncrementVertexProgress(vertex.Id);
            }
        }
        
        return vertices;
    }
}