using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Comprehensive TinkerGraph-compatible test suite inspired by Apache TinkerPop's TinkerGraph tests
/// </summary>
public class TinkerGraphCompatibilityTests
{
    #region Basic Graph Structure Tests

    [Fact]
    public async Task TinkerGraph_BasicVertexOperations_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act & Assert - Add vertices
        var result1 = await connector.ExecuteAsync("g.addV('person').property('name', 'marko').property('age', 29)", new Dictionary<string, object>());
        result1.Should().HaveCount(1);

        var result2 = await connector.ExecuteAsync("g.addV('person').property('name', 'vadas').property('age', 27)", new Dictionary<string, object>());
        result2.Should().HaveCount(1);

        var result3 = await connector.ExecuteAsync("g.addV('software').property('name', 'lop').property('lang', 'java')", new Dictionary<string, object>());
        result3.Should().HaveCount(1);

        // Check vertex count
        var countResult = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
        ((long)countResult.First()).Should().Be(3);
    }

    [Fact]
    public async Task TinkerGraph_BasicEdgeOperations_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act & Assert - Add edges
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').addE('knows').to(g.V().has('name', 'vadas')).property('weight', 0.5)", new Dictionary<string, object>());
        result.Should().HaveCount(1);

        // Check edge count
        var edgeCount = await connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());
        ((long)edgeCount.First()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_VertexIdRetrieval_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').id()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertexId = result.First();
        ((string)vertexId).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TinkerGraph_VertexLabelRetrieval_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').label()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("person");
    }

    [Fact]
    public async Task TinkerGraph_VertexPropertyValues_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').values('age')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((int)result.First()).Should().Be(29);
    }

    #endregion

    #region Traversal Step Tests

    [Fact]
    public async Task TinkerGraph_OutStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').out('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_InStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'vadas').in('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_BothStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').both('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_OutEStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').outE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_InEStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'vadas').inE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_BothEStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').bothE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_OutVStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.E().has('weight', 0.5).outV()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_InVStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.E().has('weight', 0.5).inV()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Filter Step Tests

    [Fact]
    public async Task TinkerGraph_HasStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("marko");
    }

    [Fact]
    public async Task TinkerGraph_HasLabelStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
        foreach (var vertex in result)
        {
            ((string)vertex.label).Should().Be("person");
        }
    }

    [Fact]
    public async Task TinkerGraph_HasIdStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Add vertex with known ID
        var addResult = await connector.ExecuteAsync("g.addV('person').property('id', 'test-vertex').property('name', 'Test')", new Dictionary<string, object>());
        
        // Act
        var result = await connector.ExecuteAsync("g.V().hasId('test-vertex')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.id).Should().Be("test-vertex");
    }

    [Fact]
    public async Task TinkerGraph_WhereStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act - This is a simplified where step test
        var result = await connector.ExecuteAsync("g.V().where(g.V().has('name', 'marko'))", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Aggregation Step Tests

    [Fact]
    public async Task TinkerGraph_CountStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_GroupStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().group().by('label')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groups = result.First() as Dictionary<string, List<dynamic>>;
        groups.Should().NotBeNull();
        groups.Should().ContainKey("person");
    }

    [Fact]
    public async Task TinkerGraph_GroupCountStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().groupCount().by('label')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var counts = result.First() as Dictionary<string, long>;
        counts.Should().NotBeNull();
        counts.Should().ContainKey("person");
    }

    [Fact]
    public async Task TinkerGraph_FoldStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().values('name').fold()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var folded = result.First() as List<dynamic>;
        folded.Should().NotBeNull();
        folded.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TinkerGraph_UnfoldStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().values('name').fold().unfold()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Barrier Step Tests

    [Fact]
    public async Task TinkerGraph_DedupStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().out().in().dedup()", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TinkerGraph_OrderStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().values('name').order()", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TinkerGraph_LimitStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().limit(2)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task TinkerGraph_SkipStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var totalCount = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
        var skipResult = await connector.ExecuteAsync("g.V().skip(1)", new Dictionary<string, object>());

        // Assert
        var total = (long)totalCount.First();
        skipResult.Should().HaveCount((int)total - 1);
    }

    [Fact]
    public async Task TinkerGraph_RangeStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().range(1, 3)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2); // range is exclusive on the end
    }

    [Fact]
    public async Task TinkerGraph_SampleStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().sample(2)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task TinkerGraph_TailStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().tail(2)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountLessOrEqualTo(2);
    }

    #endregion

    #region Property Step Tests

    [Fact]
    public async Task TinkerGraph_ValuesStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().values('name')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
        result.All(r => r is string).Should().BeTrue();
    }

    [Fact]
    public async Task TinkerGraph_ValueMapStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').valueMap()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var valueMap = result.First() as Dictionary<string, object>;
        valueMap.Should().NotBeNull();
        valueMap.Should().ContainKey("name");
        valueMap.Should().ContainKey("age");
    }

    [Fact]
    public async Task TinkerGraph_PropertyStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Add a vertex
        await connector.ExecuteAsync("g.addV('person').property('id', 'test').property('name', 'Test')", new Dictionary<string, object>());

        // Act - Add another property
        var result = await connector.ExecuteAsync("g.V('test').property('age', 30)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        
        // Verify the property was added
        var checkResult = await connector.ExecuteAsync("g.V('test').values('age')", new Dictionary<string, object>());
        checkResult.Should().HaveCount(1);
        ((int)checkResult.First()).Should().Be(30);
    }

    #endregion

    #region Path and Select Tests

    [Fact]
    public async Task TinkerGraph_PathStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').out('knows').path()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_AsStep_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').as('a').out('knows').as('b').select('a', 'b')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Complex Traversal Tests

    [Fact]
    public async Task TinkerGraph_MultiStepTraversal_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').out('knows').values('name')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
        result.All(r => r is string).Should().BeTrue();
    }

    [Fact]
    public async Task TinkerGraph_FriendsOfFriends_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'marko').out('knows').out('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TinkerGraph_CreatedByAge_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupModernGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').has('age', 29).out('created').values('name')", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task TinkerGraph_LargeGraphTraversal_ShouldPerformWell()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create a larger graph
        for (int i = 0; i < 100; i++)
        {
            await connector.ExecuteAsync($"g.addV('person').property('id', 'person_{i}').property('name', 'Person{i}').property('age', {20 + (i % 50)})", new Dictionary<string, object>());
        }

        // Connect some vertices
        for (int i = 0; i < 50; i++)
        {
            await connector.ExecuteAsync($"g.V('person_{i}').addE('knows').to(g.V('person_{i + 50}'))", new Dictionary<string, object>());
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await connector.ExecuteAsync("g.V().out('knows').values('name')", new Dictionary<string, object>());
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(50);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
    }

    #endregion

    #region Test Setup Helper

    /// <summary>
    /// Set up the classic "Modern" graph used in TinkerPop examples
    /// </summary>
    private async Task SetupModernGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Add vertices
        await connector.ExecuteAsync("g.addV('person').property('id', '1').property('name', 'marko').property('age', 29)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', '2').property('name', 'vadas').property('age', 27)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('software').property('id', '3').property('name', 'lop').property('lang', 'java')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', '4').property('name', 'josh').property('age', 32)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('software').property('id', '5').property('name', 'ripple').property('lang', 'java')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', '6').property('name', 'peter').property('age', 35)", new Dictionary<string, object>());

        // Add edges
        await connector.ExecuteAsync("g.V('1').addE('knows').to(g.V('2')).property('weight', 0.5)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('1').addE('knows').to(g.V('4')).property('weight', 1.0)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('1').addE('created').to(g.V('3')).property('weight', 0.4)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('4').addE('created').to(g.V('5')).property('weight', 1.0)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('4').addE('created').to(g.V('3')).property('weight', 0.4)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('6').addE('created').to(g.V('3')).property('weight', 0.2)", new Dictionary<string, object>());
    }

    #endregion
}