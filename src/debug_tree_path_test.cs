using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory;

class Program
{
    static async Task Main(string[] args)
    {
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Create a simple tree structure: Root -> Child1
        await connector.ExecuteAsync("g.addV('person').property('id', 'root').property('name', 'Root')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'child1').property('name', 'Child1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('root').addE('parent').to(g.V('child1'))", new Dictionary<string, object>());

        Console.WriteLine("=== Test 1: Simple out().tree() ===");
        var result1 = await connector.ExecuteAsync("g.V('root').out('parent').tree()", new Dictionary<string, object>());
        Console.WriteLine($"Result count: {result1.Count()}");
        foreach (var item in result1)
        {
            Console.WriteLine($"Result type: {item?.GetType().Name}");
            Console.WriteLine($"Result: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
        }

        Console.WriteLine("\n=== Test 2: repeat().until() with tree() ===");
        var result2 = await connector.ExecuteAsync("g.V('root').repeat(__.out('parent')).until(__.outE('parent').count().is(0)).tree()", new Dictionary<string, object>());
        Console.WriteLine($"Result count: {result2.Count()}");
        foreach (var item in result2)
        {
            Console.WriteLine($"Result type: {item?.GetType().Name}");
            Console.WriteLine($"Result: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
        }

        Console.WriteLine("\n=== Test 3: Just repeat().until() without tree() ===");
        var result3 = await connector.ExecuteAsync("g.V('root').repeat(__.out('parent')).until(__.outE('parent').count().is(0))", new Dictionary<string, object>());
        Console.WriteLine($"Result count: {result3.Count()}");
        foreach (var item in result3)
        {
            Console.WriteLine($"Result type: {item?.GetType().Name}");
            Console.WriteLine($"Result: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
        }
    }
}
