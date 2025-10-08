using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;

class Program
{
    static async Task Main()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Setup
        await connector.ExecuteAsync("g.addV('org').property('id', 'org1').property('name', 'HQ').property('type', 'headquarters')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('org').property('id', 'org2').property('name', 'Branch1').property('type', 'branch')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('org').property('id', 'org3').property('name', 'Branch2').property('type', 'branch')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('org1').addE('contains').to(g.V('org2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('org1').addE('contains').to(g.V('org3'))", new Dictionary<string, object>());
        
        Console.WriteLine("=== Setup Complete ===");
        Console.WriteLine();
        
        // Test 1: Just V('org1')
        Console.WriteLine("Test 1: g.V('org1')");
        var r1 = await connector.ExecuteAsync("g.V('org1')", new Dictionary<string, object>());
        Console.WriteLine($"  Results: {r1.Count()}");
        foreach (var item in r1)
        {
            Console.WriteLine($"    - id={item.id}, type={item.properties?.type}");
        }
        Console.WriteLine();
        
        // Test 2: V('org1').out('contains')
        Console.WriteLine("Test 2: g.V('org1').out('contains')");
        var r2 = await connector.ExecuteAsync("g.V('org1').out('contains')", new Dictionary<string, object>());
        Console.WriteLine($"  Results: {r2.Count()}");
        foreach (var item in r2)
        {
            Console.WriteLine($"    - id={item.id}, type={item.properties?.type}");
        }
        Console.WriteLine();
        
        // Test 3: V('org1').emit().repeat(out('contains')).until(loops().is(3))
        Console.WriteLine("Test 3: g.V('org1').emit().repeat(out('contains')).until(loops().is(3))");
        var r3 = await connector.ExecuteAsync("g.V('org1').emit().repeat(out('contains')).until(loops().is(3))", new Dictionary<string, object>());
        Console.WriteLine($"  Results: {r3.Count()}");
        foreach (var item in r3)
        {
            Console.WriteLine($"    - id={item.id}, type={item.properties?.type}");
        }
        Console.WriteLine();
        
        // Test 4: Full query
        Console.WriteLine("Test 4: g.V('org1').emit().repeat(out('contains')).until(loops().is(3)).has('type','branch').hasId('org2')");
        var r4 = await connector.ExecuteAsync("g.V('org1').emit().repeat(out('contains')).until(loops().is(3)).has('type','branch').hasId('org2')", new Dictionary<string, object>());
        Console.WriteLine($"  Results: {r4.Count()}");
        foreach (var item in r4)
        {
            Console.WriteLine($"    - id={item.id}, type={item.properties?.type}");
        }
        Console.WriteLine();
        
        // Test 5: Parameterized version
        Console.WriteLine("Test 5: Parameterized version");
        var r5 = await connector.ExecuteAsync(
            "g.V(__p0).emit().repeat(out(__p1)).until(loops().is(__p2)).has(__p3,__p4).hasId(__p5)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "org1" },
                { "__p1", "contains" },
                { "__p2", 3 },
                { "__p3", "type" },
                { "__p4", "branch" },
                { "__p5", "org2" }
            });
        Console.WriteLine($"  Results: {r5.Count()}");
        foreach (var item in r5)
        {
            Console.WriteLine($"    - id={item.id}, type={item.properties?.type}");
        }
        
        Console.WriteLine();
        Console.WriteLine("=== Done ===");
    }
}
