using Stardust.Paradox.Data.InMemory;

var connector = InMemoryGremlinLanguageConnector.Create();

// Create isolated vertex
await connector.ExecuteAsync(
    "g.addV('isolated').property('id', 'alone').property('pk', 'p1').property('name', 'target')", 
    new Dictionary<string, object>());

// Test 1: Just V
var test1 = await connector.ExecuteAsync(
    "g.V([__p0,__p1])", 
    new Dictionary<string, object> { { "__p0", "p1" }, { "__p1", "alone" } });
Console.WriteLine($"Test 1 (V only): {test1.Count()} results");
foreach (var r in test1)
{
    Console.WriteLine($"  - id={r.id}, name={r.properties.name}");
}

// Test 2: V + emit + repeat + until
var test2 = await connector.ExecuteAsync(
    "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7))", 
    new Dictionary<string, object> 
    { 
        { "__p0", "p1" },
        { "__p1", "alone" },
        { "__p7", 5 }
    });
Console.WriteLine($"\nTest 2 (V + emit + repeat + until): {test2.Count()} results");
foreach (var r in test2)
{
    Console.WriteLine($"  - id={r.id}, name={r.properties.name}");
}

// Test 3: Full query
var test3 = await connector.ExecuteAsync(
    "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)", 
    new Dictionary<string, object> 
    { 
        { "__p0", "p1" },
        { "__p1", "alone" },
        { "__p2", "name" },
        { "__p3", "target" },
        { "__p4", "alone" },
        { "__p7", 5 }
    });
Console.WriteLine($"\nTest 3 (Full query): {test3.Count()} results");
foreach (var r in test3)
{
    Console.WriteLine($"  - id={r.id}, name={r.properties.name}");
}
