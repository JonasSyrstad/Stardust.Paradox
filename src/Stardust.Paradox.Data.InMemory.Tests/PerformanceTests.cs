using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Performance and stress tests for the InMemory database
/// </summary>
public class PerformanceTests
{
    [Fact]
    public async Task ExecuteAsync_With1000Vertices_ShouldPerformWell()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Create 1000 vertices
        for (int i = 0; i < 1000; i++)
        {
            await connector.ExecuteAsync($"g.addV('person').property('id', 'p{i}').property('index', {i})", 
                new Dictionary<string, object>());
        }

        stopwatch.Stop();

        // Assert
        var (vertexCount, _) = connector.GetStatistics();
        vertexCount.Should().Be(1000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in under 5 seconds
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeResultSet_ShouldHandleEfficiently()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Add 500 vertices
        for (int i = 0; i < 500; i++)
        {
            await connector.ExecuteAsync($"g.addV('person').property('index', {i})", new Dictionary<string, object>());
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Query all vertices
        var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());

        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(500);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete in under 1 second
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexTraversals_ShouldScaleReasonably()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupPerformanceTestGraph(connector);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Execute complex traversal 100 times
        for (int i = 0; i < 100; i++)
        {
            await connector.ExecuteAsync("g.V().hasLabel('person').out('works_for').in('works_for').dedup()", 
                new Dictionary<string, object>());
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 100 complex queries in under 5 seconds
    }

    [Fact]
    public async Task ExecuteAsync_WithManyAggregations_ShouldPerformWell()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Add test data
        for (int i = 0; i < 200; i++)
        {
            await connector.ExecuteAsync($"g.addV('person').property('age', {20 + (i % 50)})", new Dictionary<string, object>());
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Perform multiple aggregations
        var tasks = new[]
        {
            connector.ExecuteAsync("g.V().hasLabel('person').count()", new Dictionary<string, object>()),
            connector.ExecuteAsync("g.V().hasLabel('person').values('age').sum()", new Dictionary<string, object>()),
            connector.ExecuteAsync("g.V().hasLabel('person').values('age').mean()", new Dictionary<string, object>()),
            connector.ExecuteAsync("g.V().hasLabel('person').values('age').max()", new Dictionary<string, object>()),
            connector.ExecuteAsync("g.V().hasLabel('person').values('age').min()", new Dictionary<string, object>())
        };

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // All aggregations in under 2 seconds
        tasks.All(t => t.Result.Any()).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentQueries_ShouldHandleThreadSafety()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Pre-populate with some data
        for (int i = 0; i < 50; i++)
        {
            await connector.ExecuteAsync($"g.addV('person').property('id', 'p{i}')", new Dictionary<string, object>());
        }

        // Act - Execute 20 concurrent queries
        var tasks = new List<Task<IEnumerable<dynamic>>>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(connector.ExecuteAsync("g.V().hasLabel('person').count()", new Dictionary<string, object>()));
            tasks.Add(connector.ExecuteAsync("g.V().hasLabel('person').limit(10)", new Dictionary<string, object>()));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(40);
        results.All(r => r != null).Should().BeTrue();
        
        // Verify count queries return correct result
        var countResults = results.Where((r, index) => index % 2 == 0).ToList();
        countResults.All(r => r.First().Equals(50L)).Should().BeTrue();
    }

    [Fact]
    public async Task TokenizerPerformance_WithComplexQueries_ShouldBeFast()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var complexQueries = new[]
        {
            "g.V().hasLabel('person').has('age', gt(25)).out('works_for').hasLabel('company').values('name')",
            "g.V().hasLabel('person').out('knows').out('works_for').in('works_for').hasLabel('person').dedup()",
            "g.V().hasLabel('project').in('assigned_to').hasLabel('person').has('department', 'Engineering').count()",
            "g.V().hasLabel('person').values('salary').fold().unfold().sum()",
            "g.V().hasLabel('person').group().by('department').by(count())"
        };

        await SetupPerformanceTestGraph(connector);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Execute complex queries multiple times
        foreach (var query in complexQueries)
        {
            for (int i = 0; i < 20; i++)
            {
                await connector.ExecuteAsync(query, new Dictionary<string, object>());
            }
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000); // 100 complex queries in under 3 seconds
    }

    [Fact]
    public void MemoryUsage_WithLargeGraph_ShouldBeReasonable()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Create large graph
        for (int i = 0; i < 1000; i++)
        {
            var vertex = connector.AddVertex("person", $"p{i}");
            vertex.Properties["name"] = $"Person {i}";
            vertex.Properties["age"] = 20 + (i % 60);
            vertex.Properties["email"] = $"person{i}@example.com";
        }

        // Add some edges
        for (int i = 0; i < 500; i++)
        {
            var sourceId = $"p{i}";
            var targetId = $"p{(i + 1) % 1000}";
            connector.AddEdge("knows", sourceId, targetId);
        }

        var finalMemory = GC.GetTotalMemory(false);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        memoryUsed.Should().BeLessThan(50 * 1024 * 1024); // Should use less than 50MB
        
        var (vertexCount, edgeCount) = connector.GetStatistics();
        vertexCount.Should().Be(1000);
        edgeCount.Should().Be(500);
    }

    [Fact]
    public async Task RUConsumption_ShouldTrackAccurately()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts =>
        {
            opts.SimulatedRUPerQuery = 2.5;
        });

        // Act - Execute 10 queries
        for (int i = 0; i < 10; i++)
        {
            await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
        }

        // Assert
        connector.ConsumedRU.Should().Be(25.0); // 10 queries * 2.5 RU each
    }

    [Fact]
    public async Task LargeParameterSet_ShouldHandleEfficiently()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var parameters = new Dictionary<string, object>();
        
        // Create large parameter set
        for (int i = 0; i < 100; i++)
        {
            parameters[$"p{i}"] = $"value{i}";
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await connector.ExecuteAsync("g.addV('test').property('data', p0)", parameters);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Should handle large parameter set quickly
    }

    [Fact]
    public async Task DeepTraversal_ShouldCompleteWithinLimits()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupDeepTraversalGraph(connector);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Execute maximum depth traversal (10 steps)
        var result = await connector.ExecuteAsync(
            "g.V('start').out('next').out('next').out('next').out('next').out('next').out('next').out('next').out('next').out('next').limit(1)", 
            new Dictionary<string, object>());

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Deep traversal in under 1 second
        result.Should().NotBeNull();
    }

    private static async Task SetupPerformanceTestGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create people
        for (int i = 0; i < 50; i++)
        {
            await connector.ExecuteAsync($"g.addV('person').property('id', 'p{i}').property('name', 'Person {i}').property('department', '{(i % 3 == 0 ? "Engineering" : i % 3 == 1 ? "Sales" : "Marketing")}')", 
                new Dictionary<string, object>());
        }

        // Create companies
        for (int i = 0; i < 5; i++)
        {
            await connector.ExecuteAsync($"g.addV('company').property('id', 'c{i}').property('name', 'Company {i}')", 
                new Dictionary<string, object>());
        }

        // Create work relationships
        for (int i = 0; i < 50; i++)
        {
            var companyId = $"c{i % 5}";
            await connector.ExecuteAsync($"g.V('p{i}').addE('works_for').to(g.V('{companyId}'))", 
                new Dictionary<string, object>());
        }
    }

    private static async Task SetupDeepTraversalGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create a chain of vertices for deep traversal
        await connector.ExecuteAsync("g.addV('node').property('id', 'start')", new Dictionary<string, object>());
        
        for (int i = 1; i <= 10; i++)
        {
            await connector.ExecuteAsync($"g.addV('node').property('id', 'node{i}')", new Dictionary<string, object>());
            await connector.ExecuteAsync($"g.V('node{i - 1}').addE('next').to(g.V('node{i}'))", new Dictionary<string, object>());
        }
    }
}