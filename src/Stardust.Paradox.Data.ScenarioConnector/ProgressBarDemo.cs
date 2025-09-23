using Stardust.Paradox.Data.ScenarioConnector;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Simple demonstration of progress bar functionality
/// </summary>
public static class ProgressBarDemo
{
    public static async Task RunProgressBarDemoAsync()
    {
        Console.WriteLine("=== Progress Bar Demo ===");
        Console.WriteLine("Demonstrating various progress bar styles used in scenario export");
        Console.WriteLine();

        // Demo 1: Vertex processing simulation
        Console.WriteLine("1. Simulating vertex processing...");
        await SimulateVertexProcessingAsync();

        Console.WriteLine("\n2. Simulating edge discovery...");
        await SimulateEdgeDiscoveryAsync();

        Console.WriteLine("\n3. Simulating property enhancement...");
        await SimulatePropertyEnhancementAsync();

        Console.WriteLine("\n4. Simulating file operations...");
        await SimulateFileOperationsAsync();

        Console.WriteLine("\n=== Progress Bar Demo Complete ===");
    }

    private static async Task SimulateVertexProcessingAsync()
    {
        var vertexIds = new[] { "user1", "user2", "user3", "product1", "product2", "company1", "location1" };
        
        using (var progress = ProgressBarFactory.CreateVertexProgress(vertexIds.Length))
        {
            foreach (var vertexId in vertexIds)
            {
                // Simulate some processing time
                await Task.Delay(300);
                progress.Increment(vertexId);
            }
        }
    }

    private static async Task SimulateEdgeDiscoveryAsync()
    {
        var vertices = new[] { "v1", "v2", "v3", "v4", "v5", "v6", "v7", "v8", "v9", "v10" };
        
        using (var progress = ProgressBarFactory.Create(vertices.Length, "Discovering Edges"))
        {
            foreach (var vertex in vertices)
            {
                // Simulate edge discovery for each vertex
                await Task.Delay(200);
                progress.Increment($"Processing {vertex}");
            }
        }
    }

    private static async Task SimulatePropertyEnhancementAsync()
    {
        var items = new[] { "person:john", "person:jane", "company:acme", "location:nyc", "product:laptop" };
        
        using (var progress = ProgressBarFactory.CreatePropertyProgress(items.Length))
        {
            foreach (var item in items)
            {
                // Simulate property fetching
                await Task.Delay(250);
                progress.Increment(item);
            }
        }
    }

    private static async Task SimulateFileOperationsAsync()
    {
        var files = new[] { "scenario.json", "scenario.cs" };
        
        using (var progress = ProgressBarFactory.CreateFileProgress(files.Length))
        {
            foreach (var file in files)
            {
                // Simulate file saving
                await Task.Delay(500);
                progress.Increment($"Saving {file}");
            }
        }
    }
}