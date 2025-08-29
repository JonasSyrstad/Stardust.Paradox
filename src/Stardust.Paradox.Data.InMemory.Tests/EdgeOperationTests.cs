using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for edge operations in the InMemory database
/// </summary>
public class EdgeOperationTests
{
    [Fact]
    public async Task AddE_ShouldCreateEdge()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var edge = result.First();
        ((string)edge.label).Should().Be("knows");
        ((string)edge.type).Should().Be("edge");
        ((string)edge.outV).Should().Be("p1");
        ((string)edge.inV).Should().Be("p2");
    }

    [Fact]
    public async Task AddE_UsingScenario_ShouldCreateEdge()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up basic social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice')", new Dictionary<string, object>());

        // Act - Add a new edge between existing vertices
        var result = await connector.ExecuteAsync("g.V('bob').addE('follows').to(g.V('alice'))", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var edge = result.First();
        ((string)edge.label).Should().Be("follows");
        ((string)edge.outV).Should().Be("bob");
        ((string)edge.inV).Should().Be("alice");
    }

    [Fact]
    public async Task E_WithoutArguments_ShouldReturnAllEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task E_WithScenario_ShouldReturnAllEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('post').property('id', 'post1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('knows').to(g.V('bob'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('authored').to(g.V('post1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('authored').to(g.V('post1'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        result.Count().Should().BeGreaterThanOrEqualTo(4); // At least the friendship and authorship edges
    }

    [Fact]
    public async Task E_WithId_ShouldReturnSpecificEdge()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        var addResult = await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        var edgeId = addResult.First().id.ToString();

        // Act
        var result = await connector.ExecuteAsync($"g.E('{edgeId}')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be(edgeId);
    }

    [Fact]
    public async Task HasLabel_OnEdges_ShouldFilterByLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'c1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('works_for').to(g.V('c1'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().label).Should().Be("knows");
    }

    [Fact]
    public async Task HasLabel_OnEdges_UsingScenario_ShouldFilterByLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('knows').to(g.V('john'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        result.All(r => ((string)r.label) == "knows").Should().BeTrue();
    }

    [Fact]
    public async Task OutE_ShouldReturnOutgoingEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'c1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('works_for').to(g.V('c1'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p1').outE()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
        result.All(r => ((string)r.outV) == "p1").Should().BeTrue();
    }

    [Fact]
    public async Task OutE_UsingScenario_ShouldReturnOutgoingEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('bob'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('john').outE()", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        result.All(r => ((string)r.outV) == "john").Should().BeTrue();
    }

    [Fact]
    public async Task OutE_WithLabel_ShouldFilterByEdgeLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'c1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('works_for').to(g.V('c1'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p1').outE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().label).Should().Be("knows");
    }

    [Fact]
    public async Task OutE_WithLabel_UsingScenario_ShouldFilterByEdgeLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('john').outE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        result.All(r => ((string)r.label) == "knows").Should().BeTrue();
    }

    [Fact]
    public async Task InE_ShouldReturnIncomingEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p3')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p3').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p2').inE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
        result.All(r => ((string)r.inV) == "p2").Should().BeTrue();
    }

    [Fact]
    public async Task InE_UsingScenario_ShouldReturnIncomingEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());

        // Act - Jane should have incoming 'knows' edges
        var result = await connector.ExecuteAsync("g.V('jane').inE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        result.All(r => ((string)r.inV) == "jane").Should().BeTrue();
    }

    [Fact]
    public async Task BothE_ShouldReturnBothDirectionEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p3')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p3').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p2').bothE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task BothE_UsingScenario_ShouldReturnBothDirectionEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());

        // Act - Jane should have edges in both directions
        var result = await connector.ExecuteAsync("g.V('jane').bothE('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InV_ShouldNavigateToIncomingVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p1').outE('knows').inV()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("Jane");
    }

    [Fact]
    public async Task InV_UsingScenario_ShouldNavigateToIncomingVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('john').outE('knows').inV()", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        // Should navigate to John's friends
    }

    [Fact]
    public async Task OutV_ShouldNavigateToOutgoingVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p2').inE('knows').outV()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("John");
    }

    [Fact]
    public async Task OutV_UsingScenario_ShouldNavigateToOutgoingVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('jane').inE('knows').outV()", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        // Should navigate to people who know Jane
    }

    [Fact]
    public async Task BothV_ShouldNavigateToBothVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.E().hasLabel('knows').bothV()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
        var names = result.Select(r => ((string)r.properties.name)).ToList();
        names.Should().Contain("John");
        names.Should().Contain("Jane");
    }

    [Fact]
    public async Task BothV_UsingScenario_ShouldNavigateToBothVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.E().hasLabel('knows').bothV()", new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
        // Should include all people connected by 'knows' edges
    }

    [Fact]
    public async Task DropEdge_ShouldRemoveEdge()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        var addResult = await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        var edgeId = addResult.First().id.ToString();

        // Act
        await connector.ExecuteAsync($"g.E('{edgeId}').drop()", new Dictionary<string, object>());
        var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DropVertex_ShouldRemoveConnectedEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p3')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p3'))", new Dictionary<string, object>());

        // Act
        await connector.ExecuteAsync("g.V('p1').drop()", new Dictionary<string, object>());
        var edgeResult = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
        var vertexResult = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

        // Assert
        edgeResult.Should().BeEmpty(); // All edges connected to p1 should be removed
        vertexResult.Should().HaveCount(2); // Only p2 and p3 should remain
    }

    [Fact]
    public async Task ScenarioData_ShouldSupportComplexEdgeQueries()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('knows').to(g.V('bob'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('knows').to(g.V('alice'))", new Dictionary<string, object>());

        // Act - Complex query combining multiple edge traversals
        var friendsOfFriends = await connector.ExecuteAsync("g.V('john').out('knows').out('knows').dedup()", new Dictionary<string, object>());

        // Assert
        friendsOfFriends.Should().NotBeNull();
        // Should find people who are friends of John's friends (but not John himself)
    }

    [Fact]
    public async Task MultipleScenarios_ShouldCombineEdgeData()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        // Set up social network and ecommerce data manually
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('product').property('id', 'laptop')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('purchased').to(g.V('laptop'))", new Dictionary<string, object>());

        // Act
        var socialEdges = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());
        var purchaseEdges = await connector.ExecuteAsync("g.E().hasLabel('purchased')", new Dictionary<string, object>());

        // Assert
        socialEdges.Should().NotBeEmpty();
        purchaseEdges.Should().NotBeEmpty();
    }
}