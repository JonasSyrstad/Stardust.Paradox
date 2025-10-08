using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static async Task Main()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create test data
        await connector.ExecuteAsync("g.addV('person').property('id', 'parent').property('name', 'Parent')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'child1').property('name', 'Child1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'child2').property('name', 'Child2')", new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('parent').addE('parent').to(g.V('child1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('parent').addE('parent').to(g.V('child2'))", new Dictionary<string, object>());
        
        // Execute tree query
        var result = await connector.ExecuteAsync(
            "g.V('parent').repeat(out('parent')).until(outE('parent').count().is(0)).tree()", 
            new Dictionary<string, object>());
        
        Console.WriteLine($"Result count: {result.Count()}");
        Console.WriteLine($"Result type: {result.GetType()}");
        
        foreach (var item in result)
        {
            Console.WriteLine($"\nItem type: {item?.GetType()}");
            Console.WriteLine($"Item value:");
            Console.WriteLine(JsonConvert.SerializeObject(item, Formatting.Indented));
        }
    }
}
