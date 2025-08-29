using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for parameterized queries
/// </summary>
public class ParameterizedQueryTests
{
    [Fact]
    public async Task ExecuteAsync_WithStringParameter_ShouldSubstituteCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', p0)", 
            new Dictionary<string, object> { { "p0", "John" } });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("John");
    }

    [Fact]
    public async Task ExecuteAsync_WithIntegerParameter_ShouldSubstituteCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('age', 30)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().has('age', p0)", 
            new Dictionary<string, object> { { "p0", 30 } });

        // Assert
        result.Should().HaveCount(1);
        ((int)result.First().properties.age).Should().Be(30);
    }

    [Fact]
    public async Task ExecuteAsync_WithBooleanParameter_ShouldSubstituteCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('active', true)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().has('active', p0)", 
            new Dictionary<string, object> { { "p0", true } });

        // Assert
        result.Should().HaveCount(1);
        ((bool)result.First().properties.active).Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullParameter_ShouldSubstituteCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person').property('optional', p0)", 
            new Dictionary<string, object> { { "p0", null } });

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleParameters_ShouldSubstituteAll()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person').property('name', p0).property('age', p1)", 
            new Dictionary<string, object> 
            { 
                { "p0", "John" },
                { "p1", 30 }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("John");
        ((int)result.First().properties.age).Should().Be(30);
    }

    [Fact]
    public async Task ExecuteAsync_WithParameterInDifferentPositions_ShouldSubstituteAll()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('age', 30)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('age', 25)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel(p0).has('age', p1)", 
            new Dictionary<string, object> 
            { 
                { "p0", "person" },
                { "p1", 30 }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("John");
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexParameterizedQuery_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupTestData(connector);

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().hasLabel(p0).has(p1, p2).out(p3).hasLabel(p4)", 
            new Dictionary<string, object> 
            { 
                { "p0", "person" },
                { "p1", "name" },
                { "p2", "John" },
                { "p3", "works_for" },
                { "p4", "company" }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("Tech Corp");
    }

    [Fact]
    public async Task ExecuteAsync_WithParameterInAggregation_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob').property('department', 'Sales')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', p0).count()", 
            new Dictionary<string, object> { { "p0", "Engineering" } });

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(2L);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepeatedParameter_ShouldSubstituteAllOccurrences()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', p0).has('name', p0)", 
            new Dictionary<string, object> { { "p0", "John" } });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("John");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnusedParameter_ShouldNotCauseError()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act & Assert
        var result = await connector.ExecuteAsync("g.V().count()", 
            new Dictionary<string, object> { { "p0", "unused" } });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingParameter_ShouldNotSubstitute()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person').property('name', p0)", 
            new Dictionary<string, object>());

        // Assert - The parameter should remain as literal text
        result.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("test string")]
    [InlineData("string with spaces")]
    [InlineData("string-with-dashes")]
    [InlineData("string_with_underscores")]
    [InlineData("string.with.dots")]
    [InlineData("string123with456numbers")]
    public async Task ExecuteAsync_WithVariousStringParameterValues_ShouldWork(string paramValue)
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person').property('name', p0)", 
            new Dictionary<string, object> { { "p0", paramValue } });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be(paramValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public async Task ExecuteAsync_WithVariousIntegerParameterValues_ShouldWork(int paramValue)
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person').property('value', p0)", 
            new Dictionary<string, object> { { "p0", paramValue } });

        // Assert
        result.Should().HaveCount(1);
        ((int)result.First().properties.value).Should().Be(paramValue);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(-2.7)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public async Task ExecuteAsync_WithVariousDoubleParameterValues_ShouldWork(double paramValue)
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.addV('person').property('value', p0)", 
            new Dictionary<string, object> { { "p0", paramValue } });

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First().properties.value).Should().Be(paramValue);
    }

    private static async Task SetupTestData(InMemoryGremlinLanguageConnector connector)
    {
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
    }
}