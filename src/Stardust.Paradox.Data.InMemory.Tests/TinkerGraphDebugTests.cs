using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Debug tests to identify issues with TinkerGraph query parsing and execution
/// </summary>
public class TinkerGraphDebugTests
{
    [Fact]
    public async Task SimpleVertexQuery_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts => opts.EnableDebugLogging = true);
        
        // Add a simple vertex
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        
        // Act
        var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
        
        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SimpleVertexByIdQuery_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts => opts.EnableDebugLogging = true);
        
        // Add a simple vertex
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        
        // Act
        var result = await connector.ExecuteAsync("g.V('john')", new Dictionary<string, object>());
        
        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.id).Should().Be("john");
    }

    [Fact]
    public async Task SimpleTraversal_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts => opts.EnableDebugLogging = true);
        
        // Setup simple graph
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        
        // Act - Simple out traversal
        var result = await connector.ExecuteAsync("g.V('john').out('works_for')", new Dictionary<string, object>());
        
        // Assert
        result.Should().HaveCount(1);
        var company = result.First();
        ((string)company.properties.name).Should().Be("Tech Corp");
    }

    [Fact]
    public async Task DatabaseDirectAccess_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Access database directly
        var vertex1 = connector.Database.AddVertex("person", "john");
        vertex1.SetProperty("name", "John");
        
        var vertex2 = connector.Database.AddVertex("company", "tech_corp");
        vertex2.SetProperty("name", "Tech Corp");
        
        var edge = connector.Database.AddEdge("works_for", "john", "tech_corp");
        
        // Act - Test direct database traversal methods
        var outVertices = connector.Database.GetOutVertices("john", "works_for");
        
        // Assert
        outVertices.Should().HaveCount(1);
        var company = outVertices.First();
        company.GetProperty<string>("name").Should().Be("Tech Corp");
    }

    [Fact]
    public async Task TinkerGraphParser_DirectTest()
    {
        // Arrange
        var database = new InMemoryGraphDatabase();
        var parser = new TinkerGraphQueryParser(database);
        
        // Add data directly
        var vertex = database.AddVertex("person", "john");
        vertex.SetProperty("name", "John");
        
        // Act - Test parser directly
        try
        {
            var result = parser.ParseAndExecute("g.V('john')", new Dictionary<string, object>());
            
            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
        }
        catch (System.Exception ex)
        {
            // This will help us see what's failing in the parser
            throw new System.Exception($"TinkerGraph parser failed: {ex.Message}", ex);
        }
    }
}