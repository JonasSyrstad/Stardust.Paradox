using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Basic tests for TinkerGraph query parsing and execution functionality
/// </summary>
public class TinkerGraphBasicTests
{
    [Fact]
    public async Task SimpleVertexQuery_ShouldReturnAllVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        
        // Act
        var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
        
        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task VertexByIdQuery_ShouldReturnSpecificVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        
        // Act
        var result = await connector.ExecuteAsync("g.V('john')", new Dictionary<string, object>());
        
        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.id).Should().Be("john");
    }

    [Fact]
    public async Task SimpleOutTraversal_ShouldNavigateToConnectedVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        
        // Act
        var result = await connector.ExecuteAsync("g.V('john').out('works_for')", new Dictionary<string, object>());
        
        // Assert
        result.Should().HaveCount(1);
        var company = result.First();
        ((string)company.properties.name).Should().Be("Tech Corp");
    }

    [Fact]
    public async Task DatabaseDirectAccess_ShouldSupportDirectOperations()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create data using direct database access
        var vertex1 = connector.Database.AddVertex("person", "john");
        vertex1.SetProperty("name", "John");
        
        var vertex2 = connector.Database.AddVertex("company", "tech_corp");
        vertex2.SetProperty("name", "Tech Corp");
        
        var edge = connector.Database.AddEdge("works_for", "john", "tech_corp");
        
        // Act
        var outVertices = connector.Database.GetOutVertices("john", "works_for");
        
        // Assert
        outVertices.Should().HaveCount(1);
        var company = outVertices.First();
        company.GetProperty<string>("name").Should().Be("Tech Corp");
    }

    [Fact]
    public async Task TinkerGraphParser_ShouldParseBasicQueries()
    {
        // Arrange
        var database = new InMemoryGraphDatabase();
        var parser = new TinkerGraphQueryParser(database);
        var vertex = database.AddVertex("person", "john");
        vertex.SetProperty("name", "John");
        
        // Act
        var result = parser.ParseAndExecute("g.V('john')", new Dictionary<string, object>());
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var returnedVertex = result.First();
        ((string)returnedVertex.id).Should().Be("john");
    }
}