using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory
{
    class DebugProgram
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== DEBUG SESSION ===");
            
            var connector = InMemoryGremlinLanguageConnector.Create(opts =>
            {
                opts.LogQueries = true;
                opts.EnableQueryLogging = true;
            });

            try
            {
                Console.WriteLine("\n1. Creating first vertex (john)...");
                var result1 = await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John').property('age', 30)", new Dictionary<string, object>());
                Console.WriteLine($"Result count: {result1.Count()}");

                Console.WriteLine("\n2. Creating second vertex (jane)...");
                var result2 = await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane').property('age', 28)", new Dictionary<string, object>());
                Console.WriteLine($"Result count: {result2.Count()}");

                Console.WriteLine("\n3. Creating company vertex (tech_corp)...");
                var result3 = await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
                Console.WriteLine($"Result count: {result3.Count()}");

                Console.WriteLine("\n4. Creating work edge (john -> tech_corp)...");
                var result4 = await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
                Console.WriteLine($"Result count: {result4.Count()}");

                Console.WriteLine("\n5. Database state:");
                Console.WriteLine(connector.GetSummary());

                Console.WriteLine("\n6. Testing traversal: g.V('john').out('works_for')");
                var traversalResult = await connector.ExecuteAsync("g.V('john').out('works_for')", new Dictionary<string, object>());
                Console.WriteLine($"Traversal result count: {traversalResult.Count()}");
                
                foreach (var item in traversalResult)
                {
                    Console.WriteLine($"Result item: {item}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\n=== END DEBUG ===");
        }
    }
}