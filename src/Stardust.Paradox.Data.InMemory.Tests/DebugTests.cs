using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Debug tests to understand what's happening during query execution
/// </summary>
public class DebugTests
{
    [Fact]
    public async Task Debug_SimpleVertexCreation()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act - Create vertex with simple ID
        var result = await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        
        // Assert
        result.Should().HaveCount(1, "vertex creation should succeed");
        
        // Check database state directly
        var (vertexCount, _) = connector.GetStatistics();
        vertexCount.Should().Be(1, "should have 1 vertex in database");
        
        // Try to retrieve all vertices
        var allVertices = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
        allVertices.Should().HaveCount(1, "should be able to retrieve the vertex");
        
        // Try to retrieve by ID
        var specificVertex = await connector.ExecuteAsync("g.V('john')", new Dictionary<string, object>());
        specificVertex.Should().HaveCount(1, "should be able to retrieve vertex by ID");
    }

    [Fact]
    public void Debug_DirectDatabaseAccess()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act - Use direct methods
        var vertex = connector.AddVertex("person", "john");
        vertex.Properties["name"] = "John";

        // Assert - Direct access should work
        var retrieved = connector.GetVertex("john");
        retrieved.Should().NotBeNull("direct retrieval should work");
        retrieved.Id.Should().Be("john");
        retrieved.Properties["name"].Should().Be("John");
    }

    [Fact]
    public async Task Debug_QueryParsing()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Test if vertex creation query is parsed as addV
        var query1 = "g.addV('person').property('id', 'john')";
        var parser = new GremlinQueryParser(connector.Database);
        
        // This should go to ExecuteAddVertex method
        var result1 = await parser.ParseAndExecuteAsync(query1, null);
        result1.Should().HaveCount(1, "addV query should create vertex");

        // Test if vertex retrieval query is parsed correctly
        var query2 = "g.V('john')";
        var result2 = await parser.ParseAndExecuteAsync(query2, null);
        result2.Should().HaveCount(1, "V(id) query should find vertex");
    }

    [Fact]
    public async Task Debug_SetupAndTraversal_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act & Debug - Step by step
        
        // 1. Create vertices
        var result1 = await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John').property('age', 30)", new Dictionary<string, object>());
        result1.Should().HaveCount(1, "vertex creation should return 1 result");

        var result2 = await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        result2.Should().HaveCount(1, "company creation should return 1 result");

        // 2. Check if vertices exist
        var johnVertex = await connector.ExecuteAsync("g.V('john')", new Dictionary<string, object>());
        johnVertex.Should().HaveCount(1, "john vertex should exist");

        var companyVertex = await connector.ExecuteAsync("g.V('tech_corp')", new Dictionary<string, object>());
        companyVertex.Should().HaveCount(1, "company vertex should exist");

        // 3. Create edge
        var edgeResult = await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        edgeResult.Should().HaveCount(1, "edge creation should return 1 result");

        // 4. Check database state
        var (vertexCount, edgeCount) = connector.GetStatistics();
        vertexCount.Should().Be(2, "should have 2 vertices");
        edgeCount.Should().Be(1, "should have 1 edge");

        // 5. Test traversal
        var traversalResult = await connector.ExecuteAsync("g.V('john').out('works_for')", new Dictionary<string, object>());
        traversalResult.Should().HaveCount(1, "traversal should return the company");

        // Assert final result
        var company = traversalResult.First();
        ((string)company.properties.name).Should().Be("Tech Corp");
    }

    [Fact]
    public async Task Debug_ManualSetup_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Manual setup using direct methods
        var johnVertex = connector.AddVertex("person", "john");
        johnVertex.Properties["name"] = "John";
        johnVertex.Properties["age"] = 30;

        var companyVertex = connector.AddVertex("company", "tech_corp");
        companyVertex.Properties["name"] = "Tech Corp";

        var edge = connector.AddEdge("works_for", "john", "tech_corp");
        edge.Should().NotBeNull("edge should be created successfully");

        // Test traversal
        var traversalResult = await connector.ExecuteAsync("g.V('john').out('works_for')", new Dictionary<string, object>());
        traversalResult.Should().HaveCount(1, "traversal should return the company");

        var company = traversalResult.First();
        ((object)company).Should().NotBeNull("company should not be null");
    }

    [Fact]
    public async Task Debug_CheckQueryParsing()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Test individual query patterns
        
        // 1. Simple vertex creation
        var addVResult = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());
        addVResult.Should().HaveCount(1, "simple addV should work");

        // 2. Vertex with single property
        var addVWithPropResult = await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());
        addVWithPropResult.Should().HaveCount(1, "addV with property should work");

        // 3. Check vertex retrieval
        var vertices = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
        vertices.Should().HaveCount(2, "should have 2 vertices");

        // 4. Check specific vertex retrieval
        var specificVertex = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
        specificVertex.Should().HaveCount(2, "should have 2 person vertices");
    }
}