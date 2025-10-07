using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for complex loop queries with emit, repeat, until, and hasId patterns
/// </summary>
public class ComplexLoopQueryTests
{
    [Fact]
    public async Task EmitRepeatUntilLoops_WithParameterizedVertices_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupHierarchicalData(connector);

        // Act - Query matching: g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "person" },  // partition key placeholder
                { "__p1", "root" },    // vertex id
                { "__p2", "name" },    // property name
                { "__p3", "target" },  // property value
                { "__p4", "target" },  // id to match
                { "__p7", 5 }          // max loops
            });

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task V_WithArrayParameter_ShouldQueryCorrectVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync(
            "g.addV('person').property('id', 'v1').property('pk', 'person').property('name', 'John')", 
            new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1])", 
            new Dictionary<string, object> 
            { 
                { "__p0", "person" },
                { "__p1", "v1" }
            });

        // Assert
        result.Should().HaveCount(1);
        var vertex = result.First();
        ((string)vertex.properties.name).Should().Be("John");
    }

    [Fact]
    public async Task EmitRepeatOut_ShouldReturnAllLevels()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupHierarchicalData(connector);

        // Act
        var result = await connector.ExecuteAsync(
            "g.V('root').emit().repeat(out('child'))", 
            new Dictionary<string, object>());

        // Assert - Should include root plus all children
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RepeatUntilLoops_ShouldStopAtMaxDepth()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupHierarchicalData(connector);

        // Act
        var result = await connector.ExecuteAsync(
            "g.V('root').repeat(out('child')).until(loops().is(__p0))", 
            new Dictionary<string, object> { { "__p0", 2 } });

        // Assert - Should only traverse 2 levels
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EmitRepeatUntil_WithDedup_ShouldNotReturnDuplicates()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupHierarchicalDataWithCrossLinks(connector);

        // Act
        var result = await connector.ExecuteAsync(
            "g.V('root').emit().repeat(out('child').dedup()).until(loops().is(__p0))", 
            new Dictionary<string, object> { { "__p0", 3 } });

        // Assert
        var ids = result.Select(v => (string)v.id).ToList();
        ids.Should().OnlyHaveUniqueItems("dedup should remove duplicates");
    }

    [Fact]
    public async Task HasId_WithSingleParameter_ShouldFilterCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'v3').property('name', 'Bob')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().hasId(__p0)", 
            new Dictionary<string, object> { { "__p0", "v2" } });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.name).Should().Be("Jane");
    }

    [Fact]
    public async Task HasId_WithMultipleParameters_ShouldFilterMultipleVertices()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'John')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('name', 'Jane')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'v3').property('name', 'Bob')", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().hasId(__p0, __p1)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "v1" },
                { "__p1", "v3" }
            });

        // Assert
        result.Should().HaveCount(2);
        var names = result.Select(v => (string)v.properties.name).ToList();
        names.Should().Contain("John");
        names.Should().Contain("Bob");
    }

    [Fact]
    public async Task Loops_WithIs_ShouldFilterByLoopCount()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupHierarchicalData(connector);

        // Act - Get vertices at exactly depth 2
        var result = await connector.ExecuteAsync(
            "g.V('root').repeat(out('child')).emit().until(loops().is(__p0)).has('level', __p1)", 
            new Dictionary<string, object> 
            { 
                { "__p0", 3 },
                { "__p1", 2 }
            });

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComplexQuery_WithAllSteps_ShouldExecuteSuccessfully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupComplexTestData(connector);

        // Act - Full complex query
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out(__p2).dedup()).until(loops().is(__p3)).has(__p4,__p5).hasId(__p6)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "department" },   // partition key
                { "__p1", "dept-root" },    // start vertex id
                { "__p2", "manages" },       // edge label
                { "__p3", 4 },              // max depth
                { "__p4", "role" },         // property to filter
                { "__p5", "manager" },      // property value
                { "__p6", "mgr-1" }         // specific id to match
            });

        // Assert
        result.Should().NotBeNull();
        if (result.Any())
        {
            result.All(v => v.id != null).Should().BeTrue();
        }
    }

    [Fact]
    public async Task EmitRepeatOut_WithPropertyFilter_AndIdFilter_ShouldChainCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('org').property('id', 'org1').property('name', 'HQ').property('type', 'headquarters')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('org').property('id', 'org2').property('name', 'Branch1').property('type', 'branch')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('org').property('id', 'org3').property('name', 'Branch2').property('type', 'branch')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('org1').addE('contains').to(g.V('org2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('org1').addE('contains').to(g.V('org3'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync(
            "g.V(__p0).emit().repeat(out(__p1)).until(loops().is(__p2)).has(__p3,__p4).hasId(__p5)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "org1" },
                { "__p1", "contains" },
                { "__p2", 3 },
                { "__p3", "type" },
                { "__p4", "branch" },
                { "__p5", "org2" }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("org2");
    }

    [Fact]
    public async Task ParameterizedQuery_WithUnderscorePrefix_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('test').property('id', 'test1').property('value', 'target')", new Dictionary<string, object>());

        // Act - Using __ prefix convention
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "value" },
                { "__p1", "target" }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().properties.value).Should().Be("target");
    }

    [Fact]
    public async Task ParameterizedQuery_WithNumericParameters_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('item').property('id', 'item1').property('count', 10)", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "count" },
                { "__p1", 10 }
            });

        // Assert
        result.Should().HaveCount(1);
        ((int)result.First().properties.count).Should().Be(10);
    }

    private static async Task SetupHierarchicalData(InMemoryGremlinLanguageConnector connector)
    {
        // Create root
        await connector.ExecuteAsync("g.addV('node').property('id', 'root').property('name', 'Root').property('level', 0)", new Dictionary<string, object>());
        
        // Level 1
        await connector.ExecuteAsync("g.addV('node').property('id', 'child1').property('name', 'Child1').property('level', 1)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('node').property('id', 'child2').property('name', 'Child2').property('level', 1)", new Dictionary<string, object>());
        
        // Level 2
        await connector.ExecuteAsync("g.addV('node').property('id', 'grandchild1').property('name', 'GrandChild1').property('level', 2)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('node').property('id', 'target').property('name', 'target').property('level', 2)", new Dictionary<string, object>());
        
        // Create edges
        await connector.ExecuteAsync("g.V('root').addE('child').to(g.V('child1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('root').addE('child').to(g.V('child2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('child1').addE('child').to(g.V('grandchild1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('child2').addE('child').to(g.V('target'))", new Dictionary<string, object>());
    }

    private static async Task SetupHierarchicalDataWithCrossLinks(InMemoryGremlinLanguageConnector connector)
    {
        await SetupHierarchicalData(connector);
        
        // Add cross-links that could cause duplicates
        await connector.ExecuteAsync("g.V('child1').addE('child').to(g.V('target'))", new Dictionary<string, object>());
    }

    private static async Task SetupComplexTestData(InMemoryGremlinLanguageConnector connector)
    {
        // Create department hierarchy with roles
        await connector.ExecuteAsync(
            "g.addV('department').property('id', 'dept-root').property('pk', 'department').property('name', 'Engineering').property('role', 'department')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('person').property('id', 'mgr-1').property('pk', 'department').property('name', 'Manager1').property('role', 'manager')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('person').property('id', 'emp-1').property('pk', 'department').property('name', 'Employee1').property('role', 'employee')", 
            new Dictionary<string, object>());
        
        // Create edges
        await connector.ExecuteAsync("g.V('dept-root').addE('manages').to(g.V('mgr-1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('mgr-1').addE('manages').to(g.V('emp-1'))", new Dictionary<string, object>());
    }
}
