using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;

class DebugTreeStepTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Tree Step Debug Test ===");
        
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        Console.WriteLine("1. Testing empty traversal: V('nonexistent').out('child').tree()");
        var result1 = await connector.ExecuteAsync("g.V('nonexistent').out('child').tree()", new Dictionary<string, object>());
        Console.WriteLine($"Result count: {result1?.Count() ?? 0}");
        foreach (var item in result1 ?? new List<dynamic>())
        {
            Console.WriteLine($"Result item: {item}");
        }
        
        Console.WriteLine("\n2. Testing tree() on empty vertex set: V('nonexistent').tree()");
        var result2 = await connector.ExecuteAsync("g.V('nonexistent').tree()", new Dictionary<string, object>());
        Console.WriteLine($"Result count: {result2?.Count() ?? 0}");
        foreach (var item in result2 ?? new List<dynamic>())
        {
            Console.WriteLine($"Result item: {item}");
        }
        
        Console.WriteLine("\n3. Testing tree() with no vertices:");
        // Clear database to make sure it's empty
        await connector.ExecuteAsync("g.V().drop()", new Dictionary<string, object>());
        var result3 = await connector.ExecuteAsync("g.V().tree()", new Dictionary<string, object>());
        Console.WriteLine($"Result count: {result3?.Count() ?? 0}");
        foreach (var item in result3 ?? new List<dynamic>())
        {
            Console.WriteLine($"Result item: {item}");
        }
        
        Console.WriteLine("\n4. Create vertex and test:");
        await connector.ExecuteAsync("g.addV('person').property('id', 'test')", new Dictionary<string, object>());
        var result4 = await connector.ExecuteAsync("g.V('test').tree()", new Dictionary<string, object>());
        Console.WriteLine($"Result count: {result4?.Count() ?? 0}");
        foreach (var item in result4 ?? new List<dynamic>())
        {
            Console.WriteLine($"Result item: {item}");
        }
    }
}