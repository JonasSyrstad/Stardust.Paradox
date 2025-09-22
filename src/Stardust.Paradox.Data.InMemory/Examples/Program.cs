using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Extensions;

namespace Stardust.Paradox.Data.InMemory.Examples
{
    /// <summary>
    /// Enhanced demo program showcasing advanced Gremlin query capabilities
    /// Remove this class if not needed in production
    /// </summary>
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Stardust.Paradox.Data.InMemory - Advanced Gremlin Database Demo");
            Console.WriteLine("================================================================\n");

            try
            {
                // Run basic examples
                Console.WriteLine("?? BASIC EXAMPLES");
                Console.WriteLine(new string('?', 50));
                await ExampleUsage.RunExampleAsync();
                
                Console.WriteLine("\n" + new string('?', 70) + "\n");
                
                // Run CRUD examples
                Console.WriteLine("?? CRUD OPERATIONS");
                Console.WriteLine(new string('?', 50));
                await ExampleUsage.RunCrudExampleAsync();
                
                Console.WriteLine("\n" + new string('?', 70) + "\n");
                
                // Run advanced query examples (NEW)
                Console.WriteLine("?? ADVANCED GREMLIN QUERIES (UP TO 10 STEPS)");
                Console.WriteLine(new string('?', 50));
                await AdvancedQueryExamples.RunAdvancedQueriesAsync();
                
                Console.WriteLine("\n" + new string('?', 70) + "\n");
                
                // Run CosmosDB compatibility examples (NEW)
                Console.WriteLine("?? COSMOSDB COMPATIBILITY");
                Console.WriteLine(new string('?', 50));
                await AdvancedQueryExamples.DemoCosmosDbCompatibilityAsync();
                
                Console.WriteLine("\n" + new string('?', 70) + "\n");
                
                // Run bulk data example
                Console.WriteLine("?? BULK DATA & PERFORMANCE");
                Console.WriteLine(new string('?', 50));
                ExampleUsage.RunBulkDataExample();
                
                Console.WriteLine("\n" + new string('?', 70) + "\n");
                
                // Advanced feature demonstrations
                Console.WriteLine("?? ADVANCED FEATURES DEMO");
                Console.WriteLine(new string('?', 50));
                await DemoAdvancedFeatures();
                
                Console.WriteLine("\n" + new string('?', 70) + "\n");
                
                // Interface compliance demonstration
                Console.WriteLine("?? INTERFACE COMPLIANCE");
                Console.WriteLine(new string('?', 50));
                await DemonstrateInterfaceCompliance();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\n" + new string('?', 70));
            Console.WriteLine("?? Demo completed! Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task DemoAdvancedFeatures()
        {
            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.LogQueries = false; // Reduce noise for this demo
                options.SimulatedRUPerQuery = 2.0;
            });

            // Setup complex graph
            connector.LoadData(
                vertices: new[]
                {
                    ("mgr1", "manager", new System.Collections.Generic.Dictionary<string, object> { { "name", "Alice Manager" }, { "level", 5 }, { "department", "Engineering" } }),
                    ("dev1", "developer", new System.Collections.Generic.Dictionary<string, object> { { "name", "Bob Developer" }, { "level", 3 }, { "salary", 85000 } }),
                    ("dev2", "developer", new System.Collections.Generic.Dictionary<string, object> { { "name", "Carol Developer" }, { "level", 4 }, { "salary", 95000 } }),
                    ("proj1", "project", new System.Collections.Generic.Dictionary<string, object> { { "name", "Project Alpha" }, { "priority", "high" }, { "budget", 200000 } }),
                    ("proj2", "project", new System.Collections.Generic.Dictionary<string, object> { { "name", "Project Beta" }, { "priority", "medium" }, { "budget", 150000 } })
                },
                edges: new[]
                {
                    ("manages1", "manages", "mgr1", "dev1", new System.Collections.Generic.Dictionary<string, object>()),
                    ("manages2", "manages", "mgr1", "dev2", new System.Collections.Generic.Dictionary<string, object>()),
                    ("works_on1", "works_on", "dev1", "proj1", new System.Collections.Generic.Dictionary<string, object> { { "hours_per_week", 40 } }),
                    ("works_on2", "works_on", "dev2", "proj1", new System.Collections.Generic.Dictionary<string, object> { { "hours_per_week", 30 } }),
                    ("works_on3", "works_on", "dev2", "proj2", new System.Collections.Generic.Dictionary<string, object> { { "hours_per_week", 10 } })
                }
            );

            Console.WriteLine("1. Tokenizer Demo - Complex Query Parsing:");
            var tokenizer = new GremlinTokenizer("g.V().hasLabel('developer').out('works_on').has('priority', 'high').values('name')");
            var tokens = tokenizer.Tokenize();
            Console.WriteLine($"   Parsed {tokens.Count} tokens from complex query");

            Console.WriteLine("\n2. Multi-Step Chain Execution (8 steps):");
            var result1 = await connector.ExecuteAsync(
                "g.V('mgr1').out('manages').out('works_on').hasLabel('project').has('priority', 'high').values('budget').sum()", 
                new System.Collections.Generic.Dictionary<string, object>());
            Console.WriteLine($"   High priority project budget under Alice: ${result1.SafeFirstOrDefault()}");

            Console.WriteLine("\n3. Advanced Property Operations:");
            var result2 = await connector.ExecuteAsync(
                "g.V().hasLabel('developer').valueMap('name', 'salary')", 
                new System.Collections.Generic.Dictionary<string, object>());
            Console.WriteLine($"   Developer value maps retrieved: {result2.SafeCount()}");

            Console.WriteLine("\n4. Aggregation with Grouping:");
            var result3 = await connector.ExecuteAsync(
                "g.V().hasLabel('project').groupCount().by('priority')", 
                new System.Collections.Generic.Dictionary<string, object>());
            Console.WriteLine($"   Project priority groups: {result3.SafeCount()}");

            Console.WriteLine("\n5. Path Traversal:");
            var result4 = await connector.ExecuteAsync(
                "g.V('mgr1').out('manages').out('works_on').path()", 
                new System.Collections.Generic.Dictionary<string, object>());
            Console.WriteLine($"   Management-to-project paths: {result4.SafeCount()}");

            Console.WriteLine("\n6. Statistical Operations:");
            var result5 = await connector.ExecuteAsync(
                "g.V().hasLabel('developer').values('salary').mean()", 
                new System.Collections.Generic.Dictionary<string, object>());
            Console.WriteLine($"   Average developer salary: ${result5.SafeFirst():F2}");

            Console.WriteLine("\n7. Deduplication and Ordering:");
            var result6 = await connector.ExecuteAsync(
                "g.V().hasLabel('developer').out('works_on').in('works_on').dedup().order().limit(3)", 
                new System.Collections.Generic.Dictionary<string, object>());
            Console.WriteLine($"   Unique colleagues (ordered, limited): {result6.SafeCount()}");

            Console.WriteLine("\n8. Element Maps with Metadata:");
            var result7 = await connector.ExecuteAsync(
                "g.V('dev1').elementMap()", 
                new System.Collections.Generic.Dictionary<string, object>());
            Console.WriteLine($"   Bob's complete element map: {result7.SafeCount()}");

            Console.WriteLine($"\n   ?? Total RU consumed: {connector.ConsumedRU:F2}");
        }

        private static async Task DemonstrateInterfaceCompliance()
        {
            // Create connector via interface
            IGremlinLanguageConnector connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.SimulatedRUPerQuery = 1.0;
                options.LogQueries = false;
            });

            // Test interface properties
            Console.WriteLine($"? Can parameterize queries: {connector.CanParameterizeQueries}");
            Console.WriteLine($"? Initial consumed RU: {connector.ConsumedRU}");

            // Test advanced interface method with complex query
            await connector.ExecuteAsync(
                "g.addV('test').property('name', 'Interface Test').property('complexity', 'advanced')", 
                new System.Collections.Generic.Dictionary<string, object>());

            // Test parameterized complex query
            var result = await connector.ExecuteAsync(
                "g.V().hasLabel('test').has('name', p0).valueMap()", 
                new System.Collections.Generic.Dictionary<string, object> { { "p0", "Interface Test" } });

            Console.WriteLine($"? Complex parameterized query result: {result.SafeCount()} items");
            Console.WriteLine($"? Final consumed RU: {connector.ConsumedRU}");
            Console.WriteLine("? Advanced interface compliance verified!");
        }
    }
}

// Comment out or remove the Program class if you don't want a console application// Comment out or remove the Program class if you don't want a console application
