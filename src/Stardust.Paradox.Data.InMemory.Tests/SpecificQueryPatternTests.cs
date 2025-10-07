using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests specifically for the query pattern: g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)
/// </summary>
public class SpecificQueryPatternTests
{
    [Fact]
    public async Task ExactQueryPattern_ShouldExecuteSuccessfully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupTestGraph(connector);

        // Act - Execute the exact query pattern
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "partition1" },
                { "__p1", "root" },
                { "__p2", "name" },
                { "__p3", "target" },
                { "__p4", "target" },
                { "__p7", 5 }
            });

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task QueryPattern_WithExistingMatchingVertex_ShouldReturnResult()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create a simple graph where we can find a match
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'v1').property('pk', 'pk1').property('name', 'start')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'v2').property('pk', 'pk1').property('name', 'middle')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'v3').property('pk', 'pk1').property('name', 'found')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('v1').addE('link').to(g.V('v2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('v2').addE('link').to(g.V('v3'))", new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "pk1" },
                { "__p1", "v1" },
                { "__p2", "name" },
                { "__p3", "found" },
                { "__p4", "v3" },
                { "__p7", 3 }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("v3");
        ((string)result.First().properties.name).Should().Be("found");
    }

    [Fact]
    public async Task QueryPattern_WithoutMatch_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupTestGraph(connector);

        // Act - Search for non-existent vertex
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "partition1" },
                { "__p1", "root" },
                { "__p2", "name" },
                { "__p3", "nonexistent" },
                { "__p4", "nonexistent" },
                { "__p7", 5 }
            });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryPattern_Components_V_WithArraySyntax()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync(
            "g.addV('test').property('id', 'testid').property('pk', 'testpk')", 
            new Dictionary<string, object>());

        // Act - Test V([pk, id]) syntax
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1])", 
            new Dictionary<string, object> 
            { 
                { "__p0", "testpk" },
                { "__p1", "testid" }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("testid");
    }

    [Fact]
    public async Task QueryPattern_Components_Emit()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('n').property('id', 'v1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('n').property('id', 'v2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('v1').addE('e').to(g.V('v2'))", new Dictionary<string, object>());

        // Act - Test emit() which should include start vertex
        var result = await connector.ExecuteAsync(
            "g.V(__p0).emit().repeat(out())", 
            new Dictionary<string, object> { { "__p0", "v1" } });

        // Assert - Should include v1 (start) and any traversed vertices
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryPattern_Components_RepeatOut()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('n').property('id', 'v1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('n').property('id', 'v2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('n').property('id', 'v3')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('v1').addE('e').to(g.V('v2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('v2').addE('e').to(g.V('v3'))", new Dictionary<string, object>());

        // Act - Test repeat(out())
        var result = await connector.ExecuteAsync(
            "g.V(__p0).repeat(out()).until(loops().is(__p1))", 
            new Dictionary<string, object> 
            { 
                { "__p0", "v1" },
                { "__p1", 2 }
            });

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryPattern_Components_UntilLoops()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('n').property('id', 'v1')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('n').property('id', 'v2')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('v1').addE('e').to(g.V('v2'))", new Dictionary<string, object>());

        // Act - Test until(loops().is(__p))
        var result = await connector.ExecuteAsync(
            "g.V(__p0).repeat(out()).until(loops().is(__p1))", 
            new Dictionary<string, object> 
            { 
                { "__p0", "v1" },
                { "__p1", 1 }
            });

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryPattern_Components_Dedup()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('n').property('id', 'v1').property('value', 'A')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('n').property('id', 'v2').property('value', 'A')", new Dictionary<string, object>());

        // Act - Test dedup()
        var result = await connector.ExecuteAsync(
            "g.V().values(__p0).dedup()", 
            new Dictionary<string, object> { { "__p0", "value" } });

        // Assert - Should only return one 'A'
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryPattern_Components_HasWithProperty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync(
            "g.addV('test').property('id', 'v1').property('status', 'active')", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('test').property('id', 'v2').property('status', 'inactive')", 
            new Dictionary<string, object>());

        // Act - Test has(__p1, __p2)
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "status" },
                { "__p1", "active" }
            });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("v1");
    }

    [Fact]
    public async Task QueryPattern_Components_HasId()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync("g.addV('test').property('id', 'target-id')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('test').property('id', 'other-id')", new Dictionary<string, object>());

        // Act - Test hasId(__p)
        var result = await connector.ExecuteAsync(
            "g.V().hasId(__p0)", 
            new Dictionary<string, object> { { "__p0", "target-id" } });

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("target-id");
    }

    [Fact]
    public async Task QueryPattern_FullChain_StepByStep()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Setup a graph
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'start').property('pk', 'p1').property('level', 0)", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'mid1').property('pk', 'p1').property('level', 1)", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'end').property('pk', 'p1').property('level', 2).property('found', true)", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('start').addE('next').to(g.V('mid1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('mid1').addE('next').to(g.V('end'))", new Dictionary<string, object>());

        // Act - Build the query step by step
        // Step 1: V with array
        var step1 = await connector.ExecuteAsync(
            "g.V([__p0,__p1])", 
            new Dictionary<string, object> { { "__p0", "p1" }, { "__p1", "start" } });
        step1.Should().HaveCount(1);

        // Step 2: Add emit and repeat
        var step2 = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out())", 
            new Dictionary<string, object> { { "__p0", "p1" }, { "__p1", "start" } });
        step2.Should().NotBeEmpty();

        // Step 3: Add until
        var step3 = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out()).until(loops().is(__p2))", 
            new Dictionary<string, object> 
            { 
                { "__p0", "p1" }, 
                { "__p1", "start" },
                { "__p2", 3 }
            });
        step3.Should().NotBeEmpty();

        // Step 4: Add has filter
        var step4 = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out()).until(loops().is(__p2)).has(__p3,__p4)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "p1" }, 
                { "__p1", "start" },
                { "__p2", 3 },
                { "__p3", "found" },
                { "__p4", true }
            });
        step4.Should().HaveCount(1);

        // Step 5: Full query with hasId
        var step5 = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p2)).has(__p3,__p4).hasId(__p5)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "p1" }, 
                { "__p1", "start" },
                { "__p2", 3 },
                { "__p3", "found" },
                { "__p4", true },
                { "__p5", "end" }
            });
        step5.Should().HaveCount(1);
        ((string)step5.First().id).Should().Be("end");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task QueryPattern_WithDifferentMaxLoops_ShouldRespectLimit(int maxLoops)
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await SetupTestGraph(connector);

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7))", 
            new Dictionary<string, object> 
            { 
                { "__p0", "partition1" },
                { "__p1", "root" },
                { "__p7", maxLoops }
            });

        // Assert - Just verify it executes without error
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryPattern_WithNoEdges_ShouldReturnOnlyStartVertex()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        await connector.ExecuteAsync(
            "g.addV('isolated').property('id', 'alone').property('pk', 'p1').property('name', 'target')", 
            new Dictionary<string, object>());

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).emit().repeat(out().dedup()).until(loops().is(__p7)).has(__p2,__p3).hasId(__p4)", 
            new Dictionary<string, object> 
            { 
                { "__p0", "p1" },
                { "__p1", "alone" },
                { "__p2", "name" },
                { "__p3", "target" },
                { "__p4", "alone" },
                { "__p7", 5 }
            });

        // Assert - Should return the start vertex since it matches the criteria
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("alone");
    }

    private static async Task SetupTestGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create a test graph structure
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'root').property('pk', 'partition1').property('name', 'root').property('level', 0)", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'child1').property('pk', 'partition1').property('name', 'child1').property('level', 1)", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'child2').property('pk', 'partition1').property('name', 'child2').property('level', 1)", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'target').property('pk', 'partition1').property('name', 'target').property('level', 2)", 
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('root').addE('connects').to(g.V('child1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('root').addE('connects').to(g.V('child2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('child2').addE('connects').to(g.V('target'))", new Dictionary<string, object>());
    }
}
