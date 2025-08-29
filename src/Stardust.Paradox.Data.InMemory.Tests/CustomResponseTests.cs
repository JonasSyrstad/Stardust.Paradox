using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for custom response functionality
/// </summary>
public class CustomResponseTests
{
    [Fact]
    public void RegisterCustomResponse_ShouldAllowCustomQueryHandling()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var customData = new[] 
        { 
            new { name = "Custom1", value = 100 },
            new { name = "Custom2", value = 200 }
        };

        // Act
        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('custom'\)", 
            (query, parameters) => customData);

        // Assert
        var result = connector.Database.GetCustomResponse("g.V().hasLabel('custom')");
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomResponse_ShouldReturnCustomData()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var customData = new dynamic[] 
        { 
            new { id = "custom1", label = "custom", name = "Test Data" }
        };

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('special'\)", 
            (query, parameters) => customData);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('special')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().name).Should().Be("Test Data");
    }

    [Fact]
    public async Task ExecuteAsync_WithParameterizedCustomResponse_ShouldPassParameters()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        string capturedParameter = null;

        connector.RegisterCustomResponse(@"g\.V\(\)\.has\('name', .+\)", 
            (query, parameters) => 
            {
                if (parameters.ContainsKey("p0"))
                {
                    capturedParameter = parameters["p0"].ToString();
                }
                return new dynamic[] { new { name = capturedParameter } };
            });

        // Act
        var result = await connector.ExecuteAsync("g.V().has('name', p0)", 
            new Dictionary<string, object> { { "p0", "TestName" } });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().name).Should().Be("TestName");
        capturedParameter.Should().Be("TestName");
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexCustomResponse_ShouldHandleComplexQueries()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var aggregatedData = new Dictionary<string, object>
        {
            { "Engineering", new[] { "John", "Jane" } },
            { "Sales", new[] { "Bob" } },
            { "Marketing", new[] { "Alice" } }
        };

        connector.RegisterCustomResponse(@"g\.V\(\)\.group\(\)\.by\('department'\)", 
            (query, parameters) => new dynamic[] { aggregatedData });

        // Act
        var result = await connector.ExecuteAsync("g.V().group().by('department')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groupResult = result.First() as Dictionary<string, object>;
        groupResult.Should().NotBeNull();
        groupResult!.Should().ContainKey("Engineering");
        groupResult.Should().ContainKey("Sales");
        groupResult.Should().ContainKey("Marketing");
    }

    [Fact]
    public async Task ExecuteAsync_WithStatisticalCustomResponse_ShouldReturnStatistics()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)\.values\('salary'\)\.sum\(\)", 
            (query, parameters) => new dynamic[] { 250000.0 });

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)\.values\('salary'\)\.mean\(\)", 
            (query, parameters) => new dynamic[] { 83333.33 });

        // Act
        var sumResult = await connector.ExecuteAsync("g.V().hasLabel('person').values('salary').sum()", new Dictionary<string, object>());
        var meanResult = await connector.ExecuteAsync("g.V().hasLabel('person').values('salary').mean()", new Dictionary<string, object>());

        // Assert
        sumResult.Should().HaveCount(1);
        ((double)sumResult.First()).Should().Be(250000.0);

        meanResult.Should().HaveCount(1);
        ((double)meanResult.First()).Should().Be(83333.33);
    }

    [Fact]
    public async Task ExecuteAsync_WithPathCustomResponse_ShouldReturnPaths()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var pathData = new[]
        {
            new[] { "John", "Tech Corp", "Project Alpha" },
            new[] { "Jane", "Tech Corp", "Project Beta" }
        };

        connector.RegisterCustomResponse(@"g\.V\(\)\.out\('works_for'\)\.in\('funds'\)\.path\(\)", 
            (query, parameters) => pathData);

        // Act
        var result = await connector.ExecuteAsync("g.V().out('works_for').in('funds').path()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithConditionalCustomResponse_ShouldChooseCorrectResponse()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)\.has\('active', true\)", 
            (query, parameters) => new dynamic[] 
            { 
                new { name = "Active User 1" },
                new { name = "Active User 2" }
            });

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)\.has\('active', false\)", 
            (query, parameters) => new dynamic[] 
            { 
                new { name = "Inactive User 1" }
            });

        // Act
        var activeResult = await connector.ExecuteAsync("g.V().hasLabel('person').has('active', true)", new Dictionary<string, object>());
        var inactiveResult = await connector.ExecuteAsync("g.V().hasLabel('person').has('active', false)", new Dictionary<string, object>());

        // Assert
        activeResult.Should().HaveCount(2);
        inactiveResult.Should().HaveCount(1);
        ((string)activeResult.First().name).Should().Contain("Active");
        ((string)inactiveResult.First().name).Should().Contain("Inactive");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleCustomResponses_ShouldUseCorrectPattern()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        connector.RegisterCustomResponse(@"pattern1", 
            (query, parameters) => new dynamic[] { "Response 1" });

        connector.RegisterCustomResponse(@"pattern2", 
            (query, parameters) => new dynamic[] { "Response 2" });

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('test'\)", 
            (query, parameters) => new dynamic[] { "Test Response" });

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('test')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First()).Should().Be("Test Response");
    }

    [Fact]
    public async Task ExecuteAsync_WithFallbackToNormalExecution_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());

        // Register custom response for different pattern
        connector.RegisterCustomResponse(@"custom_pattern", 
            (query, parameters) => new dynamic[] { "Custom" });

        // Act - This should fall back to normal execution
        var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().label).Should().Be("person");
    }

    [Fact]
    public async Task ExecuteAsync_WithLambdaCustomResponse_ShouldExecuteLambda()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var callCount = 0;

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('counter'\)", 
            (query, parameters) => 
            {
                callCount++;
                return new dynamic[] { callCount };
            });

        // Act
        var result1 = await connector.ExecuteAsync("g.V().hasLabel('counter')", new Dictionary<string, object>());
        var result2 = await connector.ExecuteAsync("g.V().hasLabel('counter')", new Dictionary<string, object>());

        // Assert
        if (result1.Any())
        {
            ((int)result1.First()).Should().Be(1);
        }
        if (result2.Any())
        {
            ((int)result2.First()).Should().Be(2);
        }
        callCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCustomResponse_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('empty'\)", 
            (query, parameters) => new dynamic[0]);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('empty')", new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithExceptionInCustomResponse_ShouldFallback()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());

        connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)", 
            (query, parameters) => throw new InvalidOperationException("Custom response error"));

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());

        // Assert - Should fallback to normal execution
        result.Should().HaveCount(1);
        ((string)result.First().label).Should().Be("person");
    }
}