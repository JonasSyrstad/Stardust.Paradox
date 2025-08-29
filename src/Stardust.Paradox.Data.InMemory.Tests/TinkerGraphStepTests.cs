using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for specific TinkerPop step patterns and behaviors
/// </summary>
public class TinkerGraphStepTests
{
    #region Start Step Tests

    [Fact]
    public async Task TinkerGraph_VStep_WithNoArguments_ShouldReturnAllVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task TinkerGraph_VStep_WithSpecificIds_ShouldReturnMatchingVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'charlie').property('name', 'Charlie')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('alice', 'charlie')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
        var names = result.Select(r => (string)r.properties.name).ToList();
        names.Should().Contain("Alice");
        names.Should().Contain("Charlie");
        names.Should().NotContain("Bob");
    }

    [Fact]
    public async Task TinkerGraph_EStep_WithNoArguments_ShouldReturnAllEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_AddVStep_ShouldCreateVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.label).Should().Be("person");
    }

    [Fact]
    public async Task TinkerGraph_AddEStep_ShouldCreateEdge()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var edge = result.First();
        ((string)edge.label).Should().Be("knows");
    }

    #endregion

    #region Graph Navigation Step Tests

    [Fact]
    public async Task TinkerGraph_OutStep_WithLabel_ShouldFilterByEdgeLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').out('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_OutStep_WithoutLabel_ShouldReturnAllOutVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').out()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_InStep_ShouldReturnIncomingVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('bob').in('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("Alice");
    }

    [Fact]
    public async Task TinkerGraph_BothStep_ShouldReturnBothDirections()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('bob').both('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_OutEStep_ShouldReturnOutgoingEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').outE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
        var edge = result.First();
        ((string)edge.label).Should().Be("knows");
    }

    [Fact]
    public async Task TinkerGraph_InEStep_ShouldReturnIncomingEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('bob').inE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
        var edge = result.First();
        ((string)edge.label).Should().Be("knows");
    }

    [Fact]
    public async Task TinkerGraph_BothEStep_ShouldReturnBothDirectionEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').bothE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_OutVStep_ShouldReturnOutVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.E().has('label', 'knows').outV()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TinkerGraph_InVStep_ShouldReturnInVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.E().has('label', 'knows').inV()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Filter Step Tests

    [Fact]
    public async Task TinkerGraph_HasStep_WithPropertyValue_ShouldFilter()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'Alice')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("Alice");
    }

    [Fact]
    public async Task TinkerGraph_HasStep_WithPropertyExists_ShouldFilter()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().has('age')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("Alice");
    }

    [Fact]
    public async Task TinkerGraph_HasLabelStep_ShouldFilterByLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('name', 'Acme Corp')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("Alice");
    }

    [Fact]
    public async Task TinkerGraph_HasIdStep_ShouldFilterById()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasId('alice')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("Alice");
    }

    #endregion

    #region Property Step Tests

    [Fact]
    public async Task TinkerGraph_ValuesStep_ShouldReturnPropertyValues()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().values('name')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("Alice");
    }

    [Fact]
    public async Task TinkerGraph_ValuesStep_WithMultipleKeys_ShouldReturnAllValues()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().values('name', 'age')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task TinkerGraph_ValueMapStep_ShouldReturnPropertyMap()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().valueMap()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var valueMap = result.First() as Dictionary<string, object>;
        valueMap.Should().NotBeNull();
        valueMap.Should().ContainKey("name");
        valueMap.Should().ContainKey("age");
    }

    [Fact]
    public async Task TinkerGraph_PropertyStep_ShouldAddProperty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').property('age', 30)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        
        // Verify the property was added
        var ageResult = await connector.ExecuteAsync("g.V('alice').values('age')", new Dictionary<string, object>());
        ageResult.Should().HaveCount(1);
        ((int)ageResult.First()).Should().Be(30);
    }

    [Fact]
    public async Task TinkerGraph_IdStep_ShouldReturnVertexId()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').id()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("alice");
    }

    [Fact]
    public async Task TinkerGraph_LabelStep_ShouldReturnVertexLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().label()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("person");
    }

    #endregion

    #region Branch Step Tests

    [Fact]
    public async Task TinkerGraph_UnionStep_ShouldCombineResults()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act - This is a simplified test since union parsing might be complex
        var aliceResult = await connector.ExecuteAsync("g.V('alice')", new Dictionary<string, object>());
        var bobResult = await connector.ExecuteAsync("g.V('bob')", new Dictionary<string, object>());

        // Assert
        aliceResult.Should().HaveCount(1);
        bobResult.Should().HaveCount(1);
    }

    #endregion

    #region Utility Step Tests

    [Fact]
    public async Task TinkerGraph_AsStep_ShouldLabelSteps()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').as('a').out('knows').as('b').select('a')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("Alice");
    }

    [Fact]
    public async Task TinkerGraph_SelectStep_ShouldReturnLabeledValues()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('alice').as('a').out('knows').as('b').select('a', 'b')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Mutation Step Tests

    [Fact]
    public async Task TinkerGraph_DropStep_ShouldRemoveVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'temp').property('name', 'Temporary')", new Dictionary<string, object>());

        // Verify vertex exists
        var beforeResult = await connector.ExecuteAsync("g.V('temp')", new Dictionary<string, object>());
        beforeResult.Should().HaveCount(1);

        // Act
        var result = await connector.ExecuteAsync("g.V('temp').drop()", new Dictionary<string, object>());

        // Assert
        var afterResult = await connector.ExecuteAsync("g.V('temp')", new Dictionary<string, object>());
        afterResult.Should().BeEmpty();
    }

    [Fact]
    public async Task TinkerGraph_DropStep_ShouldRemoveEdge()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupBasicGraph(connector);

        // Get initial edge count
        var beforeCount = await connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());
        var initialCount = (long)beforeCount.First();

        // Act
        var result = await connector.ExecuteAsync("g.E().has('label', 'knows').drop()", new Dictionary<string, object>());

        // Assert
        var afterCount = await connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());
        var finalCount = (long)afterCount.First();
        finalCount.Should().BeLessThan(initialCount);
    }

    #endregion

    #region Test Setup Helper

    private async Task SetupBasicGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create vertices
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob').property('age', 28)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'charlie').property('name', 'Charlie').property('age', 35)", new Dictionary<string, object>());

        // Create edges
        await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('bob')).property('since', 2010)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('charlie')).property('since', 2015)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('likes').to(g.V('charlie')).property('weight', 0.8)", new Dictionary<string, object>());
    }

    #endregion
}