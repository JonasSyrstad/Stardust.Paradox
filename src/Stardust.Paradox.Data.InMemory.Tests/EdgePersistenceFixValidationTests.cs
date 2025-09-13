using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Comprehensive tests to verify edge persistence fixes
/// </summary>
public class EdgePersistenceFixValidationTests
{
    [Fact]
    public async Task EdgePersistence_ComprehensiveValidation_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(options =>
        {
            options.EnableDebugLogging = true;
        });
        
        // Test 1: Basic edge persistence
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        
        var edgeResult = await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('bob'))", new Dictionary<string, object>());
        
        // Test 5: Edge properties - Create second edge with properties
        await connector.ExecuteAsync("g.V('alice').addE('likes').to(g.V('bob')).property('weight', 0.8)", new Dictionary<string, object>());
        
        // Now query all edges after both have been created
        var allEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
        var edgesWithWeight = await connector.ExecuteAsync("g.E().has('weight', 0.8)", new Dictionary<string, object>());
        
        // Test 2: Database integrity
        var integrity = connector.Database.ValidateIntegrity();
        
        // Test 3: Export data
        var (vertices, edges) = connector.Database.ExportData();
        
        // Test 4: Edge traversal
        var outEdges = await connector.ExecuteAsync("g.V('alice').outE()", new Dictionary<string, object>());
        var inEdges = await connector.ExecuteAsync("g.V('bob').inE()", new Dictionary<string, object>());
        
        // Assertions
        edgeResult.Should().HaveCount(1);
        allEdges.Should().HaveCount(2); // knows + likes
        
        ((bool)integrity["isValid"]).Should().BeTrue();
        ((int)integrity["totalEdges"]).Should().Be(2);
        ((int)integrity["orphanedEdges"]).Should().Be(0);
        
        vertices.Should().HaveCount(2);
        edges.Should().HaveCount(2);
        
        outEdges.Should().HaveCount(2);
        inEdges.Should().HaveCount(2);
        
        edgesWithWeight.Should().HaveCount(1);
        
        Console.WriteLine("All edge persistence validations passed!");
    }

    [Fact]
    public async Task EdgePersistence_ConcurrentOperations_ShouldMaintainIntegrity()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create vertices
        var vertexTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var id = $"v{i}";
            vertexTasks.Add(connector.ExecuteAsync($"g.addV('node').property('id', '{id}')", new Dictionary<string, object>()));
        }
        await Task.WhenAll(vertexTasks);
        
        // Create edges concurrently
        var edgeTasks = new List<Task>();
        for (int i = 0; i < 9; i++)
        {
            var from = $"v{i}";
            var to = $"v{i + 1}";
            edgeTasks.Add(connector.ExecuteAsync($"g.V('{from}').addE('connects').to(g.V('{to}'))", new Dictionary<string, object>()));
        }
        await Task.WhenAll(edgeTasks);
        
        // Validate
        var allEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
        var integrity = connector.Database.ValidateIntegrity();
        
        // Assertions
        allEdges.Should().HaveCount(9);
        ((bool)integrity["isValid"]).Should().BeTrue();
        
        Console.WriteLine($"Concurrent edge creation: {allEdges.Count()} edges, integrity valid: {integrity["isValid"]}");
    }

    [Fact]
    public async Task EdgePersistence_ComplexQueries_ShouldMaintainEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create a complex graph
        await connector.ExecuteAsync("g.addV('person').property('name', 'alice').property('age', 30)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'bob').property('age', 25)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('name', 'charlie').property('age', 35)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('name', 'tech_corp')", new Dictionary<string, object>());
        
        // Create various edge types
        await connector.ExecuteAsync("g.V().has('name', 'alice').addE('knows').to(g.V().has('name', 'bob'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V().has('name', 'bob').addE('knows').to(g.V().has('name', 'charlie'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V().has('name', 'alice').addE('works_for').to(g.V().has('name', 'tech_corp'))", new Dictionary<string, object>());
        
        // Perform complex queries that might affect persistence
        var friendsOfFriends = await connector.ExecuteAsync("g.V().has('name', 'alice').out('knows').out('knows')", new Dictionary<string, object>());
        var allEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
        var knowsEdges = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());
        var worksForEdges = await connector.ExecuteAsync("g.E().hasLabel('works_for')", new Dictionary<string, object>());
        
        // Validate persistence after complex operations
        var integrity = connector.Database.ValidateIntegrity();
        var (vertices, edges) = connector.Database.ExportData();
        
        // Assertions
        friendsOfFriends.Should().HaveCount(1); // Alice -> Bob -> Charlie
        allEdges.Should().HaveCount(3);
        knowsEdges.Should().HaveCount(2);
        worksForEdges.Should().HaveCount(1);
        
        ((bool)integrity["isValid"]).Should().BeTrue();
        vertices.Should().HaveCount(4);
        edges.Should().HaveCount(3);
        
        Console.WriteLine($"Complex queries completed: {allEdges.Count()} edges persisted");
    }

    [Fact]
    public async Task EdgePersistence_AfterVertexDeletion_ShouldCleanup()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create graph
        await connector.ExecuteAsync("g.addV('person').property('id', 'alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'bob')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('person').property('id', 'charlie')", new Dictionary<string, object>());
        
        await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('bob'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('charlie'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('knows').to(g.V('charlie'))", new Dictionary<string, object>());
        
        // Verify initial state
        var initialEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
        initialEdges.Should().HaveCount(3);
        
        // Delete vertex with edges
        await connector.ExecuteAsync("g.V('alice').drop()", new Dictionary<string, object>());
        
        // Verify cleanup
        var remainingEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
        var integrity = connector.Database.ValidateIntegrity();
        
        // Assertions
        remainingEdges.Should().HaveCount(1); // Only bob -> charlie should remain
        ((bool)integrity["isValid"]).Should().BeTrue();
        ((int)integrity["orphanedEdges"]).Should().Be(0);
        
        Console.WriteLine($"After vertex deletion: {remainingEdges.Count()} edges remaining");
    }
}