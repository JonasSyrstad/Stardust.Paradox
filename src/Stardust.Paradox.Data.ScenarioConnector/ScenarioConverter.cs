using Stardust.Paradox.Data.InMemory.Scenarios;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Converts exported scenarios to InMemory scenario format
/// </summary>
public class ScenarioConverter
{
    /// <summary>
    /// Convert an exported scenario to an InMemory scenario provider
    /// </summary>
    public static DynamicInMemoryScenarioProvider ConvertToInMemoryScenario(ExportedScenario exportedScenario)
    {
        return new DynamicInMemoryScenarioProvider(exportedScenario);
    }

    /// <summary>
    /// Save the exported scenario as a C# class file that implements IInMemoryScenarioProvider
    /// </summary>
    public static async Task SaveAsCSharpClassAsync(ExportedScenario scenario, string filePath, string? namespaceName = null, string? className = null)
    {
        var actualNamespace = namespaceName ?? "GeneratedScenarios";
        var actualClassName = className ?? ToValidClassName(scenario.Name) + "Scenario";

        var csharpCode = GenerateCSharpClass(scenario, actualNamespace, actualClassName);
        await File.WriteAllTextAsync(filePath, csharpCode);
    }

    /// <summary>
    /// Generate C# class code for the scenario
    /// </summary>
    private static string GenerateCSharpClass(ExportedScenario scenario, string namespaceName, string className)
    {
        var code = $@"using System;
using System.Collections.Generic;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace {namespaceName}
{{
    /// <summary>
    /// {EscapeXmlComment(scenario.Description)}
    /// Exported from: {EscapeXmlComment(scenario.SourceConnection)}
    /// Export Date: {scenario.ExportedAt:yyyy-MM-dd HH:mm:ss} UTC
    /// </summary>
    public class {className} : InMemoryScenarioProviderBase
    {{
        public override string ScenarioName => ""{EscapeCSharpString(scenario.Name)}"";
        public override string Description => ""{EscapeCSharpString(scenario.Description)}"";

        protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
        {{
            var vertices = new ScenarioVertexDefinition[]
            {{
{GenerateVerticesCode(scenario.Vertices)}
            }};

            var edges = new ScenarioEdgeDefinition[]
            {{
{GenerateEdgesCode(scenario.Edges)}
            }};

            return (vertices, edges);
        }}

        protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
        {{
            // Add any custom query responses here if needed
            base.ConfigureCustomResponses(database);
        }}
    }}
}}";

        return code;
    }

    /// <summary>
    /// Generate C# code for vertices
    /// </summary>
    private static string GenerateVerticesCode(List<ExportedVertex> vertices)
    {
        var vertexCode = new List<string>();

        foreach (var vertex in vertices)
        {
            var properties = string.Empty;
            if (vertex.Properties.Count > 0)
            {
                var propPairs = vertex.Properties.Select(prop => 
                    $@"                (""{EscapeCSharpString(prop.Key)}"", {FormatPropertyValue(prop.Value)})");
                properties = $@", Props(
{string.Join(",\n", propPairs)}
                )";
            }

            vertexCode.Add($@"                new ScenarioVertexDefinition(""{EscapeCSharpString(vertex.Id)}"", ""{EscapeCSharpString(vertex.Label)}""{properties})");
        }

        return string.Join(",\n", vertexCode);
    }

    /// <summary>
    /// Generate C# code for edges
    /// </summary>
    private static string GenerateEdgesCode(List<ExportedEdge> edges)
    {
        var edgeCode = new List<string>();

        foreach (var edge in edges)
        {
            if (edge.Properties.Count > 0)
            {
                var propPairs = edge.Properties.Select(prop => 
                    $@"                (""{EscapeCSharpString(prop.Key)}"", {FormatPropertyValue(prop.Value)})");
                var properties = $@", Props(
{string.Join(",\n", propPairs)}
                )";

                edgeCode.Add($@"                new ScenarioEdgeDefinition(""{EscapeCSharpString(edge.Id)}"",""{EscapeCSharpString(edge.Label)}"", ""{EscapeCSharpString(edge.OutVertexId)}"", ""{EscapeCSharpString(edge.InVertexId)}""{properties})");
            }
            else
            {
                edgeCode.Add($@"                new ScenarioEdgeDefinition(""{EscapeCSharpString(edge.Id)}"",""{EscapeCSharpString(edge.Label)}"", ""{EscapeCSharpString(edge.OutVertexId)}"", ""{EscapeCSharpString(edge.InVertexId)}"")");
            }
        }

        return string.Join(",\n", edgeCode);
    }

    /// <summary>
    /// Format a property value for C# code generation
    /// </summary>
    private static string FormatPropertyValue(object value)
    {
        return value switch
        {
            null => "null",
            string s => $@"""{EscapeCSharpString(s)}""",
            bool b => b.ToString().ToLower(),
            int i => i.ToString(),
            long l => l.ToString() + "L",
            float f => f.ToString("F") + "f",
            double d => d.ToString("F") + "d",
            DateTime dt => $"new DateTime({dt.Ticks}L)",
            _ => $@"""{EscapeCSharpString(value.ToString() ?? "")}"""
        };
    }

    /// <summary>
    /// Escape string for C# string literals
    /// </summary>
    private static string EscapeCSharpString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\r", "\\r")
                   .Replace("\n", "\\n")
                   .Replace("\t", "\\t");
    }

    /// <summary>
    /// Escape string for XML comments
    /// </summary>
    private static string EscapeXmlComment(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;");
    }

    /// <summary>
    /// Convert string to valid C# class name
    /// </summary>
    private static string ToValidClassName(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "Generated";

        // Remove invalid characters and capitalize first letter
        var result = new System.Text.StringBuilder();
        bool capitalizeNext = true;

        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (capitalizeNext)
                {
                    result.Append(char.ToUpper(c));
                    capitalizeNext = false;
                }
                else
                {
                    result.Append(c);
                }
            }
            else if (char.IsWhiteSpace(c) || c == '_' || c == '-')
            {
                capitalizeNext = true;
            }
        }

        var className = result.ToString();
        
        // Ensure it starts with a letter
        if (className.Length == 0 || !char.IsLetter(className[0]))
            className = "Generated" + className;

        return className;
    }
}

/// <summary>
/// Dynamic scenario provider that creates scenarios from exported data
/// </summary>
public class DynamicInMemoryScenarioProvider : InMemoryScenarioProviderBase
{
    private readonly ExportedScenario _exportedScenario;

    public DynamicInMemoryScenarioProvider(ExportedScenario exportedScenario)
    {
        _exportedScenario = exportedScenario ?? throw new ArgumentNullException(nameof(exportedScenario));
    }

    public override string ScenarioName => _exportedScenario.Name;
    public override string Description => _exportedScenario.Description;

    protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        // Convert vertices
        var vertices = _exportedScenario.Vertices.Select(v => 
            new ScenarioVertexDefinition(
                v.Id, 
                v.Label, 
                v.Properties.Select(p => new KeyValuePair<string, object>(p.Key, p.Value)).ToArray()
            )).ToArray();

        // Convert edges
        var edges = _exportedScenario.Edges.Select(e =>
        {
            if (e.Properties.Count > 0)
            {
                return new ScenarioEdgeDefinition(
                    e.Label,
                    e.OutVertexId,
                    e.InVertexId,
                    e.Id,
                    e.Properties.Select(p => new KeyValuePair<string, object>(p.Key, p.Value)).ToArray()
                );
            }
            else
            {
                return new ScenarioEdgeDefinition(e.Label, e.OutVertexId, e.InVertexId);
            }
        }).ToArray();

        return (vertices, edges);
    }
}