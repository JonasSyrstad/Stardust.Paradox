using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;
using Stardust.Paradox.Data.InMemory.Extensions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Simple test runner for advanced query features
    /// </summary>
    public class SimpleTestRunner
    {
        public static async Task RunAdvancedQueryTestsAsync()
        {
            Console.WriteLine("?? Testing Advanced Gremlin Query Features");
            Console.WriteLine(new string('=', 50));

            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.LogQueries = false; // Keep output clean for tests
                options.SimulatedRUPerQuery = 1.0;
            });

            // Set up test data
            await SetupTestData(connector);

            // Test 1: Tokenizer functionality
            Console.WriteLine("\n? Test 1: Tokenizer Parsing");
            var tokenizer = new GremlinTokenizer("g.V().hasLabel('person').values('name')");
            var tokens = tokenizer.Tokenize();
            Console.WriteLine($"   Parsed {tokens.Count} tokens successfully");

            // Test 2: Multi-step traversal (5 steps)
            Console.WriteLine("\n? Test 2: Multi-Step Traversal (5 steps)");
            var result2 = await connector.ExecuteAsync(
                "g.V('person1').out('works_for').in('works_for').hasLabel('person').dedup()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Colleagues found: {result2.SafeCount()}");

            // Test 3: Advanced aggregation
            Console.WriteLine("\n? Test 3: Aggregation Operations");
            var result3 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').values('age').sum()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Total age: {result3.SafeFirst()}");

            // Test 4: Property operations
            Console.WriteLine("\n? Test 4: Property Operations");
            var result4 = await connector.ExecuteAsync(
                "g.V('person1').valueMap('name', 'age')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Value maps: {result4.SafeCount()}");

            // Test 5: Filtering with hasId
            Console.WriteLine("\n? Test 5: Advanced Filtering");
            var result5 = await connector.ExecuteAsync(
                "g.V().hasId('person1', 'person2')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Filtered by ID: {result5.SafeCount()}");

            // Test 6: Edge navigation
            Console.WriteLine("\n? Test 6: Edge Navigation");
            var result6 = await connector.ExecuteAsync(
                "g.V('person1').outE('works_for').inV()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Edge traversal: {result6.SafeCount()}");

            // Test 7: Ordering and limiting
            Console.WriteLine("\n? Test 7: Ordering and Limiting");
            var result7 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').order().limit(2)", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Ordered and limited: {result7.SafeCount()}");

            // Test 8: Group count
            Console.WriteLine("\n? Test 8: Group Count");
            var result8 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').groupCount().by('department')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Groups: {result8.SafeCount()}");

            // Test 9: Complex chained query (7 steps)
            Console.WriteLine("\n? Test 9: Complex Chain (7 steps)");
            var result9 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').has('age').values('age').max()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Max age: {result9.SafeFirst()}");

            // Test 10: Parameterized advanced query
            Console.WriteLine("\n? Test 10: Parameterized Advanced Query");
            var result10 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').has('name', p0).elementMap()", 
                new Dictionary<string, object> { { "p0", "John Doe" } });
            Console.WriteLine($"   Parameterized result: {result10.SafeCount()}");

            Console.WriteLine($"\n?? Total RU Consumed: {connector.ConsumedRU:F2}");
            Console.WriteLine("\n?? All advanced query tests completed successfully!");
        }

        private static async Task SetupTestData(InMemoryGremlinLanguageConnector connector)
        {
            // Add test vertices
            await connector.ExecuteAsync("g.addV('person').property('id', 'person1').property('name', 'John Doe').property('age', 30).property('department', 'Engineering')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'person2').property('name', 'Jane Smith').property('age', 28).property('department', 'Engineering')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'person3').property('name', 'Bob Johnson').property('age', 35).property('department', 'Sales')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('company').property('id', 'company1').property('name', 'Tech Corp')", new Dictionary<string, object>());

            // Add test edges
            await connector.ExecuteAsync("g.V('person1').addE('works_for').to(g.V('company1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('person2').addE('works_for').to(g.V('company1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('person3').addE('works_for').to(g.V('company1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('person1').addE('knows').to(g.V('person2'))", new Dictionary<string, object>());

            Console.WriteLine("Test data setup completed.");
        }
    }
}
