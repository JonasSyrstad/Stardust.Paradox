using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Testing Tree Step Implementation");
        
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create simple test data
        await connector.ExecuteAsync("g.addV('person').property('id', 'A').property('name', 'A')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'B').property('name', 'B')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'C').property('name', 'C')", new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('A').addE('child').to(g.V('B'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('B').addE('child').to(g.V('C'))", new Dictionary<string, object>());
        
        Console.WriteLine("Created test data");
        
        // Test simple tree query
        var result = await connector.ExecuteAsync("g.V('A').out('child').tree()", new Dictionary<string, object>());
        
        Console.WriteLine($"Tree result count: {result?.Count()}");
        foreach (var item in result ?? new List<dynamic>())
        {
            Console.WriteLine($"Tree result type: {item?.GetType()?.FullName}");
            Console.WriteLine($"Tree result: {item}");
        }
        
        // Test with all vertices
        var allVertices = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
        Console.WriteLine($"All vertices count: {allVertices?.Count()}");
        
        // Test paths
        var paths = await connector.ExecuteAsync("g.V('A').out('child').path()", new Dictionary<string, object>());
        Console.WriteLine($"Paths count: {paths?.Count()}");
        foreach (var path in paths ?? new List<dynamic>())
        {
            Console.WriteLine($"Path: {path}");
        }
    }
}