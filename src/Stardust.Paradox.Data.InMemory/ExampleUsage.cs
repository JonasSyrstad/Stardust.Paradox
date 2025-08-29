using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.InMemory.Examples
{
    /// <summary>
    /// Simple example demonstrating InMemoryGremlinLanguageConnector usage
    /// </summary>
    public class ExampleUsage
    {
        public static async Task RunExampleAsync()
        {
            // Create a new in-memory database
            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.LogQueries = true;
                options.SimulatedRUPerQuery = 1.5;
            });

            // Add some sample data
            connector.WithSampleData();

            // Example 1: Basic vertex and edge queries
            Console.WriteLine("=== Basic Queries ===");
            
            var allVertices = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
            Console.WriteLine($"Total vertices: {allVertices.Count()}");

            var persons = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
            Console.WriteLine($"Persons: {persons.Count()}");

            // Example 2: Traversal queries
            Console.WriteLine("\n=== Traversal Queries ===");
            
            var colleagues = await connector.ExecuteAsync("g.V('person1').out('works_for').in('works_for')", 
                new Dictionary<string, object>());
            Console.WriteLine($"Colleagues of person1: {colleagues.Count()}");

            // Example 3: Parameterized queries
            Console.WriteLine("\n=== Parameterized Queries ===");
            
            var searchResult = await connector.ExecuteAsync("g.V().has('name', p0)", 
                new Dictionary<string, object> { { "p0", "John Doe" } });
            Console.WriteLine($"Search results: {searchResult.Count()}");

            // Example 4: Count queries
            Console.WriteLine("\n=== Count Queries ===");
            
            var vertexCount = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            var edgeCount = await connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());
            
            Console.WriteLine($"Vertex count: {vertexCount.First()}");
            Console.WriteLine($"Edge count: {edgeCount.First()}");

            // Example 5: Custom responses for complex queries
            Console.WriteLine("\n=== Custom Responses ===");
            
            connector.RegisterCustomResponse(@"g\.V\(\)\.group\(\)\.by\('label'\)", 
                (query, parameters) => new[]
                {
                    new Dictionary<string, object>
                    {
                        { "person", new[] { "person1", "person2" } },
                        { "company", new[] { "company1" } }
                    }
                });

            var groupResult = await connector.ExecuteAsync("g.V().group().by('label')", new Dictionary<string, object>());
            Console.WriteLine($"Group result: {groupResult.Count()} groups");

            // Example 6: Database statistics
            Console.WriteLine("\n=== Database Statistics ===");
            Console.WriteLine(connector.GetSummary());
            Console.WriteLine($"Consumed RU: {connector.ConsumedRU:F2}");
        }

        public static async Task RunCrudExampleAsync()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();

            Console.WriteLine("=== CRUD Operations Example ===\n");

            // CREATE
            Console.WriteLine("1. Creating vertices and edges...");
            await connector.ExecuteAsync("g.addV('user').property('id', 'user1').property('name', 'Alice')", 
                new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('user').property('id', 'user2').property('name', 'Bob')", 
                new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('user1').addE('follows').to(g.V('user2'))", 
                new Dictionary<string, object>());

            // READ
            Console.WriteLine("2. Reading data...");
            var users = await connector.ExecuteAsync("g.V().hasLabel('user')", new Dictionary<string, object>());
            Console.WriteLine($"   Found {users.Count()} users");

            var relationships = await connector.ExecuteAsync("g.E().hasLabel('follows')", new Dictionary<string, object>());
            Console.WriteLine($"   Found {relationships.Count()} relationships");

            // UPDATE
            Console.WriteLine("3. Updating properties...");
            await connector.ExecuteAsync("g.V('user1').property('email', 'alice@example.com')", 
                new Dictionary<string, object>());

            // READ updated data
            var updatedUser = await connector.ExecuteAsync("g.V('user1')", new Dictionary<string, object>());
            Console.WriteLine($"   Updated user: {updatedUser.Count()} result");

            // DELETE
            Console.WriteLine("4. Deleting data...");
            await connector.ExecuteAsync("g.V('user2').drop()", new Dictionary<string, object>());

            var remainingUsers = await connector.ExecuteAsync("g.V().hasLabel('user')", new Dictionary<string, object>());
            Console.WriteLine($"   Remaining users: {remainingUsers.Count()}");

            Console.WriteLine("\nCRUD example completed!");
        }

        public static void RunBulkDataExample()
        {
            Console.WriteLine("=== Bulk Data Example ===\n");

            var connector = InMemoryGremlinLanguageConnector.Create()
                .WithBulkData(1000, 2000)
                .WithPerformanceSettings();

            var (vertexCount, edgeCount) = connector.GetStatistics();
            Console.WriteLine($"Generated {vertexCount} vertices and {edgeCount} edges");
            Console.WriteLine(connector.GetSummary());
        }
    }
}