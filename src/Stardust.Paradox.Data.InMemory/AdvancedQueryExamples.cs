using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.InMemory.Examples
{
    /// <summary>
    /// Advanced query examples demonstrating CosmosDB-compatible Gremlin features
    /// </summary>
    public class AdvancedQueryExamples
    {
        public static async Task RunAdvancedQueriesAsync()
        {
            Console.WriteLine("=== Advanced Gremlin Query Examples ===\n");

            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.LogQueries = true;
                options.SimulatedRUPerQuery = 2.0;
            });

            // Set up sample data
            await SetupAdvancedSampleData(connector);

            Console.WriteLine("1. Multi-Step Traversals (up to 10 steps):");
            await DemoMultiStepTraversals(connector);

            Console.WriteLine("\n2. Advanced Filtering:");
            await DemoAdvancedFiltering(connector);

            Console.WriteLine("\n3. Aggregation Operations:");
            await DemoAggregationOperations(connector);

            Console.WriteLine("\n4. Property Operations:");
            await DemoPropertyOperations(connector);

            Console.WriteLine("\n5. Ordering and Limiting:");
            await DemoOrderingAndLimiting(connector);

            Console.WriteLine("\n6. Complex Chained Operations:");
            await DemoComplexChainedOperations(connector);

            Console.WriteLine($"\nTotal RU Consumed: {connector.ConsumedRU:F2}");
        }

        private static async Task SetupAdvancedSampleData(InMemoryGremlinLanguageConnector connector)
        {
            // Create a more complex graph structure
            var vertices = new[]
            {
                ("john", "person", new Dictionary<string, object> { { "name", "John Doe" }, { "age", 30 }, { "department", "Engineering" }, { "salary", 75000 } }),
                ("jane", "person", new Dictionary<string, object> { { "name", "Jane Smith" }, { "age", 28 }, { "department", "Engineering" }, { "salary", 80000 } }),
                ("bob", "person", new Dictionary<string, object> { { "name", "Bob Johnson" }, { "age", 35 }, { "department", "Sales" }, { "salary", 65000 } }),
                ("alice", "person", new Dictionary<string, object> { { "name", "Alice Brown" }, { "age", 32 }, { "department", "Marketing" }, { "salary", 70000 } }),
                ("tech_corp", "company", new Dictionary<string, object> { { "name", "Tech Corp" }, { "founded", 2010 }, { "industry", "Technology" } }),
                ("proj1", "project", new Dictionary<string, object> { { "name", "Project Alpha" }, { "budget", 100000 }, { "status", "active" } }),
                ("proj2", "project", new Dictionary<string, object> { { "name", "Project Beta" }, { "budget", 150000 }, { "status", "completed" } }),
                ("skill1", "skill", new Dictionary<string, object> { { "name", "C#" }, { "level", "expert" } }),
                ("skill2", "skill", new Dictionary<string, object> { { "name", "JavaScript" }, { "level", "intermediate" } }),
                ("skill3", "skill", new Dictionary<string, object> { { "name", "Python" }, { "level", "beginner" } })
            };

            var edges = new[]
            {
                ("emp1", "works_for", "john", "tech_corp", new Dictionary<string, object> { { "start_date", DateTime.Now.AddYears(-2) }, { "position", "Senior Developer" } }),
                ("emp2", "works_for", "jane", "tech_corp", new Dictionary<string, object> { { "start_date", DateTime.Now.AddYears(-1) }, { "position", "Lead Developer" } }),
                ("emp3", "works_for", "bob", "tech_corp", new Dictionary<string, object> { { "start_date", DateTime.Now.AddYears(-3) }, { "position", "Sales Manager" } }),
                ("emp4", "works_for", "alice", "tech_corp", new Dictionary<string, object> { { "start_date", DateTime.Now.AddMonths(-6) }, { "position", "Marketing Specialist" } }),
                ("proj_assign1", "assigned_to", "john", "proj1", new Dictionary<string, object> { { "role", "lead" } }),
                ("proj_assign2", "assigned_to", "jane", "proj1", new Dictionary<string, object> { { "role", "developer" } }),
                ("proj_assign3", "assigned_to", "john", "proj2", new Dictionary<string, object> { { "role", "developer" } }),
                ("skill_has1", "has_skill", "john", "skill1", new Dictionary<string, object>()),
                ("skill_has2", "has_skill", "john", "skill2", new Dictionary<string, object>()),
                ("skill_has3", "has_skill", "jane", "skill1", new Dictionary<string, object>()),
                ("skill_has4", "has_skill", "bob", "skill3", new Dictionary<string, object>()),
                ("knows1", "knows", "john", "jane", new Dictionary<string, object> { { "since", DateTime.Now.AddYears(-2) } }),
                ("knows2", "knows", "jane", "bob", new Dictionary<string, object> { { "since", DateTime.Now.AddYears(-1) } })
            };

            connector.LoadData(vertices, edges);
            Console.WriteLine("Advanced sample data loaded successfully.");
        }

        private static async Task DemoMultiStepTraversals(InMemoryGremlinLanguageConnector connector)
        {
            // 7-step traversal: Find colleagues of John through projects
            var result1 = await connector.ExecuteAsync(
                "g.V('john').out('assigned_to').in('assigned_to').out('works_for').in('works_for').has('name').dedup()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Colleagues through projects: {result1.Count()} found");

            // 6-step traversal: Find skills of people in Engineering department
            var result2 = await connector.ExecuteAsync(
                "g.V().has('department', 'Engineering').out('has_skill').values('name').dedup()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Engineering skills: {result2.Count()} found");

            // 8-step traversal: Complex relationship path
            var result3 = await connector.ExecuteAsync(
                "g.V('john').out('knows').out('knows').out('works_for').in('works_for').hasLabel('person').values('name')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Extended network: {result3.Count()} people");
        }

        private static async Task DemoAdvancedFiltering(InMemoryGremlinLanguageConnector connector)
        {
            // Multiple has conditions
            var result1 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').has('age').has('department', 'Engineering')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Engineering people with age: {result1.Count()}");

            // hasId with multiple IDs
            var result2 = await connector.ExecuteAsync(
                "g.V().hasId('john', 'jane', 'bob')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Specific people by ID: {result2.Count()}");

            // Deduplication
            var result3 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').out('works_for').in('works_for').dedup()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Unique colleagues: {result3.Count()}");
        }

        private static async Task DemoAggregationOperations(InMemoryGremlinLanguageConnector connector)
        {
            // Count aggregation
            var result1 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').count()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Total people: {result1.First()}");

            // Sum of salaries
            var result2 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').values('salary').sum()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Total salary: ${result2.First()}");

            // Max salary
            var result3 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').values('salary').max()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Max salary: ${result3.First()}");

            // Min age
            var result4 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').values('age').min()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Min age: {result4.First()}");

            // Mean salary
            var result5 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').values('salary').mean()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Mean salary: ${result5.First():F2}");

            // Group count by department
            var result6 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').groupCount().by('department')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Department groups: {result6.Count()}");
        }

        private static async Task DemoPropertyOperations(InMemoryGremlinLanguageConnector connector)
        {
            // Get all property values
            var result1 = await connector.ExecuteAsync(
                "g.V('john').values()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   John's property values: {result1.Count()}");

            // Get specific property values
            var result2 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').values('name', 'department')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Names and departments: {result2.Count()}");

            // Value map
            var result3 = await connector.ExecuteAsync(
                "g.V('john').valueMap()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   John's value map: {result3.Count()}");

            // Element map (includes id, label, type)
            var result4 = await connector.ExecuteAsync(
                "g.V('john').elementMap()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   John's element map: {result4.Count()}");

            // Properties with keys
            var result5 = await connector.ExecuteAsync(
                "g.V('john').properties('name', 'age')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   John's specific properties: {result5.Count()}");
        }

        private static async Task DemoOrderingAndLimiting(InMemoryGremlinLanguageConnector connector)
        {
            // Basic ordering
            var result1 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').order().limit(2)", 
                new Dictionary<string, object>());
            Console.WriteLine($"   First 2 ordered people: {result1.Count()}");

            // Range selection
            var result2 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').range(1, 3)", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Range 1-3: {result2.Count()}");

            // Skip and limit
            var result3 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').skip(1).limit(2)", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Skip 1, limit 2: {result3.Count()}");

            // Tail (last N elements)
            var result4 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').tail(2)", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Last 2 people: {result4.Count()}");

            // Sample (random selection)
            var result5 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').sample(2)", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Random sample of 2: {result5.Count()}");
        }

        private static async Task DemoComplexChainedOperations(InMemoryGremlinLanguageConnector connector)
        {
            // 10-step complex query: Find the most skilled people in active projects
            var result1 = await connector.ExecuteAsync(
                "g.V().hasLabel('project').has('status', 'active').in('assigned_to').out('has_skill').in('has_skill').dedup().values('name').fold()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   People in active projects with skills: {result1.Count()}");

            // Multi-step aggregation and filtering
            var result2 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').has('salary').values('salary').fold().unfold().sum()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Total salary (fold/unfold): ${result2.First()}");

            // Path tracking
            var result3 = await connector.ExecuteAsync(
                "g.V('john').out('knows').out('works_for').path()", 
                new Dictionary<string, object>());
            Console.WriteLine($"   John's relationship paths: {result3.Count()}");

            // Constant values
            var result4 = await connector.ExecuteAsync(
                "g.V().hasLabel('person').constant('employee').limit(3)", 
                new Dictionary<string, object>());
            Console.WriteLine($"   Constant employee labels: {result4.Count()}");

            // Identity pass-through
            var result5 = await connector.ExecuteAsync(
                "g.V('john').identity().values('name')", 
                new Dictionary<string, object>());
            Console.WriteLine($"   John's name via identity: {result5.Count()}");
        }

        public static async Task DemoCosmosDbCompatibilityAsync()
        {
            Console.WriteLine("=== CosmosDB Compatibility Examples ===\n");

            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.LogQueries = true;
                options.SimulatedRUPerQuery = 1.5;
            });

            // Set up CosmosDB-style data
            await SetupCosmosDbStyleData(connector);

            Console.WriteLine("1. Vertex and Edge Navigation:");
            await DemoVertexEdgeNavigation(connector);

            Console.WriteLine("\n2. Advanced Filtering Predicates:");
            await DemoAdvancedPredicates(connector);

            Console.WriteLine("\n3. Projection and Selection:");
            await DemoProjectionSelection(connector);

            Console.WriteLine($"\nTotal RU Consumed: {connector.ConsumedRU:F2}");
        }

        private static async Task SetupCosmosDbStyleData(InMemoryGremlinLanguageConnector connector)
        {
            // Create data similar to CosmosDB samples
            await connector.ExecuteAsync("g.addV('person').property('id', 'user1').property('firstName', 'Thomas').property('lastName', 'Andersen').property('age', 44)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'user2').property('firstName', 'Mary').property('lastName', 'Kay').property('age', 35)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'user3').property('firstName', 'Robin').property('lastName', 'Wakefield').property('age', 29)", new Dictionary<string, object>());
            
            await connector.ExecuteAsync("g.V('user1').addE('knows').to(g.V('user2')).property('weight', 0.75)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('user1').addE('knows').to(g.V('user3')).property('weight', 0.5)", new Dictionary<string, object>());

            Console.WriteLine("CosmosDB-style sample data loaded.");
        }

        private static async Task DemoVertexEdgeNavigation(InMemoryGremlinLanguageConnector connector)
        {
            // Get all vertices
            var result1 = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
            Console.WriteLine($"   All vertices: {result1.Count()}");

            // Get all edges
            var result2 = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            Console.WriteLine($"   All edges: {result2.Count()}");

            // Traverse out from user1
            var result3 = await connector.ExecuteAsync("g.V('user1').out('knows')", new Dictionary<string, object>());
            Console.WriteLine($"   Users known by user1: {result3.Count()}");

            // Get outgoing edges
            var result4 = await connector.ExecuteAsync("g.V('user1').outE('knows')", new Dictionary<string, object>());
            Console.WriteLine($"   Outgoing 'knows' edges from user1: {result4.Count()}");

            // Navigate from edge to vertices
            var result5 = await connector.ExecuteAsync("g.V('user1').outE('knows').inV()", new Dictionary<string, object>());
            Console.WriteLine($"   Target vertices of 'knows' edges: {result5.Count()}");
        }

        private static async Task DemoAdvancedPredicates(InMemoryGremlinLanguageConnector connector)
        {
            // Note: These would require more advanced predicate parsing
            // For now, showing basic has() filtering
            
            var result1 = await connector.ExecuteAsync("g.V().hasLabel('person').has('age')", new Dictionary<string, object>());
            Console.WriteLine($"   People with age property: {result1.Count()}");

            var result2 = await connector.ExecuteAsync("g.V().hasLabel('person').has('firstName', 'Thomas')", new Dictionary<string, object>());
            Console.WriteLine($"   People named Thomas: {result2.Count()}");
        }

        private static async Task DemoProjectionSelection(InMemoryGremlinLanguageConnector connector)
        {
            // Get specific property values
            var result1 = await connector.ExecuteAsync("g.V().hasLabel('person').values('firstName')", new Dictionary<string, object>());
            Console.WriteLine($"   First names: {result1.Count()}");

            // Get value maps
            var result2 = await connector.ExecuteAsync("g.V('user1').valueMap()", new Dictionary<string, object>());
            Console.WriteLine($"   User1 value map: {result2.Count()}");

            // Get element maps
            var result3 = await connector.ExecuteAsync("g.V('user1').elementMap()", new Dictionary<string, object>());
            Console.WriteLine($"   User1 element map: {result3.Count()}");
        }
    }
}