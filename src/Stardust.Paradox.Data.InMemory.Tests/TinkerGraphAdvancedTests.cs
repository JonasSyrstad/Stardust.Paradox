using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Advanced TinkerGraph tests for stress testing and edge cases
/// </summary>
public class TinkerGraphAdvancedTests
{
    #region Stress Tests

    [Fact]
    public async Task TinkerGraph_LargeVertexSet_ShouldHandleEfficiently()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        const int vertexCount = 1000;

        // Act - Create many vertices
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < vertexCount; i++)
        {
            await connector.ExecuteAsync($"g.addV('node').property('id', 'node_{i}').property('value', {i})", new Dictionary<string, object>());
        }
        stopwatch.Stop();

        // Assert
        var countResult = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
        ((long)countResult.First()).Should().Be(vertexCount);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    [Fact]
    public async Task TinkerGraph_LargeEdgeSet_ShouldHandleEfficiently()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        const int nodeCount = 100;
        const int edgeCount = 500;

        // Create nodes
        for (int i = 0; i < nodeCount; i++)
        {
            await connector.ExecuteAsync($"g.addV('node').property('id', 'node_{i}')", new Dictionary<string, object>());
        }

        // Act - Create many edges
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < edgeCount; i++)
        {
            var fromId = random.Next(nodeCount);
            var toId = random.Next(nodeCount);
            if (fromId != toId) // Avoid self-loops for this test
            {
                await connector.ExecuteAsync($"g.V('node_{fromId}').addE('connects').to(g.V('node_{toId}'))", new Dictionary<string, object>());
            }
        }
        stopwatch.Stop();

        // Assert
        var edgeCountResult = await connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());
        ((long)edgeCountResult.First()).Should().BeGreaterThan(400); // Should have most edges (some might be skipped due to self-loops)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Should complete within 10 seconds
    }

    [Fact]
    public async Task TinkerGraph_DeepTraversal_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create a chain of nodes
        const int chainLength = 20;
        for (int i = 0; i < chainLength; i++)
        {
            await connector.ExecuteAsync($"g.addV('node').property('id', 'node_{i}').property('level', {i})", new Dictionary<string, object>());
        }

        // Connect them in a chain
        for (int i = 0; i < chainLength - 1; i++)
        {
            await connector.ExecuteAsync($"g.V('node_{i}').addE('next').to(g.V('node_{i + 1}'))", new Dictionary<string, object>());
        }

        // Act - Deep traversal
        var result = await connector.ExecuteAsync("g.V('node_0').repeat(g.out('next')).times(10).values('level')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((int)result.First()).Should().Be(10);
    }

    [Fact]
    public async Task TinkerGraph_ComplexAggregation_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupComplexGraph(connector);

        // Act - Complex aggregation query
        var result = await connector.ExecuteAsync(
            "g.V().hasLabel('person').group().by('department').by(g.values('salary').sum())",
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groups = result.First() as Dictionary<string, long>;
        groups.Should().NotBeNull();
        groups.Should().ContainKey("Engineering");
        groups.Should().ContainKey("Sales");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TinkerGraph_EmptyGraph_ShouldHandleGracefully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act & Assert
        var vertexCount = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
        ((long)vertexCount.First()).Should().Be(0);

        var edgeCount = await connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());
        ((long)edgeCount.First()).Should().Be(0);

        var traversalResult = await connector.ExecuteAsync("g.V().out().count()", new Dictionary<string, object>());
        ((long)traversalResult.First()).Should().Be(0);
    }

    [Fact]
    public async Task TinkerGraph_NonExistentVertex_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'existing').property('name', 'Test')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('non-existent')", new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TinkerGraph_SelfLoop_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'self').property('name', 'Self')", new Dictionary<string, object>());

        // Act - Create self-loop
        await connector.ExecuteAsync("g.V('self').addE('likes').to(g.V('self'))", new Dictionary<string, object>());

        // Assert
        var result = await connector.ExecuteAsync("g.V('self').out('likes')", new Dictionary<string, object>());
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("Self");
    }

    [Fact]
    public async Task TinkerGraph_MultipleEdgesSameVertices_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'a').property('name', 'A')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'b').property('name', 'B')", new Dictionary<string, object>());

        // Act - Create multiple edges between same vertices
        await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('a').addE('likes').to(g.V('b'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('a').addE('works_with').to(g.V('b'))", new Dictionary<string, object>());

        // Assert
        var result = await connector.ExecuteAsync("g.V('a').outE().count()", new Dictionary<string, object>());
        ((long)result.First()).Should().Be(3);
    }

    [Fact]
    public async Task TinkerGraph_CircularTraversal_ShouldNotInfiniteLoop()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create circular graph
        await connector.ExecuteAsync("g.addV('node').property('id', 'a')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('node').property('id', 'b')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('node').property('id', 'c')", new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('a').addE('next').to(g.V('b'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('b').addE('next').to(g.V('c'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('c').addE('next').to(g.V('a'))", new Dictionary<string, object>());

        // Act - Limited traversal should not infinite loop
        var result = await connector.ExecuteAsync("g.V('a').repeat(g.out('next')).times(5)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task TinkerGraph_PropertyOverwrite_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'test').property('name', 'Original').property('age', 25)", new Dictionary<string, object>());

        // Act - Overwrite property
        await connector.ExecuteAsync("g.V('test').property('name', 'Updated')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('test').property('age', 26)", new Dictionary<string, object>());

        // Assert
        var nameResult = await connector.ExecuteAsync("g.V('test').values('name')", new Dictionary<string, object>());
        ((string)nameResult.First()).Should().Be("Updated");

        var ageResult = await connector.ExecuteAsync("g.V('test').values('age')", new Dictionary<string, object>());
        ((int)ageResult.First()).Should().Be(26);
    }

    [Fact]
    public async Task TinkerGraph_NullPropertyValue_ShouldHandleCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'test').property('name', 'Test')", new Dictionary<string, object>());

        // Act - Set property to null (this might depend on implementation)
        await connector.ExecuteAsync("g.V('test').property('optional', null)", new Dictionary<string, object>());

        // Assert - Verify the vertex still exists and other properties are intact
        var result = await connector.ExecuteAsync("g.V('test').values('name')", new Dictionary<string, object>());
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("Test");
    }

    #endregion

    #region Data Type Tests

    [Fact]
    public async Task TinkerGraph_VariousDataTypes_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act - Add vertex with various property types
        await connector.ExecuteAsync("g.addV('data').property('id', 'types')" +
            ".property('string_prop', 'text')" +
            ".property('int_prop', 42)" +
            ".property('double_prop', 3.14)" +
            ".property('bool_prop', true)", new Dictionary<string, object>());

        // Assert - Verify each property type
        var stringResult = await connector.ExecuteAsync("g.V('types').values('string_prop')", new Dictionary<string, object>());
        ((string)stringResult.First()).Should().Be("text");

        var intResult = await connector.ExecuteAsync("g.V('types').values('int_prop')", new Dictionary<string, object>());
        ((int)intResult.First()).Should().Be(42);

        var doubleResult = await connector.ExecuteAsync("g.V('types').values('double_prop')", new Dictionary<string, object>());
        ((double)doubleResult.First()).Should().Be(3.14);

        var boolResult = await connector.ExecuteAsync("g.V('types').values('bool_prop')", new Dictionary<string, object>());
        ((bool)boolResult.First()).Should().BeTrue();
    }

    [Fact]
    public async Task TinkerGraph_SpecialCharactersInProperties_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        await connector.ExecuteAsync("g.addV('test').property('id', 'special')" +
            ".property('name', 'Test with spaces')" +
            ".property('description', 'Contains \"quotes\" and other chars: !@#$%^&*()')" +
            ".property('unicode', '????')", new Dictionary<string, object>());

        // Assert
        var nameResult = await connector.ExecuteAsync("g.V('special').values('name')", new Dictionary<string, object>());
        ((string)nameResult.First()).Should().Be("Test with spaces");

        var unicodeResult = await connector.ExecuteAsync("g.V('special').values('unicode')", new Dictionary<string, object>());
        ((string)unicodeResult.First()).Should().Be("????");
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task TinkerGraph_ConcurrentReads_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Setup some data
        for (int i = 0; i < 10; i++)
        {
            await connector.ExecuteAsync($"g.addV('person').property('id', 'person_{i}').property('name', 'Person{i}')", new Dictionary<string, object>());
        }

        // Act - Concurrent reads
        var tasks = new List<Task<IEnumerable<dynamic>>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>()));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        foreach (var result in results)
        {
            ((long)result.First()).Should().Be(10);
        }
    }

    #endregion

    #region Test Setup Helper

    private async Task SetupComplexGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create employees
        await connector.ExecuteAsync("g.addV('person').property('id', 'emp1').property('name', 'John').property('department', 'Engineering').property('salary', 75000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'emp2').property('name', 'Jane').property('department', 'Engineering').property('salary', 80000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'emp3').property('name', 'Bob').property('department', 'Sales').property('salary', 60000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'emp4').property('name', 'Alice').property('department', 'Sales').property('salary', 65000)", new Dictionary<string, object>());

        // Create projects
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj1').property('name', 'Project Alpha')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj2').property('name', 'Project Beta')", new Dictionary<string, object>());

        // Create relationships
        await connector.ExecuteAsync("g.V('emp1').addE('works_on').to(g.V('proj1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp2').addE('works_on').to(g.V('proj1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp3').addE('works_on').to(g.V('proj2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp4').addE('works_on').to(g.V('proj2'))", new Dictionary<string, object>());
    }

    #endregion
}