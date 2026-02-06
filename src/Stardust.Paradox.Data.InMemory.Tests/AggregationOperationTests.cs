using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for aggregation operations
/// </summary>
public class AggregationOperationTests
{
    [Fact]
    public async Task Count_ShouldReturnCorrectCount()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').count()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(3L);
    }

    [Fact]
    public async Task Sum_ShouldCalculateCorrectSum()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('age').sum()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(93L); // 30 + 28 + 35
    }

    [Fact]
    public async Task Max_ShouldReturnMaximumValue()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('age').max()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(35.0);
    }

    [Fact]
    public async Task Min_ShouldReturnMinimumValue()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('age').min()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(28.0);
    }

    [Fact]
    public async Task Mean_ShouldCalculateAverage()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('age').mean()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(31.0); // (30 + 28 + 35) / 3
    }

    [Fact]
    public async Task Fold_ShouldCollectToList()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('name').fold()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var list = result.First() as List<dynamic>;
        list.Should().NotBeNull();
        list.Should().HaveCount(3);
    }

    [Fact]
    public async Task Unfold_ShouldExpandList()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('age').fold().unfold()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Group_ShouldGroupElements()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').group()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groups = result.First() as Dictionary<string, List<dynamic>>;
        groups.Should().NotBeNull();
    }

    [Fact]
    public async Task GroupCount_ShouldCountGroups()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupGroupTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').groupCount().by('department')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groupCounts = result.First() as Dictionary<string, long>;
        groupCounts.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexAggregation_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSalaryTestData(connector);

        // Act - Calculate total salary of engineers
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', 'Engineering').values('salary').sum()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(165000L); // 75000 + 90000
    }

    [Fact]
    public async Task MultiStepAggregation_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupAdvancedAggregationData(connector);

        // Act - Find average salary of people working on active projects
        var result = await connector.ExecuteAsync("g.V().hasLabel('project').has('status', 'active').in('assigned_to').values('salary').mean()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EmptyAggregation_ShouldHandleGracefully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('nonexistent').count()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(0L);
    }

    [Fact]
    public async Task EmptyMax_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('nonexistent').values('age').max()", new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyMin_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('nonexistent').values('age').min()", new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty();
    }

    // ===== NEW ENHANCED AGGREGATION TESTS =====

    [Fact]
    public async Task Count_WithTraversal_ShouldReturnCorrectCount()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupAdvancedAggregationData(connector);

        // Act - Count people connected to active projects
        var result = await connector.ExecuteAsync("g.V().hasLabel('project').has('status', 'active').in('assigned_to').count()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(2L); // john and jane
    }

    [Fact]
    public async Task Sum_WithFiltering_ShouldCalculateFilteredSum()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSalaryTestData(connector);

        // Act - Sum salaries of people in Sales department
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', 'Sales').values('salary').sum()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(65000L); // Only Bob in Sales
    }

    [Fact]
    public async Task Mean_WithEmptyResult_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('nonexistent').values('age').mean()", new Dictionary<string, object>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Max_WithStringValues_ShouldHandleNonNumericGracefully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act - Try to get max of string names (should handle gracefully)
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('name').max()", new Dictionary<string, object>());

        // Assert - Should either return empty or handle non-numeric values appropriately
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Min_WithMixedDataTypes_ShouldHandleGracefully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('test').property('mixed', '100')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('test').property('mixed', 200)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('test').property('mixed', 'text')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('test').values('mixed').min()", new Dictionary<string, object>());

        // Assert - Should handle mixed data types gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Fold_WithEmptyResult_ShouldReturnEmptyList()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('nonexistent').values('name').fold()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var list = result.First() as List<dynamic>;
        list.Should().NotBeNull();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Unfold_WithNestedFolds_ShouldFlattenCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act - Test nested fold/unfold operations
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('name').fold().unfold()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(3);
        var names = result.Select(r => r.ToString()).ToList();
        names.Should().Contain("John");
        names.Should().Contain("Jane");
        names.Should().Contain("Bob");
    }

    [Fact]
    public async Task GroupCount_WithoutBy_ShouldGroupByVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').groupCount()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groupCounts = result.First() as Dictionary<string, long>;
        groupCounts.Should().NotBeNull();
        groupCounts.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Group_WithProjection_ShouldGroupCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupGroupTestData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').group().by('department')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groups = result.First() as Dictionary<string, List<dynamic>>;
        groups.Should().NotBeNull();
        groups.Should().ContainKey("Engineering");
        groups.Should().ContainKey("Sales");
        groups.Should().ContainKey("Marketing");
    }

    [Fact]
    public async Task AggregateCount_OnEdges_ShouldCountEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupAdvancedAggregationData(connector);

        // Act
        var result = await connector.ExecuteAsync("g.E().hasLabel('assigned_to').count()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(3L); // john->proj1, jane->proj1, bob->proj2
    }

    [Fact]
    public async Task Sum_WithDecimalValues_ShouldHandleDecimals()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('product').property('price', 19.99)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('product').property('price', 29.95)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('product').property('price', 15.50)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('product').values('price').sum()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((decimal)result.First()).Should().BeApproximately(65.44m, 0.01m);
    }

    [Fact]
    public async Task Mean_WithSingleValue_ShouldReturnSameValue()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('single').property('value', 42)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('single').values('value').mean()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(42.0);
    }

    [Fact]
    public async Task Max_WithNegativeNumbers_ShouldReturnCorrectMax()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('temp').property('value', -10)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('temp').property('value', -5)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('temp').property('value', -20)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('temp').values('value').max()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(-5.0);
    }

    [Fact]
    public async Task Min_WithNegativeNumbers_ShouldReturnCorrectMin()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('temp').property('value', -10)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('temp').property('value', -5)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('temp').property('value', -20)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('temp').values('value').min()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(-20.0);
    }

    [Fact]
    public async Task ChainedAggregations_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupSalaryTestData(connector);

        // Act - Get count of high-earning engineers (salary > 80000)
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').has('department', 'Engineering').has('salary', gt(80000)).count()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(1L); // Only Jane has salary > 80000
    }

    [Fact]
    public async Task AggregationWithLimit_ShouldRespectLimit()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupNumericTestData(connector);

        // Act - Get sum of first 2 ages
        var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('age').limit(2).sum()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().BeLessThan(93.0); // Should be less than total sum
    }

    [Fact]
    public async Task MultiPropertyGroupCount_ShouldGroupByMultipleProperties()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('employee').property('department', 'IT').property('level', 'Senior')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('employee').property('department', 'IT').property('level', 'Junior')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('employee').property('department', 'HR').property('level', 'Senior')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('employee').groupCount().by('department')", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var groupCounts = result.First() as Dictionary<string, long>; // Changed from int to long for consistency
        groupCounts.Should().NotBeNull();
        groupCounts.Should().ContainKey("IT");
        groupCounts.Should().ContainKey("HR");
        groupCounts["IT"].Should().Be(2L); // Changed from 2 to 2L
        groupCounts["HR"].Should().Be(1L); // Changed from 1 to 1L
    }

    [Fact]
    public async Task NestedTraversalAggregation_ShouldCalculateCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupAdvancedAggregationData(connector);

        // Act - Average salary of people NOT working on completed projects
        var result = await connector.ExecuteAsync("g.V().hasLabel('project').has('status', 'completed').in('assigned_to').values('salary').mean()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(65000.0); // Only Bob works on completed project
    }

    [Fact]
    public async Task AggregationWithDedup_ShouldEliminateDuplicates()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('item').property('category', 'A')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('item').property('category', 'A')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('item').property('category', 'B')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync("g.V().hasLabel('item').values('category').dedup().count()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(2L); // Should count only unique categories: A, B
    }

    private static async Task SetupNumericTestData(InMemoryGremlinLanguageConnector connector)
    {
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('age', 30)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('age', 28)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob').property('age', 35)", new Dictionary<string, object>());
    }

    private static async Task SetupGroupTestData(InMemoryGremlinLanguageConnector connector)
    {
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('department', 'Engineering')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob').property('department', 'Sales')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Alice').property('department', 'Marketing')", new Dictionary<string, object>());
    }

    private static async Task SetupSalaryTestData(InMemoryGremlinLanguageConnector connector)
    {
        await connector.ExecuteAsync("g.addV('person').property('name', 'John').property('department', 'Engineering').property('salary', 75000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Jane').property('department', 'Engineering').property('salary', 90000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'Bob').property('department', 'Sales').property('salary', 65000)", new Dictionary<string, object>());
    }

    private static async Task SetupAdvancedAggregationData(InMemoryGremlinLanguageConnector connector)
    {
        // Create people with salaries
        await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John').property('salary', 75000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane').property('salary', 90000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob').property('salary', 65000)", new Dictionary<string, object>());

        // Create projects
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj1').property('name', 'Active Project').property('status', 'active')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj2').property('name', 'Completed Project').property('status', 'completed')", new Dictionary<string, object>());

        // Create assignments
        await connector.ExecuteAsync("g.V('john').addE('assigned_to').to(g.V('proj1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('jane').addE('assigned_to').to(g.V('proj1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('assigned_to').to(g.V('proj2'))", new Dictionary<string, object>());
    }
}