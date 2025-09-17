using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests;

public class GroupDebugTest
{
    [Fact]
    public async Task Debug_GroupWithProjection_StepByStep()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Setup test data
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob').property('department', 'Sales')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice').property('department', 'Marketing')", new Dictionary<string, object>());

        // Test query parsing step by step
        System.Console.WriteLine("=== Testing Query Parsing ===");
        
        // Test 1: Let's see what we get from a simple group() without .by()
        var simpleGroupResult = await connector.ExecuteAsync("g.V().hasLabel('person').group()", new Dictionary<string, object>());
        System.Console.WriteLine($"Simple group() result count: {simpleGroupResult.Count()}");
        
        if (simpleGroupResult.Any())
        {
            var simpleGroups = simpleGroupResult.First() as Dictionary<string, List<dynamic>>;
            if (simpleGroups != null)
            {
                System.Console.WriteLine($"Simple group keys: [{string.Join(", ", simpleGroups.Keys)}]");
            }
        }

        // Test 2: Now test the problematic query
        System.Console.WriteLine("\n=== Testing Problematic Query ===");
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').group().by('department')", new Dictionary<string, object>());
        
        System.Console.WriteLine($"Group.by('department') result count: {result.Count()}");
        
        if (result.Any())
        {
            System.Console.WriteLine($"Result type: {result.First().GetType()}");
            var groups = result.First() as Dictionary<string, List<dynamic>>;
            if (groups != null)
            {
                System.Console.WriteLine($"Group keys: [{string.Join(", ", groups.Keys)}]");
                foreach (var group in groups)
                {
                    System.Console.WriteLine($"  {group.Key}: {group.Value.Count} items");
                }
            }
            else
            {
                System.Console.WriteLine($"Failed to cast to Dictionary<string, List<dynamic>>");
                System.Console.WriteLine($"Actual result: {result.First()}");
            }
        }
    }
}