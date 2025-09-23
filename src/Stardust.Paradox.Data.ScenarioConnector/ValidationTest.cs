using Stardust.Paradox.Data.ScenarioConnector;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Simple validation test for the scenario connector components
/// </summary>
public class ValidationTest
{
    public static async Task RunValidationAsync()
    {
        Console.WriteLine("=== Validation Test ===");
        
        // Test 1: Connection Manager
        Console.WriteLine("Testing Connection Manager...");
        var connectionManager = new ConnectionManager();
        
        var testConnection = new CosmosDbConnection
        {
            Name = "TestConnection",
            Hostname = "test.gremlin.cosmosdb.azure.com",
            DatabaseName = "testdb",
            GraphName = "testgraph",
            AccessKey = "test-key-12345"
        };
        
        try
        {
            connectionManager.AddConnection(testConnection);
            var connections = connectionManager.GetConnections();
            Console.WriteLine($"? Connection Manager: Added and retrieved {connections.Count} connection(s)");
            
            connectionManager.RemoveConnection("TestConnection");
            Console.WriteLine("? Connection Manager: Successfully removed test connection");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Connection Manager failed: {ex.Message}");
        }
        
        // Test 2: Scenario Converter
        Console.WriteLine("\nTesting Scenario Converter...");
        try
        {
            var scenario = await ScenarioExporter.LoadScenarioAsync("Examples/SampleUserNetwork.json");
            if (scenario != null)
            {
                var provider = ScenarioConverter.ConvertToInMemoryScenario(scenario);
                Console.WriteLine($"? Scenario Converter: Successfully converted '{scenario.Name}'");
                Console.WriteLine($"  - Vertices: {scenario.Vertices.Count}");
                Console.WriteLine($"  - Edges: {scenario.Edges.Count}");
                Console.WriteLine($"  - Provider Name: {provider.ScenarioName}");
                Console.WriteLine($"  - Provider Description: {provider.Description}");
            }
            else
            {
                Console.WriteLine("? Scenario Converter: Failed to load sample scenario");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Scenario Converter failed: {ex.Message}");
        }
        
        // Test 3: File Operations
        Console.WriteLine("\nTesting File Operations...");
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "StardustParadoxTest");
            Directory.CreateDirectory(tempDir);
            
            var testScenario = new ExportedScenario
            {
                Name = "ValidationTest",
                Description = "Test scenario for validation",
                Vertices = new List<ExportedVertex>
                {
                    new ExportedVertex { Id = "v1", Label = "test", Properties = new Dictionary<string, object> { ["name"] = "Test Vertex" } }
                },
                Edges = new List<ExportedEdge>()
            };
            
            var jsonPath = Path.Combine(tempDir, "validation.json");
            var csPath = Path.Combine(tempDir, "validation.cs");
            
            await ScenarioExporter.SaveScenarioAsync(testScenario, jsonPath);
            await ScenarioConverter.SaveAsCSharpClassAsync(testScenario, csPath);
            
            if (File.Exists(jsonPath) && File.Exists(csPath))
            {
                Console.WriteLine("? File Operations: Successfully created JSON and C# files");
                
                // Cleanup
                File.Delete(jsonPath);
                File.Delete(csPath);
                Directory.Delete(tempDir);
            }
            else
            {
                Console.WriteLine("? File Operations: Failed to create files");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? File Operations failed: {ex.Message}");
        }
        
        Console.WriteLine("\n=== Validation Complete ===");
    }
}