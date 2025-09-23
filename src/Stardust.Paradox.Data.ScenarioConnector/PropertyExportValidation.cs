using Stardust.Paradox.Data.ScenarioConnector;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Quick test to validate ScenarioConverter property handling
/// </summary>
public class PropertyExportValidation
{
    public static async Task TestScenarioConverterAsync()
    {
        Console.WriteLine("=== Testing ScenarioConverter Property Handling ===");

        // Create test scenario with properties
        var testScenario = new ExportedScenario
        {
            Name = "PropertyTest",
            Description = "Test scenario with properties",
            ExportedAt = DateTime.UtcNow,
            SourceConnection = "TestConnection",
            ExportQuery = "g.V().limit(1)",
            Vertices = new List<ExportedVertex>
            {
                new ExportedVertex
                {
                    Id = "vertex1",
                    Label = "person",
                    Properties = new Dictionary<string, object>
                    {
                        ["name"] = "John Doe",
                        ["age"] = 30,
                        ["verified"] = true,
                        ["score"] = 95.5
                    }
                }
            },
            Edges = new List<ExportedEdge>
            {
                new ExportedEdge
                {
                    Id = "edge1",
                    Label = "knows",
                    OutVertexId = "vertex1",
                    InVertexId = "vertex2",
                    Properties = new Dictionary<string, object>
                    {
                        ["since"] = "2023-01-01",
                        ["strength"] = 0.8
                    }
                }
            }
        };

        Console.WriteLine("Created test scenario with:");
        Console.WriteLine($"- Vertex with {testScenario.Vertices[0].Properties.Count} properties");
        Console.WriteLine($"- Edge with {testScenario.Edges[0].Properties.Count} properties");

        // Test JSON serialization
        var tempJsonFile = Path.GetTempFileName() + ".json";
        await ScenarioExporter.SaveScenarioAsync(testScenario, tempJsonFile);
        
        Console.WriteLine($"\nJSON file saved to: {tempJsonFile}");
        var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
        Console.WriteLine("JSON content preview:");
        Console.WriteLine(jsonContent.Substring(0, Math.Min(500, jsonContent.Length)) + "...");

        // Test C# class generation
        var tempCsFile = Path.GetTempFileName() + ".cs";
        await ScenarioConverter.SaveAsCSharpClassAsync(testScenario, tempCsFile);
        
        Console.WriteLine($"\nC# file saved to: {tempCsFile}");
        var csContent = await File.ReadAllTextAsync(tempCsFile);
        Console.WriteLine("C# content preview:");
        Console.WriteLine(csContent.Substring(0, Math.Min(500, csContent.Length)) + "...");

        // Test dynamic provider
        var provider = ScenarioConverter.ConvertToInMemoryScenario(testScenario);
        Console.WriteLine($"\nDynamic provider created:");
        Console.WriteLine($"- Scenario Name: {provider.ScenarioName}");
        Console.WriteLine($"- Description: {provider.Description}");

        // Cleanup
        File.Delete(tempJsonFile);
        File.Delete(tempCsFile);

        Console.WriteLine("\n=== ScenarioConverter Test Complete ===");
        Console.WriteLine("If this shows properties correctly, the issue is in ScenarioExporter.");
    }

    public static async Task TestLoadedScenarioAsync(string jsonFilePath)
    {
        Console.WriteLine($"=== Testing Loaded Scenario: {jsonFilePath} ===");
        
        if (!File.Exists(jsonFilePath))
        {
            Console.WriteLine("File not found!");
            return;
        }

        var scenario = await ScenarioExporter.LoadScenarioAsync(jsonFilePath);
        if (scenario == null)
        {
            Console.WriteLine("Failed to load scenario!");
            return;
        }

        Console.WriteLine($"Loaded scenario: {scenario.Name}");
        Console.WriteLine($"Vertices: {scenario.Vertices.Count}");
        Console.WriteLine($"Edges: {scenario.Edges.Count}");

        foreach (var vertex in scenario.Vertices.Take(3))
        {
            Console.WriteLine($"Vertex {vertex.Id} ({vertex.Label}): {vertex.Properties.Count} properties");
            foreach (var prop in vertex.Properties.Take(3))
            {
                Console.WriteLine($"  {prop.Key}: {prop.Value} ({prop.Value?.GetType().Name})");
            }
        }

        foreach (var edge in scenario.Edges.Take(3))
        {
            Console.WriteLine($"Edge {edge.Id} ({edge.Label}): {edge.Properties.Count} properties");
            foreach (var prop in edge.Properties.Take(3))
            {
                Console.WriteLine($"  {prop.Key}: {prop.Value} ({prop.Value?.GetType().Name})");
            }
        }

        Console.WriteLine("=== Loaded Scenario Test Complete ===");
    }
}