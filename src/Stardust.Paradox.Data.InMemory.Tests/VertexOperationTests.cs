using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for vertex operations in the InMemory database
/// </summary>
public class VertexOperationTests
{
    [Fact]
    public async Task AddV_WithLabel_ShouldCreateVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.label).Should().Be("person");
        ((string)vertex.type).Should().Be("vertex");
        ((string)vertex.id).Should().NotBeNull();
    }

    [Fact]
    public async Task AddV_WithProperties_ShouldCreateVertexWithProperties()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync(
            "g.addV('person').property('name', 'John').property('age', 30)", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.label).Should().Be("person");
        ((string)vertex.properties.name).Should().Be("John");
        ((int)vertex.properties.age).Should().Be(30);
    }

    [Fact]
    public async Task V_WithoutArguments_ShouldReturnAllVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task V_WithId_ShouldReturnSpecificVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2').property('name', 'Jane')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p1')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.id).Should().Be("p1");
        ((string)vertex.properties.name).Should().Be("John");
    }

    [Fact]
    public async Task HasLabel_ShouldFilterByLabel()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'c1')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().label).Should().Be("person");
    }

    [Fact]
    public async Task Has_WithKeyValue_ShouldFilterByProperty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('age', 30)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('age', 25)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', 'John')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("John");
    }

    [Fact]
    public async Task HasId_WithMultipleIds_ShouldReturnMatchingVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p3')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasId('p1', 'p3')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
        var ids = result.Select(r => ((string)r.id)).ToList();
        ids.Should().Contain("p1");
        ids.Should().Contain("p3");
    }

    [Fact]
    public async Task Property_ShouldUpdateVertexProperty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", new Dictionary<string, object>());

        // Act
        await connector.ExecuteAsync("g.V('p1').property('name', 'John Doe')", new Dictionary<string, object>());
        var result = await connector.ExecuteAsync("g.V('p1')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("John Doe");
    }

    [Fact]
    public async Task Drop_ShouldRemoveVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", new Dictionary<string, object>());

        // Act
        await connector.ExecuteAsync("g.V('p1').drop()", new Dictionary<string, object>());
        var result = await connector.ExecuteAsync("g.V('p1')", new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Values_ShouldReturnPropertyValues()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('age', 30)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('name')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("John");
    }

    [Fact]
    public async Task ValueMap_ShouldReturnPropertyMap()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('age', 30)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').valueMap()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var valueMap = result.First() as Dictionary<string, object>;
        valueMap.Should().NotBeNull();
        valueMap!["name"].Should().Be("John");
        valueMap["age"].Should().Be(30);
    }

    [Fact]
    public async Task ElementMap_ShouldReturnCompleteElementInfo()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V('p1').elementMap()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var elementMap = result.First() as Dictionary<string, object>;
        elementMap.Should().NotBeNull();
        elementMap!["id"].Should().Be("p1");
        elementMap["label"].Should().Be("person");
        elementMap["type"].Should().Be("vertex");
        elementMap["name"].Should().Be("John");
    }

    [Fact]
    public async Task Dedup_ShouldRemoveDuplicates()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'p2').property('name', 'John')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('name').dedup()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("John");
    }
}