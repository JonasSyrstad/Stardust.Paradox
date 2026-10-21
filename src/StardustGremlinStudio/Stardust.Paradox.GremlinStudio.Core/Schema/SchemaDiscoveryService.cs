using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Schema;

/// <summary>
/// Implementation of schema discovery service using Gremlin queries.
/// </summary>
public class SchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly ILogger<SchemaDiscoveryService> _logger;

    public SchemaDiscoveryService(ILogger<SchemaDiscoveryService> logger)
    {
        _logger = logger;
    }

    public async Task<GraphSchema> DiscoverSchemaAsync(
        IGremlinLanguageConnector connector,
        int sampleSize = 100,
        IProgress<SchemaDiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var schema = new GraphSchema();

        try
        {
            // Step 1: Discover vertex labels
            progress?.Report(new SchemaDiscoveryProgress
            {
                StepName = "Discovering Labels",
                StatusMessage = "Finding vertex labels...",
                PercentComplete = 5
            });

            var vertexLabels = await DiscoverVertexLabelsAsync(connector, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Found {Count} vertex labels", vertexLabels.Count);

            // Step 2: Discover edge labels
            progress?.Report(new SchemaDiscoveryProgress
            {
                StepName = "Discovering Labels",
                StatusMessage = "Finding edge labels...",
                PercentComplete = 10
            });

            var edgeLabels = await DiscoverEdgeLabelsAsync(connector, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Found {Count} edge labels", edgeLabels.Count);

            var totalLabels = vertexLabels.Count + edgeLabels.Count;
            var processedLabels = 0;

            // Step 3: Analyze each vertex label
            foreach (var label in vertexLabels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedLabels++;
                var percentComplete = 10 + (int)(processedLabels * 45.0 / Math.Max(1, totalLabels));

                progress?.Report(new SchemaDiscoveryProgress
                {
                    StepName = "Analyzing Vertices",
                    StatusMessage = $"Analyzing vertex label '{label}'...",
                    PercentComplete = percentComplete,
                    CurrentItem = processedLabels,
                    TotalItems = totalLabels
                });

                var vertexLabel = await AnalyzeVertexLabelAsync(connector, label, sampleSize, cancellationToken).ConfigureAwait(false);
                schema.VertexLabels.Add(vertexLabel);
                schema.VertexSampleCount += vertexLabel.Count;
            }

            // Step 4: Analyze each edge label
            foreach (var label in edgeLabels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedLabels++;
                var percentComplete = 10 + (int)(processedLabels * 45.0 / Math.Max(1, totalLabels));

                progress?.Report(new SchemaDiscoveryProgress
                {
                    StepName = "Analyzing Edges",
                    StatusMessage = $"Analyzing edge label '{label}'...",
                    PercentComplete = percentComplete,
                    CurrentItem = processedLabels,
                    TotalItems = totalLabels
                });

                var edgeLabel = await AnalyzeEdgeLabelAsync(connector, label, sampleSize, cancellationToken).ConfigureAwait(false);
                schema.EdgeLabels.Add(edgeLabel);
                schema.EdgeSampleCount += edgeLabel.Count;
            }

            // Step 5: Discover edge connections between vertex types
            progress?.Report(new SchemaDiscoveryProgress
            {
                StepName = "Mapping Connections",
                StatusMessage = "Discovering edge connections between vertex types...",
                PercentComplete = 85
            });

            await DiscoverEdgeConnectionsAsync(connector, schema, cancellationToken).ConfigureAwait(false);

            progress?.Report(new SchemaDiscoveryProgress
            {
                StepName = "Complete",
                StatusMessage = $"Discovered {schema.VertexLabels.Count} vertex labels, {schema.EdgeLabels.Count} edge labels",
                PercentComplete = 100
            });

            schema.DiscoveredAt = DateTime.UtcNow;
            return schema;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Schema discovery was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover schema");
            throw;
        }
    }

    public async Task<List<string>> DiscoverVertexLabelsAsync(
        IGremlinLanguageConnector connector,
        CancellationToken cancellationToken = default)
    {
        var query = "g.V().label().dedup()";
        var results = await ExecuteQueryAsync(connector, query, cancellationToken).ConfigureAwait(false);
        return ParseStringArray(results);
    }

    public async Task<List<string>> DiscoverEdgeLabelsAsync(
        IGremlinLanguageConnector connector,
        CancellationToken cancellationToken = default)
    {
        var query = "g.E().label().dedup()";
        var results = await ExecuteQueryAsync(connector, query, cancellationToken).ConfigureAwait(false);
        return ParseStringArray(results);
    }

    private async Task<SchemaVertexLabel> AnalyzeVertexLabelAsync(
        IGremlinLanguageConnector connector,
        string label,
        int sampleSize,
        CancellationToken cancellationToken)
    {
        var schemaLabel = new SchemaVertexLabel { Label = label };

        // Get count
        var countQuery = $"g.V().hasLabel('{EscapeGremlin(label)}').count()";
        var countResult = await ExecuteQueryAsync(connector, countQuery, cancellationToken).ConfigureAwait(false);
        schemaLabel.Count = ParseCount(countResult);

        // Get sample vertices to discover properties
        var sampleQuery = $"g.V().hasLabel('{EscapeGremlin(label)}').limit({sampleSize}).valueMap(true)";
        var sampleResult = await ExecuteQueryAsync(connector, sampleQuery, cancellationToken).ConfigureAwait(false);

        schemaLabel.Properties = AnalyzeProperties(sampleResult, schemaLabel.Count);

        return schemaLabel;
    }

    private async Task<SchemaEdgeLabel> AnalyzeEdgeLabelAsync(
        IGremlinLanguageConnector connector,
        string label,
        int sampleSize,
        CancellationToken cancellationToken)
    {
        var schemaLabel = new SchemaEdgeLabel { Label = label };

        // Get count
        var countQuery = $"g.E().hasLabel('{EscapeGremlin(label)}').count()";
        var countResult = await ExecuteQueryAsync(connector, countQuery, cancellationToken).ConfigureAwait(false);
        schemaLabel.Count = ParseCount(countResult);

        // Get sample edges to discover properties
        var sampleQuery = $"g.E().hasLabel('{EscapeGremlin(label)}').limit({sampleSize}).valueMap(true)";
        var sampleResult = await ExecuteQueryAsync(connector, sampleQuery, cancellationToken).ConfigureAwait(false);

        schemaLabel.Properties = AnalyzeProperties(sampleResult, schemaLabel.Count);

        // Get source vertex labels
        var sourceQuery = $"g.E().hasLabel('{EscapeGremlin(label)}').outV().label().dedup()";
        var sourceResult = await ExecuteQueryAsync(connector, sourceQuery, cancellationToken).ConfigureAwait(false);
        schemaLabel.SourceLabels = ParseStringArray(sourceResult);

        // Get target vertex labels
        var targetQuery = $"g.E().hasLabel('{EscapeGremlin(label)}').inV().label().dedup()";
        var targetResult = await ExecuteQueryAsync(connector, targetQuery, cancellationToken).ConfigureAwait(false);
        schemaLabel.TargetLabels = ParseStringArray(targetResult);

        return schemaLabel;
    }

    private async Task DiscoverEdgeConnectionsAsync(
        IGremlinLanguageConnector connector,
        GraphSchema schema,
        CancellationToken cancellationToken)
    {
        foreach (var vertexLabel in schema.VertexLabels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find outgoing edges
            var outQuery = $"g.V().hasLabel('{EscapeGremlin(vertexLabel.Label)}').outE().project('edge','target').by(label()).by(inV().label()).dedup()";
            var outResult = await ExecuteQueryAsync(connector, outQuery, cancellationToken).ConfigureAwait(false);
            vertexLabel.OutgoingEdges = ParseEdgeConnections(outResult, true);

            // Find incoming edges
            var inQuery = $"g.V().hasLabel('{EscapeGremlin(vertexLabel.Label)}').inE().project('edge','source').by(label()).by(outV().label()).dedup()";
            var inResult = await ExecuteQueryAsync(connector, inQuery, cancellationToken).ConfigureAwait(false);
            vertexLabel.IncomingEdges = ParseEdgeConnections(inResult, false);
        }
    }

    private List<SchemaEdgeConnection> ParseEdgeConnections(string json, bool isOutgoing)
    {
        var connections = new List<SchemaEdgeConnection>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        var edgeLabel = element.TryGetProperty("edge", out var edgeProp) ? edgeProp.GetString() ?? "" : "";
                        var vertexLabel = isOutgoing
                            ? (element.TryGetProperty("target", out var targetProp) ? targetProp.GetString() ?? "" : "")
                            : (element.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() ?? "" : "");

                        if (!string.IsNullOrEmpty(edgeLabel) && !string.IsNullOrEmpty(vertexLabel))
                        {
                            // Check if we already have this edge label
                            var existing = connections.FirstOrDefault(c => c.EdgeLabel == edgeLabel && c.VertexLabel == vertexLabel);
                            if (existing == null)
                            {
                                connections.Add(new SchemaEdgeConnection
                                {
                                    EdgeLabel = edgeLabel,
                                    VertexLabel = vertexLabel,
                                    IsCollection = true // Assume collection by default
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse edge connections");
        }

        return connections;
    }

    private List<SchemaProperty> AnalyzeProperties(string json, int totalCount)
    {
        var propertyMap = new Dictionary<string, SchemaProperty>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var sampleCount = 0;
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    sampleCount++;
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in element.EnumerateObject())
                        {
                            // Skip internal properties
                            if (prop.Name == "id" || prop.Name == "label")
                                continue;

                            if (!propertyMap.TryGetValue(prop.Name, out var schemaProp))
                            {
                                schemaProp = new SchemaProperty
                                {
                                    Name = prop.Name,
                                    InferredType = "string"
                                };
                                propertyMap[prop.Name] = schemaProp;
                            }

                            schemaProp.OccurrenceCount++;
                            UpdatePropertyType(schemaProp, prop.Value);
                        }
                    }
                }

                // Determine nullability based on occurrence
                foreach (var prop in propertyMap.Values)
                {
                    prop.IsNullable = prop.OccurrenceCount < sampleCount;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze properties");
        }

        return propertyMap.Values.OrderBy(p => p.Name).ToList();
    }

    private void UpdatePropertyType(SchemaProperty prop, JsonElement value)
    {
        string inferredType;

        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                prop.IsCollection = true;
                if (value.GetArrayLength() > 0)
                {
                    // Check the first element type
                    var firstElement = value.EnumerateArray().First();
                    inferredType = InferTypeFromElement(firstElement);
                }
                else
                {
                    inferredType = "string";
                }
                break;

            default:
                inferredType = InferTypeFromElement(value);
                break;
        }

        // Keep the most specific type (prefer numeric over string)
        if (prop.InferredType == "string" || string.IsNullOrEmpty(prop.InferredType))
        {
            prop.InferredType = inferredType;
        }
        else if (prop.InferredType != inferredType)
        {
            // If types conflict, use object or string
            prop.InferredType = "string";
        }

        // Add sample value
        if (prop.SampleValues.Count < 3)
        {
            var sampleValue = value.ValueKind == JsonValueKind.Array
                ? "[array]"
                : value.ToString();
            if (!string.IsNullOrEmpty(sampleValue) && sampleValue.Length <= 50)
            {
                prop.SampleValues.Add(sampleValue);
            }
        }
    }

    private string InferTypeFromElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => TryParseAsDateTime(element.GetString()) ? "DateTime" : "string",
            JsonValueKind.Number => element.TryGetInt64(out _) ? "long" : "double",
            JsonValueKind.True or JsonValueKind.False => "bool",
            JsonValueKind.Null => "string",
            _ => "string"
        };
    }

    private bool TryParseAsDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // Check for ISO 8601 format patterns
        return DateTime.TryParse(value, out _) &&
               (value.Contains('T') || value.Contains('-'));
    }

    private async Task<string> ExecuteQueryAsync(
        IGremlinLanguageConnector connector,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await connector.ExecuteAsync(query, new Dictionary<string, object>()).ConfigureAwait(false);

            // Handle different result types - result is IEnumerable<dynamic>
            if (result == null)
                return "[]";

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query failed: {Query}", query);
            return "[]";
        }
    }

    private List<string> ParseStringArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
        }
        catch { }

        return new List<string>();
    }

    private int ParseCount(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                if (first.ValueKind == JsonValueKind.Number)
                    return first.GetInt32();
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Number)
            {
                return doc.RootElement.GetInt32();
            }
        }
        catch { }

        return 0;
    }

    private static string EscapeGremlin(string value)
    {
        return value.Replace("'", "\\'").Replace("\\", "\\\\");
    }
}
