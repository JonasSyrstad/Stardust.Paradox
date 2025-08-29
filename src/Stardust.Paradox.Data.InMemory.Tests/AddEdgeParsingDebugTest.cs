using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Test to debug the specific addE parsing issue
/// </summary>
public class AddEdgeParsingDebugTest
{
    [Fact]
    public async Task DebugAddEdgeQuery_ShouldShowWhatHappens()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts => opts.EnableDebugLogging = true);
        
        // Create vertices first
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        
        Console.WriteLine("Before addE query:");
        Console.WriteLine($"Vertices: {connector.Database.GetAllVertices().Count()}");
        Console.WriteLine($"Edges: {connector.Database.GetAllEdges().Count()}");
        
        // Test the exact query that's failing
        var query = "g.V('john').addE('works_for').to(g.V('tech_corp'))";
        Console.WriteLine($"Executing query: {query}");
        
        var result = await connector.ExecuteAsync(query, new Dictionary<string, object>());
        
        Console.WriteLine("After addE query:");
        Console.WriteLine($"Vertices: {connector.Database.GetAllVertices().Count()}");
        Console.WriteLine($"Edges: {connector.Database.GetAllEdges().Count()}");
        Console.WriteLine($"Query result count: {result.Count()}");
        
        // Check if the problem is that john vertex exists
        var johnVertex = connector.Database.GetVertex("john");
        var techCorpVertex = connector.Database.GetVertex("tech_corp");
        
        Console.WriteLine($"John vertex exists: {johnVertex != null}");
        Console.WriteLine($"Tech Corp vertex exists: {techCorpVertex != null}");
        
        if (johnVertex != null)
        {
            Console.WriteLine($"John vertex ID: {johnVertex.Id}, Label: {johnVertex.Label}");
        }
        
        if (techCorpVertex != null)
        {
            Console.WriteLine($"Tech Corp vertex ID: {techCorpVertex.Id}, Label: {techCorpVertex.Label}");
        }
        
        // This test is just for debugging, so let's assert what we know
        johnVertex.Should().NotBeNull();
        techCorpVertex.Should().NotBeNull();
    }
}