using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Simple tests to debug specific parsing issues
/// </summary>
public class TinkerGraphParsingDebugTests
{
    [Fact]
    public async Task AddE_WithSimplePattern_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts => opts.EnableDebugLogging = true);
        
        // Create vertices first using the direct database API to eliminate addV parsing issues
        connector.Database.AddVertex("person", new Dictionary<string, object> { ["name"] = "John" }, "john");
        connector.Database.AddVertex("company", new Dictionary<string, object> { ["name"] = "Tech Corp" }, "tech_corp");

        // Act - Try simple edge creation
        var result = await connector.ExecuteAsync("g.addE('works_for').from(g.V('john')).to(g.V('tech_corp'))", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddE_UsingDatabase_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create vertices and edge using direct database API
        connector.Database.AddVertex("person", new Dictionary<string, object> { ["name"] = "John" }, "john");
        connector.Database.AddVertex("company", new Dictionary<string, object> { ["name"] = "Tech Corp" }, "tech_corp");
        var edge = connector.Database.AddEdge("works_for", "john", "tech_corp");

        // Act - Test if traversal works with direct database
        var outVertices = connector.Database.GetOutVertices("john", "works_for");

        // Assert
        outVertices.Should().HaveCount(1);
        outVertices.First().GetProperty<string>("name").Should().Be("Tech Corp");
    }

    [Fact]
    public async Task SimpleOut_AfterDirectEdgeCreation_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts => opts.EnableDebugLogging = true);
        
        // Create vertices and edge using direct database API
        connector.Database.AddVertex("person", new Dictionary<string, object> { ["name"] = "John" }, "john");
        connector.Database.AddVertex("company", new Dictionary<string, object> { ["name"] = "Tech Corp" }, "tech_corp");
        connector.Database.AddEdge("works_for", "john", "tech_corp");

        // Act - Test if query-based traversal works
        var result = await connector.ExecuteAsync("g.V('john').out('works_for')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var company = result.First();
        ((string)company.properties.name).Should().Be("Tech Corp");
    }

    [Fact]
    public async Task TinkerGraphParser_DirectTest_ShouldShowError()
    {
        // Arrange
        var database = new InMemoryGraphDatabase();
        var parser = new TinkerGraphQueryParser(database);
        
        // Add vertices directly
        database.AddVertex("person", new Dictionary<string, object> { ["name"] = "John" }, "john");
        database.AddVertex("company", new Dictionary<string, object> { ["name"] = "Tech Corp" }, "tech_corp");
        
        // Act & Assert - Test if the parser can handle complex addE queries
        try
        {
            var result = parser.ParseAndExecute("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
            result.Should().HaveCount(1);
        }
        catch (System.Exception ex)
        {
            // This will help us see what's failing in the parser
            throw new System.Exception($"TinkerGraph parser failed on addE: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task CheckDatabase_StateAfterAddE_ShouldShowEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts => opts.EnableDebugLogging = true);
        
        // Setup using queries
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        
        // Act - Check database state directly
        var vertices = connector.Database.GetAllVertices().ToList();
        var edges = connector.Database.GetAllEdges().ToList();
        var outVertices = connector.Database.GetOutVertices("john", "works_for").ToList();

        // Assert
        vertices.Should().HaveCount(2);
        edges.Should().HaveCountGreaterOrEqualTo(0); // Let's see what we actually get
        
        // This will help us understand what's happening
        Console.WriteLine($"Vertices: {vertices.Count}");
        Console.WriteLine($"Edges: {edges.Count}");
        Console.WriteLine($"Out vertices from john: {outVertices.Count}");
        
        foreach (var vertex in vertices)
        {
            Console.WriteLine($"Vertex: {vertex.Id} - {vertex.Label} - {vertex.GetProperty<string>("name")}");
        }
        
        foreach (var edge in edges)
        {
            Console.WriteLine($"Edge: {edge.Id} - {edge.Label} - {edge.OutVertexId} -> {edge.InVertexId}");
        }
    }
}