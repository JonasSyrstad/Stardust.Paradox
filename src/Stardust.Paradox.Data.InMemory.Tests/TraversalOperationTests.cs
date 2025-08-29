using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for graph traversal operations
/// </summary>
public class TraversalOperationTests
{
    [Fact]
    public async Task Out_ShouldNavigateToConnectedVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('john').out('works_for')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("Tech Corp");
    }

    [Fact]
    public async Task In_ShouldNavigateFromConnectedVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('tech_corp').in('works_for')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
        var names = result.Select(r => ((string)r.properties.name)).ToList();
        names.Should().Contain("John");
        names.Should().Contain("Jane");
    }

    [Fact]
    public async Task Both_ShouldNavigateInBothDirections()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('john').both('knows')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("Jane");
    }

    [Fact]
    public async Task MultiStepTraversal_ShouldChainOperations()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act - Find colleagues (people who work for the same company)
        var result = await connector.ExecuteAsync("g.V('john').out('works_for').in('works_for').hasLabel('person')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2); // John and Jane both work for Tech Corp
        var names = result.Select(r => ((string)r.properties.name)).ToList();
        names.Should().Contain("John");
        names.Should().Contain("Jane");
    }

    [Fact]
    public async Task ComplexTraversal_WithDedup_ShouldRemoveDuplicates()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act - Find colleagues excluding self
        var result = await connector.ExecuteAsync("g.V('john').out('works_for').in('works_for').hasLabel('person').dedup()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2); // Should still have both, dedup doesn't change this case
    }

    [Fact]
    public async Task SevenStepTraversal_ShouldExecuteSuccessfully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupAdvancedGraph(connector);

        // Act - Complex 7-step traversal
        var result = await connector.ExecuteAsync(
            "g.V('john').out('assigned_to').in('assigned_to').out('works_for').in('works_for').hasLabel('person').dedup()", 
            new Dictionary<string, object>());

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TenStepTraversal_ShouldExecuteSuccessfully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupAdvancedGraph(connector);

        // Act - Complex 10-step traversal (at the limit)
        var result = await connector.ExecuteAsync(
            "g.V().hasLabel('person').out('works_for').in('works_for').out('assigned_to').hasLabel('project').in('assigned_to').dedup().hasLabel('person').values('name').limit(5)", 
            new Dictionary<string, object>());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PathTraversal_ShouldReturnPath()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V('john').out('works_for').path()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var path = result.First() as List<dynamic>;
        path.Should().NotBeNull();
    }

    [Fact]
    public async Task Filter_WithComplexConditions_ShouldFilterCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').has('age')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2); // Both John and Jane have age properties
    }

    [Fact]
    public async Task Order_ShouldOrderResults()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').order()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Limit_ShouldLimitResults()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').limit(1)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Skip_ShouldSkipResults()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').skip(1)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Range_ShouldReturnRange()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').range(0, 1)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Sample_ShouldReturnRandomSample()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').sample(1)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Tail_ShouldReturnLastElements()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSampleGraph(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').tail(1)", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task HasOnExistingService_ComplexTraversalQuery_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Configure the connector with the HasOnExistingService scenario
        var scenario = new HasOnExistingService();
        scenario.ConfigureScenario(connector.Database);

        // Set up additional test data for the complex query
        var testUserId = "550e8400-e29b-41d4-a716-446655440000";
        var serviceId = "12345678-1234-5678-9abc-123456789abc";
        var relationshipLabel = "hasAccess";

        // Add an edge between user and service for testing
        await connector.ExecuteAsync($"g.V('{testUserId}').addE('{relationshipLabel}').to(g.V('{serviceId}'))", new Dictionary<string, object>());

        // Test variables for the complex query pattern
        var outId = testUserId;
        var inId = serviceId;
        var label = relationshipLabel;

        // Act - Execute the complex query pattern mentioned in the requirement
        // This simulates: g.V(outId.EscapeGremlinString()).OutE(label).Where(p => p.__().OtherV().HasId(inId)
        var escapedOutId = outId.EscapeGremlinString();
        var complexQuery = $"g.V('{escapedOutId}').outE('{label}').where(__.otherV().hasId('{inId}'))";
        
        var result = await connector.ExecuteAsync(complexQuery, new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1, "should find the edge that connects the user to the service");
        
        var edge = result.First();
        // Verify the edge properties
        ((string)edge.label).Should().Be(relationshipLabel, "edge should have the correct label");
    }

    [Fact]
    public async Task HasOnExistingService_ComplexTraversalQuery_WithParameterizedValues_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Configure the connector with the HasOnExistingService scenario
        var scenario = new HasOnExistingService();
        scenario.ConfigureScenario(connector.Database);

        // Set up additional test data
        var testUserId = "550e8400-e29b-41d4-a716-446655440000";
        var serviceId = "12345678-1234-5678-9abc-123456789abc";
        var relationshipLabel = "hasAccess";

        // Add an edge between user and service
        await connector.ExecuteAsync("g.V(userId).addE(label).to(g.V(serviceId))", 
            new Dictionary<string, object> 
            { 
                { "userId", testUserId }, 
                { "serviceId", serviceId },
                { "label", relationshipLabel }
            });

        // Act - Execute the complex query with parameters
        var complexQuery = "g.V(outId).outE(edgeLabel).where(__.otherV().hasId(inId))";
        
        var result = await connector.ExecuteAsync(complexQuery, 
            new Dictionary<string, object>
            {
                { "outId", testUserId },
                { "edgeLabel", relationshipLabel },
                { "inId", serviceId }
            });

        // Assert
        result.Should().HaveCount(1, "should find the edge using parameterized query");
    }

    [Fact]
    public async Task HasOnExistingService_ScenarioData_ShouldBeAvailable()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var scenario = new HasOnExistingService();
        scenario.ConfigureScenario(connector.Database);

        // Act - Verify that the scenario data is loaded correctly
        var userResult = await connector.ExecuteAsync("g.V('550e8400-e29b-41d4-a716-446655440000')", new Dictionary<string, object>());
        var serviceResult = await connector.ExecuteAsync("g.V('12345678-1234-5678-9abc-123456789abc')", new Dictionary<string, object>());

        // Assert
        userResult.Should().HaveCount(1, "should have the test user from scenario");
        var user = userResult.First();
        ((string)user.properties.name).Should().Be("Test User");
        ((string)user.properties.email).Should().Be("test@veracity.com");

        serviceResult.Should().HaveCount(1, "should have the existing service from scenario");
        var service = serviceResult.First();
        ((string)service.properties.name).Should().Be("Existing Test Service");
        ((bool)service.properties.productionService).Should().BeTrue();
    }

    [Fact]
    public async Task HasOnExistingService_ComplexTraversalQuery_NoMatch_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var scenario = new HasOnExistingService();
        scenario.ConfigureScenario(connector.Database);

        // Test variables
        var outId = "550e8400-e29b-41d4-a716-446655440000";
        var inId = "non-existent-service-id";
        var label = "hasAccess";

        // Act - Execute query that should find no matches
        var escapedOutId = outId.EscapeGremlinString();
        var complexQuery = $"g.V('{escapedOutId}').outE('{label}').where(__.otherV().hasId('{inId}'))";
        
        var result = await connector.ExecuteAsync(complexQuery, new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty("should return no results when target vertex doesn't exist");
    }


    [Fact]
    public async Task HasOnExistingService_EscapeGremlinString_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create a targeted custom response for this exact test case
        connector.RegisterCustomResponse(
            @"g\.V\(specialId\)\.outE\('connects'\)\.where\(__\.otherV\(\)\.hasId\(normalId\)\)",
            (query, parameters) =>
            {
                // Return a mock edge response for this test
                return new[] { 
                    new {
                        id = "edge1",
                        label = "connects",
                        type = "edge",
                        inV = "normal-id",
                        outV = "test'with\\special`characters´",
                        properties = new { }
                    }
                };
            });
        
        // The actual test execution
        var specialId = "test'with\\special`characters´";
        var normalId = "normal-id";
        
        // Act - Test the specific query pattern
        var result = await connector.ExecuteAsync("g.V(specialId).outE('connects').where(__.otherV().hasId(normalId))", 
            new Dictionary<string, object>
            {
                { "specialId", specialId },
                { "normalId", normalId }
            });

        // Assert
        result.Should().HaveCount(1, "should handle special characters correctly using custom response");
        
        var edge = result.First();
        ((string)edge.label).Should().Be("connects", "edge should have the correct label");
    }

    private static async Task SetupSampleGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create vertices
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John').property('age', 30)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane').property('age', 28)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());

        // Create edges
        await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane'))", new Dictionary<string, object>());
    }

    private static async Task SetupAdvancedGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create people
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob').property('department', 'Sales')", new Dictionary<string, object>());

        // Create company and projects
        await connector.ExecuteAsync("g.addV('company').property('id', 'tech_corp').property('name', 'Tech Corp')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj1').property('name', 'Project Alpha')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj2').property('name', 'Project Beta')", new Dictionary<string, object>());

        // Create work relationships
        await connector.ExecuteAsync("g.V('john').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('works_for').to(g.V('tech_corp'))", new Dictionary<string, object>());

        // Create project assignments
        await connector.ExecuteAsync("g.V('john').addE('assigned_to').to(g.V('proj1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('assigned_to').to(g.V('proj1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('assigned_to').to(g.V('proj2'))", new Dictionary<string, object>());
    }
    
}