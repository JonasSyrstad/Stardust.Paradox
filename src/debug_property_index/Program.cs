using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var connector = new InMemoryGremlinLanguageConnector();
        var db = connector.Database;

        // Add vertices
        db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
        db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

        Console.WriteLine("=== Before addE ===");
        var edgesBefore = db.GetAllEdges();
        Console.WriteLine($"Total edges: {edgesBefore.Count()}");

        // Execute the query
        var result = await connector.ExecuteAsync(
            "g.V('alice').addE('knows').to(V('bob')).property('since', 2020).property('weight', 0.8)",
            null);

        Console.WriteLine("\n=== After addE ===");
        var edgesAfter = db.GetAllEdges();
        Console.WriteLine($"Total edges: {edgesAfter.Count()}");

        foreach (var edge in edgesAfter)
        {
            Console.WriteLine($"Edge ID: {edge.Id}, Label: {edge.Label}");
            Console.WriteLine($"Properties:");
            foreach (var prop in edge.Properties)
            {
                Console.WriteLine($"  {prop.Key} = {prop.Value} (Type: {prop.Value?.GetType()})");
            }
        }

        // Check if property index has the edge
        Console.WriteLine("\n=== Property Index Lookup ===");
        var edgesByProperty = db.GetEdgesByProperty("since", 2020);
        Console.WriteLine($"Edges with 'since' = 2020: {edgesByProperty.Count()}");

        // Also check with different types
        var edgesByProperty2 = db.GetEdgesByProperty("since", (long)2020);
        Console.WriteLine($"Edges with 'since' = 2020L: {edgesByProperty2.Count()}");

        // Check result
        Console.WriteLine("\n=== Query Result ===");
        Console.WriteLine($"Result count: {result.Count()}");
        foreach (var item in result)
        {
            Console.WriteLine($"Result: {item}");
        }
    }
}
