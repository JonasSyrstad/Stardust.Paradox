using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create the exact test scenario
        Console.WriteLine("=== Creating test graph ===");
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'v1').property('pk', 'pk1').property('name', 'start')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'v2').property('pk', 'pk1').property('name', 'middle')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'v3').property('pk', 'pk1').property('name', 'found')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('v1').addE('link').to(g.V('v2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('v2').addE('link').to(g.V('v3'))", new Dictionary<string, object>());

        // Test the emit().repeat().until() pattern
        Console.WriteLine("\n=== Test 1: emit().repeat().until() ===");
        var test1 = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7))", 
            new Dictionary<string, object> 
            { 
                { "__p0", "pk1" },
                { "__p1", "v1" },
                { "__p7", 3 }
            });
        
        Console.WriteLine($"Result count: {test1.Count()}");
        foreach (var r in test1)
        {
            try
            {
                Console.WriteLine($"  Result type: {r.GetType().Name}");
                Console.WriteLine($"  - id: {r.id}");
                
                // Try to access properties
                if (r.properties != null)
                {
                    Console.WriteLine($"  - properties type: {r.properties.GetType().Name}");
                    
                    // Try to access name property
                    try
                    {
                        var nameValue = r.properties.name;
                        Console.WriteLine($"  - properties.name: {nameValue}");
                        
                        // If it's a list, check the structure
                        if (nameValue is System.Collections.IEnumerable enumerable && !(nameValue is string))
                        {
                            Console.WriteLine($"  - properties.name is enumerable");
                            var first = enumerable.Cast<object>().FirstOrDefault();
                            if (first != null)
                            {
                                Console.WriteLine($"    - First element type: {first.GetType().Name}");
                                Console.WriteLine($"    - First element: {first}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  - Error accessing properties.name: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"  - properties is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error processing result: {ex.Message}");
            }
        }

        // Test with has() filter
        Console.WriteLine("\n=== Test 2: with has('name', 'found') ===");
        var test2 = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "pk1" },
                { "__p1", "v1" },
                { "__p2", "name" },
                { "__p3", "found" },
                { "__p7", 3 }
            });
        
        Console.WriteLine($"Result count: {test2.Count()}");
        foreach (var r in test2)
        {
            try
            {
                Console.WriteLine($"  - id: {r.id}, name: {r.properties?.name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        // Test simple has() without emit/repeat
        Console.WriteLine("\n=== Test 3: Simple has() for comparison ===");
        var test3 = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "name" },
                { "__p1", "found" }
            });
        
        Console.WriteLine($"Result count: {test3.Count()}");
        foreach (var r in test3)
        {
            try
            {
                Console.WriteLine($"  - id: {r.id}");
                if (r.properties != null)
                {
                    Console.WriteLine($"  - properties type: {r.properties.GetType().Name}");
                    var nameValue = r.properties.name;
                    Console.WriteLine($"  - name type: {nameValue?.GetType().Name}");
                    Console.WriteLine($"  - name value: {nameValue}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }
    }
}
