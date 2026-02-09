using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Export;

/// <summary>
/// Implementation of scenario export service.
/// Matches the behavior of Stardust.Paradox.Data.ScenarioConnector.
/// </summary>
public class ScenarioExportService : IScenarioExportService
{
    private readonly ILogger<ScenarioExportService> _logger;

    public ScenarioExportService(ILogger<ScenarioExportService> logger)
    {
        _logger = logger;
    }

    public ScenarioData ParseQueryResults(string resultJson, string scenarioName, string? description, string? sourceConnection, string? query)
    {
        var data = new ScenarioData
        {
            Name = scenarioName,
            Description = description ?? $"Exported scenario: {scenarioName}",
            ExportedAt = DateTime.UtcNow,
            SourceConnection = sourceConnection,
            ExportQuery = query
        };

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    ParseElement(element, data);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                ParseElement(root, data);
            }

            data.Metadata.VertexCount = data.Vertices.Count;
            data.Metadata.EdgeCount = data.Edges.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse query results for scenario export");
        }

        return data;
    }

    /// <summary>
    /// Fetches edges between the vertices in the scenario data from the database.
    /// This matches the behavior of Stardust.Paradox.Data.ScenarioConnector.ScenarioExporter.FindEdgesBetweenVerticesAsync.
    /// </summary>
    public async Task<ScenarioData> FetchEdgesAsync(ScenarioData data, IGremlinLanguageConnector connector, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (data.Vertices.Count == 0)
            return data;

        var vertexIds = data.Vertices.Select(v => v.Id).ToList();
        var vertexIdSet = vertexIds.ToHashSet();
        var edges = new List<ScenarioEdge>();
        var seenEdgeIds = new HashSet<string>();

        _logger.LogInformation("Fetching edges between {VertexCount} vertices", vertexIds.Count);

        const int batchSize = 100;
        var totalVertices = vertexIds.Count;
        var processedCount = 0;

        // Process vertices in batches of 100
        for (int batchStart = 0; batchStart < totalVertices; batchStart += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchEnd = Math.Min(batchStart + batchSize, totalVertices);
            var batchVertexIds = vertexIds.Skip(batchStart).Take(batchSize).ToList();

            // Report progress
            progress?.Report($"Fetching edges for vertices {batchStart + 1}-{batchEnd} of {totalVertices}...");

            foreach (var vertexId in batchVertexIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Query edges connected to this vertex (same as ScenarioConnector)
                    var escapedId = EscapeGremlinString(vertexId);
                    var edgeQuery = $"g.V('{escapedId}').bothE()";

                    var edgeResults = await connector.ExecuteAsync(edgeQuery, new Dictionary<string, object>());

                    foreach (dynamic edge in edgeResults)
                    {
                        var exportedEdge = ParseEdgeFromDynamic(edge);
                        if (exportedEdge != null)
                        {
                            // Only include edges where both endpoints are in our vertex set
                            if (vertexIdSet.Contains(exportedEdge.OutVertexId) && 
                                vertexIdSet.Contains(exportedEdge.InVertexId) &&
                                !seenEdgeIds.Contains(exportedEdge.Id))
                            {
                                seenEdgeIds.Add(exportedEdge.Id);
                                edges.Add(exportedEdge);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch edges for vertex {VertexId}", vertexId);
                }

                processedCount++;
            }
        }

        data.Edges = edges;
        data.Metadata.EdgeCount = edges.Count;
        
        _logger.LogInformation("Found {EdgeCount} edges between vertices", edges.Count);
        
        return data;
    }

    private ScenarioEdge? ParseEdgeFromDynamic(dynamic edge)
    {
        try
        {
            string? id = null;
            string? label = null;
            string? outV = null;
            string? inV = null;
            var properties = new Dictionary<string, object?>();

            // Handle different edge result formats
            if (edge is JObject jobj)
            {
                id = jobj["id"]?.ToString();
                label = jobj["label"]?.ToString();
                outV = jobj["outV"]?.ToString();
                inV = jobj["inV"]?.ToString();

                if (jobj["properties"] is JObject propsObj)
                {
                    foreach (var prop in propsObj.Properties())
                    {
                        properties[prop.Name] = GetJTokenValue(prop.Value);
                    }
                }
            }
            else if (edge is IDictionary<string, object> dict)
            {
                id = dict.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                label = dict.TryGetValue("label", out var labelVal) ? labelVal?.ToString() : null;
                outV = dict.TryGetValue("outV", out var outVVal) ? outVVal?.ToString() : null;
                inV = dict.TryGetValue("inV", out var inVVal) ? inVVal?.ToString() : null;

                if (dict.TryGetValue("properties", out var propsVal) && propsVal is IDictionary<string, object> propsDict)
                {
                    foreach (var prop in propsDict)
                    {
                        properties[prop.Key] = prop.Value;
                    }
                }
            }
            else
            {
                // Try to access properties dynamically
                id = edge.id?.ToString();
                label = edge.label?.ToString();
                outV = edge.outV?.ToString();
                inV = edge.inV?.ToString();
            }

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(outV) || string.IsNullOrEmpty(inV))
                return null;

            return new ScenarioEdge
            {
                Id = id,
                Label = label ?? "edge",
                OutVertexId = outV,
                InVertexId = inV,
                Properties = properties
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse edge from dynamic object");
            return null;
        }
    }

    private object? GetJTokenValue(JToken token)
    {
        return token.Type switch
        {
            JTokenType.String => token.Value<string>(),
            JTokenType.Integer => token.Value<long>(),
            JTokenType.Float => token.Value<double>(),
            JTokenType.Boolean => token.Value<bool>(),
            JTokenType.Null => null,
            _ => token.ToString()
        };
    }

    private string EscapeGremlinString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        return input.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private void ParseElement(JsonElement element, ScenarioData data)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        // Check if it's a vertex or edge
        if (element.TryGetProperty("type", out var typeElement))
        {
            var type = typeElement.GetString();
            if (type == "vertex")
            {
                ParseVertex(element, data);
            }
            else if (type == "edge")
            {
                ParseEdge(element, data);
            }
        }
        else if (element.TryGetProperty("label", out _))
        {
            // Heuristic: if it has inV/outV, it's an edge
            if (element.TryGetProperty("inV", out _) || element.TryGetProperty("outV", out _))
            {
                ParseEdge(element, data);
            }
            else
            {
                ParseVertex(element, data);
            }
        }
    }

    private void ParseVertex(JsonElement element, ScenarioData data)
    {
        var vertex = new ScenarioVertex
        {
            Id = GetStringProperty(element, "id") ?? Guid.NewGuid().ToString(),
            Label = GetStringProperty(element, "label") ?? "vertex"
        };

        if (element.TryGetProperty("properties", out var propsElement))
        {
            vertex.Properties = ParseProperties(propsElement);
        }

        // Avoid duplicates
        if (!data.Vertices.Any(v => v.Id == vertex.Id))
        {
            data.Vertices.Add(vertex);
        }
    }

    private void ParseEdge(JsonElement element, ScenarioData data)
    {
        var edge = new ScenarioEdge
        {
            Id = GetStringProperty(element, "id") ?? Guid.NewGuid().ToString(),
            Label = GetStringProperty(element, "label") ?? "edge",
            OutVertexId = GetStringProperty(element, "outV") ?? "",
            InVertexId = GetStringProperty(element, "inV") ?? ""
        };

        if (element.TryGetProperty("properties", out var propsElement))
        {
            edge.Properties = ParseProperties(propsElement);
        }

        // Avoid duplicates
        if (!data.Edges.Any(e => e.Id == edge.Id))
        {
            data.Edges.Add(edge);
        }
    }

    private Dictionary<string, object?> ParseProperties(JsonElement propsElement)
    {
        var props = new Dictionary<string, object?>();

        if (propsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsElement.EnumerateObject())
            {
                // Cosmos DB format: properties are arrays of {id, value}
                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                {
                    var firstItem = prop.Value[0];
                    if (firstItem.TryGetProperty("value", out var valueElement))
                    {
                        props[prop.Name] = GetJsonValue(valueElement);
                    }
                    else
                    {
                        props[prop.Name] = GetJsonValue(firstItem);
                    }
                }
                else
                {
                    props[prop.Name] = GetJsonValue(prop.Value);
                }
            }
        }

        return props;
    }

    private string? GetStringProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        }
        return null;
    }

    private object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    public string Export(ScenarioData data, ScenarioExportFormat format)
    {
        return format switch
        {
            ScenarioExportFormat.Json => ExportToJson(data),
            ScenarioExportFormat.CSharp => ExportToCSharp(data),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private string ExportToJson(ScenarioData data)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        return System.Text.Json.JsonSerializer.Serialize(data, options);
    }

    private string ExportToCSharp(ScenarioData data)
    {
        var sb = new StringBuilder();
        var className = SanitizeClassName(data.Name);
        var namespaceName = string.IsNullOrWhiteSpace(data.Namespace) 
            ? "Stardust.Paradox.InMemory.Scenarios" 
            : data.Namespace;

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Stardust.Paradox.Data.InMemory.Core;");
        sb.AppendLine("using Stardust.Paradox.Data.InMemory.Scenarios;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {EscapeXmlComment(data.Description)}");
        if (!string.IsNullOrEmpty(data.SourceConnection))
            sb.AppendLine($"    /// Exported from: {data.SourceConnection}");
        sb.AppendLine($"    /// Export Date: {data.ExportedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public class {className}Scenario : InMemoryScenarioProviderBase");
        sb.AppendLine("    {");
        sb.AppendLine($"        public override string ScenarioName => \"{EscapeCSharpString(data.Name)}\";");
        sb.AppendLine($"        public override string Description => \"{EscapeCSharpString(data.Description)}\";");
        sb.AppendLine();
        sb.AppendLine("        protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()");
        sb.AppendLine("        {");

        // Vertices
        sb.AppendLine("            var vertices = new ScenarioVertexDefinition[]");
        sb.AppendLine("            {");
        for (int i = 0; i < data.Vertices.Count; i++)
        {
            var v = data.Vertices[i];
            var comma = i < data.Vertices.Count - 1 ? "," : "";
            if (v.Properties.Count > 0)
            {
                sb.AppendLine($"                new ScenarioVertexDefinition(\"{EscapeCSharpString(v.Id)}\", \"{EscapeCSharpString(v.Label)}\", Props(");
                WriteProperties(sb, v.Properties, "                ");
                sb.AppendLine($"                )){comma}");
            }
            else
            {
                sb.AppendLine($"                new ScenarioVertexDefinition(\"{EscapeCSharpString(v.Id)}\", \"{EscapeCSharpString(v.Label)}\"){comma}");
            }
        }
        sb.AppendLine("            };");
        sb.AppendLine();

        // Edges
        sb.AppendLine("            var edges = new ScenarioEdgeDefinition[]");
        sb.AppendLine("            {");
        for (int i = 0; i < data.Edges.Count; i++)
        {
            var e = data.Edges[i];
            var comma = i < data.Edges.Count - 1 ? "," : "";
            if (e.Properties.Count > 0)
            {
                sb.AppendLine($"                new ScenarioEdgeDefinition(\"{EscapeCSharpString(e.Id)}\", \"{EscapeCSharpString(e.Label)}\", \"{EscapeCSharpString(e.OutVertexId)}\", \"{EscapeCSharpString(e.InVertexId)}\", Props(");
                WriteProperties(sb, e.Properties, "                ");
                sb.AppendLine($"                )){comma}");
            }
            else
            {
                sb.AppendLine($"                new ScenarioEdgeDefinition(\"{EscapeCSharpString(e.Id)}\", \"{EscapeCSharpString(e.Label)}\", \"{EscapeCSharpString(e.OutVertexId)}\", \"{EscapeCSharpString(e.InVertexId)}\"){comma}");
            }
        }
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("            return (vertices, edges);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Add any custom query responses here if needed");
        sb.AppendLine("            base.ConfigureCustomResponses(database);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }


    private void WriteProperties(StringBuilder sb, Dictionary<string, object?> props, string indent)
    {
        var propsList = props.ToList();
        for (int i = 0; i < propsList.Count; i++)
        {
            var (key, value) = propsList[i];
            var comma = i < propsList.Count - 1 ? "," : "";
            var valueStr = FormatCSharpValue(value);
            sb.AppendLine($"{indent}(\"{EscapeCSharpString(key)}\", {valueStr}){comma}");
        }
    }

    private string FormatCSharpValue(object? value)
    {
        return value switch
        {
            null => "null",
            string s => $"\"{EscapeCSharpString(s)}\"",
            bool b => b.ToString().ToLowerInvariant(),
            DateTime dt => $"new DateTime({dt.Ticks}L)",
            double d => $"{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}d",
            float f => $"{f.ToString(System.Globalization.CultureInfo.InvariantCulture)}f",
            long l => $"{l}L",
            int i => i.ToString(),
            _ => $"\"{EscapeCSharpString(value.ToString() ?? "")}\""
        };
    }

    private string SanitizeClassName(string name)
    {
        var sb = new StringBuilder();
        bool capitalizeNext = true;


        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpper(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        var result = sb.ToString();
        if (result.Length > 0 && char.IsDigit(result[0]))
            result = "_" + result;

        return string.IsNullOrEmpty(result) ? "ExportedScenario" : result;
    }

    private string EscapeCSharpString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private string EscapeXmlComment(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public async Task SaveAsync(ScenarioData data, ScenarioExportFormat format, string filePath)
    {
        var content = Export(data, format);
        await File.WriteAllTextAsync(filePath, content);
        _logger.LogInformation("Saved scenario to {FilePath}", filePath);
    }
}
