using System;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using System.Collections.Generic;

namespace GremlinNetComprehensiveTestApp
{
    class BasicQueryValidationTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? BASIC QUERY VALIDATION TEST");
            Console.WriteLine("Testing fundamental Gremlin queries like g.V()");
            Console.WriteLine("=" + new string('=', 50));
            
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = 8765,
                HttpPort = 8766,
                EnableTcp = true,
                EnableWebSocket = true,
                EnableHttp = true,
                EnableLogging = false,
                EnableDebugLogging = false
            };

            var server = new GremlinServer(options);
            await server.StartAsync();
            await Task.Delay(1000);

            var basicQueries = new List<(string query, string description, bool shouldHaveResults)>
            {
                ("g.V()", "Get all vertices", false), // Initially empty
                ("g.E()", "Get all edges", false), // Initially empty
                ("g.addV('person')", "Add a vertex", true),
                ("g.V()", "Get all vertices after adding one", true),
                ("g.V().hasLabel('person')", "Filter vertices by label", true),
                ("g.V().count()", "Count vertices", true),
                ("g.inject(1, 2, 3)", "Inject multiple values", true),
                ("g.inject(42).identity()", "Inject with identity", true),
                ("g.addV('test').property('name', 'value')", "Add vertex with property", true),
                ("g.V().values('name')", "Get property values", true),
                ("g.V().valueMap()", "Get property maps", true)
            };

            var passedQueries = 0;
            foreach (var test in basicQueries)
            {
                Console.WriteLine($"\nTesting: {test.description}");
                Console.WriteLine($"Query: {test.query}");
                
                try
                {
                    var result = await server.Connector.ExecuteAsync(test.query, new Dictionary<string, object>());
                    var hasResults = result != null && result.Any();
                    
                    Console.WriteLine($"Result: {(result == null ? "null" : $"Count={result.Count()}")}");
                    
                    if (test.shouldHaveResults == hasResults)
                    {
                        passedQueries++;
                        Console.WriteLine("? PASSED");
                    }
                    else
                    {
                        Console.WriteLine($"? FAILED - Expected {(test.shouldHaveResults ? "results" : "no results")} but got {(hasResults ? "results" : "no results")}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? FAILED with exception: {ex.Message}");
                }
            }

            var successRate = (double)passedQueries / basicQueries.Count;
            Console.WriteLine($"\n=== FINAL RESULTS ===");
            Console.WriteLine($"Basic query tests: {passedQueries}/{basicQueries.Count} passed ({successRate:P0})");
            
            if (successRate >= 0.9)
            {
                Console.WriteLine("?? EXCELLENT! Basic queries work perfectly!");
            }
            else if (successRate >= 0.75)
            {
                Console.WriteLine("? GOOD! Most basic queries work correctly.");
            }
            else
            {
                Console.WriteLine("?? NEEDS IMPROVEMENT! Some basic queries failing.");
            }

            await server.StopAsync();
            server.Dispose();
        }
    }
}