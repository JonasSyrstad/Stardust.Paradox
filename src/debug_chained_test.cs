using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class DebugChainedTest
{
    public static async Task Main()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Setup the test data
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('department', 'Engineering').property('salary', 75000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('department', 'Engineering').property('salary', 90000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob').property('department', 'Sales').property('salary', 65000)", new Dictionary<string, object>());

        Console.WriteLine("=== Debug Chained Aggregations Test ===");
        
        // Test step by step
        Console.WriteLine("\n1. All persons:");
        var all = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
        Console.WriteLine($"Count: {all.Count()}");
        
        Console.WriteLine("\n2. Engineering persons:");
        var engineering = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', 'Engineering')", new Dictionary<string, object>());
        Console.WriteLine($"Count: {engineering.Count()}");
        foreach (var person in engineering)
        {
            Console.WriteLine($"  Name: {person.properties.name}, Salary: {person.properties.salary}");
        }
        
        Console.WriteLine("\n3. Engineering persons with gt() predicate (this should fail):");
        try 
        {
            var highEarners = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', 'Engineering').has('salary', gt(80000))", new Dictionary<string, object>());
            Console.WriteLine($"Count: {highEarners.Count()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        
        Console.WriteLine("\n4. Engineering persons with direct value comparison:");
        var highEarnersSimple = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', 'Engineering').has('salary', 90000)", new Dictionary<string, object>());
        Console.WriteLine($"Count: {highEarnersSimple.Count()}");
        
        Console.WriteLine("\n5. Final test count:");
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', 'Engineering').has('salary', gt(80000)).count()", new Dictionary<string, object>());
        Console.WriteLine($"Result: {result.First()}");
        Console.WriteLine($"Type: {result.First().GetType()}");
    }
}