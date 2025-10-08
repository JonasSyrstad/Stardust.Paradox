using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    static async Task Main()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Create a tree structure: Root -> Child1, Child2 -> Grandchild1, Grandchild2
        await connector.ExecuteAsync("g.addV('person').property('id', 'root').property('name', 'Root')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'child1').property('name', 'Child1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'child2').property('name', 'Child2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'grandchild1').property('name', 'Grandchild1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'grandchild2').property('name', 'Grandchild2')", new Dictionary<string, object>());

        // Create edges
        await connector.ExecuteAsync("g.V('root').addE('parent').to(g.V('child1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('root').addE('parent').to(g.V('child2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('child1').addE('parent').to(g.V('grandchild1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('child1').addE('parent').to(g.V('grandchild2'))", new Dictionary<string, object>());

        Console.WriteLine("Testing g.V('root').repeat(__.out('parent')).until(__.outE('parent').count().is(0)).tree()");
        var result = await connector.ExecuteAsync("g.V('root').repeat(__.out('parent')).until(__.outE('parent').count().is(0)).tree()", new Dictionary<string, object>());
        
        Console.WriteLine($"Result count: {result.Count()}");
        foreach (var item in result)
        {
            Console.WriteLine($"Item type: {item?.GetType().Name}");
            Console.WriteLine($"Item JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
        }
    }
}
