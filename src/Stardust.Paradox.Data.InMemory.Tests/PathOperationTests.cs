using Stardust.Paradox.Data.InMemory;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for the path() step functionality
/// </summary>
public class PathOperationTests
{
    [Fact]
    public async Task Path_SingleVertex_ShouldReturnPathWithOneElement()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        
        // Act
        var pathResult = await connector.ExecuteAsync("g.V('john').path()", new Dictionary<string, object>());
        
        // Assert
        pathResult.Should().HaveCount(1);
        var path = pathResult.First() as List<dynamic>;
        path.Should().NotBeNull("The path should be returned as a List<dynamic>");
        path.Should().HaveCount(1, "Single vertex path should have one element");
        
        // Check that the path contains the john vertex
        var pathElement = path[0];
        ((string)pathElement.id).Should().Be("john");
        ((string)pathElement.label).Should().Be("person");
    }
    
    [Fact]
    public async Task Path_TraversalWithOutStep_ShouldReturnFullPath()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        
        // Act
        var pathResult = await connector.ExecuteAsync("g.V('john').out('works_for').path()", new Dictionary<string, object>());
        
        // Assert
        pathResult.Should().HaveCount(1);
        var path = pathResult.First() as List<dynamic>;
        path.Should().NotBeNull("The path should be returned as a List<dynamic>");
        path.Should().HaveCount(2, "Traversal path should show both start and end vertices");
        
        // Check the path elements
        var startVertex = path[0];
        var endVertex = path[1];
        
        ((string)startVertex.id).Should().Be("john");
        ((string)startVertex.label).Should().Be("person");
        
        ((string)endVertex.id).Should().Be("tech_corp");
        ((string)endVertex.label).Should().Be("company");
    }

    [Fact]
    public async Task Path_MultipleStepTraversal_ShouldShowFullPath()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('knows').to(g.V('bob'))", new Dictionary<string, object>());
        
        // Act - traverse through multiple vertices
        var pathResult = await connector.ExecuteAsync("g.V('john').out('knows').out('knows').path()", new Dictionary<string, object>());
        
        // Assert
        pathResult.Should().HaveCount(1);
        var path = pathResult.First() as List<dynamic>;
        path.Should().NotBeNull();
        path.Should().HaveCount(3, "Multi-step path should show all vertices visited");
        
        // Check the path elements
        ((string)path[0].id).Should().Be("john");
        ((string)path[1].id).Should().Be("jane");
        ((string)path[2].id).Should().Be("bob");
    }
}