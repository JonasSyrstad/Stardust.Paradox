using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory.Core;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests to verify that addE() step properly updates all internal data structures and indices
    /// 
    /// Issue: When edges are added via addE().from().to() Gremlin queries,
    /// the edge indices may not be properly updated, causing subsequent traversal
    /// queries to fail to discover the newly added edges.
    /// </summary>
    public class AddEStepIndexUpdateTests
    {
        private readonly ITestOutputHelper _output;

        public AddEStepIndexUpdateTests(ITestOutputHelper _output)
        {
            this._output = _output;
        }

        [Fact]
        public async Task AddE_UpdatesEdgeLabelIndex()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Add vertices
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            _output.WriteLine("=== Before addE ===");
            var edgesBefore = db.GetEdgesByLabel("knows");
            _output.WriteLine($"Edges with label 'knows': {edgesBefore.Count()}");
            Assert.Empty(edgesBefore);

            // Act: Add edge via Gremlin query
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);

            // Assert: Edge should be findable via label index
            _output.WriteLine("\n=== After addE ===");
            var edgesAfter = db.GetEdgesByLabel("knows");
            _output.WriteLine($"Edges with label 'knows': {edgesAfter.Count()}");
            Assert.Single(edgesAfter);
        }

        [Fact]
        public async Task AddE_UpdatesOutEdgeIndex()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            _output.WriteLine("=== Before addE ===");
            var outEdgesBefore = db.GetOutEdges("alice");
            _output.WriteLine($"Out edges from alice: {outEdgesBefore.Count()}");
            Assert.Empty(outEdgesBefore);

            // Act
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);

            // Assert
            _output.WriteLine("\n=== After addE ===");
            var outEdgesAfter = db.GetOutEdges("alice");
            _output.WriteLine($"Out edges from alice: {outEdgesAfter.Count()}");
            Assert.Single(outEdgesAfter);

            var edge = outEdgesAfter.First();
            Assert.Equal("knows", edge.Label);
            Assert.Equal("alice", edge.OutVertexId);
            Assert.Equal("bob", edge.InVertexId);
        }

        [Fact]
        public async Task AddE_UpdatesInEdgeIndex()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            _output.WriteLine("=== Before addE ===");
            var inEdgesBefore = db.GetInEdges("bob");
            _output.WriteLine($"In edges to bob: {inEdgesBefore.Count()}");
            Assert.Empty(inEdgesBefore);

            // Act
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);

            // Assert
            _output.WriteLine("\n=== After addE ===");
            var inEdgesAfter = db.GetInEdges("bob");
            _output.WriteLine($"In edges to bob: {inEdgesAfter.Count()}");
            Assert.Single(inEdgesAfter);
        }

        [Fact]
        public async Task AddE_UpdatesOutVertexIndex()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            _output.WriteLine("=== Before addE ===");
            var outVerticesBefore = db.GetOutVertices("alice", "knows");
            _output.WriteLine($"Out vertices from alice via 'knows': {outVerticesBefore.Count()}");
            Assert.Empty(outVerticesBefore);

            // Act
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);

            // Assert
            _output.WriteLine("\n=== After addE ===");
            var outVerticesAfter = db.GetOutVertices("alice", "knows");
            _output.WriteLine($"Out vertices from alice via 'knows': {outVerticesAfter.Count()}");
            Assert.Single(outVerticesAfter);
            Assert.Equal("bob", outVerticesAfter.First().Id);
        }

        [Fact]
        public async Task AddE_UpdatesInVertexIndex()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            _output.WriteLine("=== Before addE ===");
            var inVerticesBefore = db.GetInVertices("bob", "knows");
            _output.WriteLine($"In vertices to bob via 'knows': {inVerticesBefore.Count()}");
            Assert.Empty(inVerticesBefore);

            // Act
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);

            // Assert
            _output.WriteLine("\n=== After addE ===");
            var inVerticesAfter = db.GetInVertices("bob", "knows");
            _output.WriteLine($"In vertices to bob via 'knows': {inVerticesAfter.Count()}");
            Assert.Single(inVerticesAfter);
            Assert.Equal("alice", inVerticesAfter.First().Id);
        }

        [Fact]
        public async Task AddE_EdgeIsDiscoverableViaOutTraversal()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            // Act: Add edge via Gremlin
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);

            // Assert: Should be able to traverse the edge
            var result = await connector.ExecuteAsync("g.V('alice').out('knows')", null);
            _output.WriteLine($"Traversal result: {result.Count()} vertices");
            Assert.Single(result);
            
            var vertex = result.First();
            _output.WriteLine($"Found vertex: {vertex.id}");
            Assert.Equal("bob", vertex.id?.ToString());
        }

        [Fact]
        public async Task AddE_EdgeIsDiscoverableViaInTraversal()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            // Act
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);

            // Assert
            var result = await connector.ExecuteAsync("g.V('bob').in('knows')", null);
            _output.WriteLine($"Traversal result: {result.Count()} vertices");
            Assert.Single(result);
            
            var vertex = result.First();
            _output.WriteLine($"Found vertex: {vertex.id}");
            Assert.Equal("alice", vertex.id?.ToString());
        }

        [Fact]
        public async Task AddE_ComplexQuery_EmitRepeatUntil()
        {
            // Arrange: This is the exact scenario from the bug report
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Create service and profiles
            db.AddVertex("service", new Dictionary<string, object> 
            { 
                ["name"] = "TestService",
                ["entityType"] = "service"
            }, "service1");
            
            db.AddVertex("profile", new Dictionary<string, object> 
            { 
                ["name"] = "User1",
                ["entityType"] = "profile"
            }, "profile1");
            
            db.AddVertex("profile", new Dictionary<string, object> 
            { 
                ["name"] = "User2",
                ["entityType"] = "profile"
            }, "profile2");

            // Add edges via Gremlin
            await connector.ExecuteAsync("g.V('service1').addE('members').to(V('profile1'))", null);
            await connector.ExecuteAsync("g.V('service1').addE('members').to(V('profile2'))", null);

            // Act: Use the complex query from the bug report
            var query = @"g.V('service1')
                .emit()
                .repeat(__.out('members').dedup())
                .until(__.loops().is(7))
                .has('entityType', 'profile')
                .dedup()";

            var result = await connector.ExecuteAsync(query, null);

            // Assert
            _output.WriteLine($"Complex query result: {result.Count()} profiles");
            Assert.Equal(2, result.Count());

            var ids = result.Select(r => r.id?.ToString()).OrderBy(id => id).ToList();
            Assert.Contains("profile1", ids);
            Assert.Contains("profile2", ids);
        }

        [Fact]
        public async Task AddE_WithProperties_UpdatesPropertyIndex()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            // Act: Add edge with properties via Gremlin
            await connector.ExecuteAsync(
                "g.V('alice').addE('knows').to(V('bob')).property('since', 2020).property('weight', 0.8)", 
                null);

            // Assert: Edge should be findable via property index
            var edgesByProperty = db.GetEdgesByProperty("since", 2020);
            _output.WriteLine($"Edges with 'since' = 2020: {edgesByProperty.Count()}");
            Assert.Single(edgesByProperty);

            var edge = edgesByProperty.First();
            Assert.Equal("knows", edge.Label);
            Assert.Equal(2020, edge.GetProperty<int>("since", 0));
            Assert.Equal(0.8, edge.GetProperty<double>("weight", 0.0));
        }

        [Fact]
        public async Task AddE_AfterDeletion_EdgeIsDiscoverable()
        {
            // Arrange: Scenario from the bug report
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            db.AddVertex("service", new Dictionary<string, object> { ["entityType"] = "service" }, "service1");
            db.AddVertex("profile", new Dictionary<string, object> { ["entityType"] = "profile" }, "profile1");
            db.AddVertex("profile", new Dictionary<string, object> { ["entityType"] = "profile" }, "profile2");
            db.AddVertex("profile", new Dictionary<string, object> { ["entityType"] = "profile" }, "profile3");

            // Add 2 edges
            await connector.ExecuteAsync("g.V('service1').addE('members').to(V('profile1'))", null);
            await connector.ExecuteAsync("g.V('service1').addE('members').to(V('profile2'))", null);

            _output.WriteLine("=== After adding 2 edges ===");
            var edges1 = db.GetAllEdges();
            var members1 = await connector.ExecuteAsync("g.V('service1').out('members')", null);
            _output.WriteLine($"Total edges: {edges1.Count()}");
            _output.WriteLine($"Members via traversal: {members1.Count()}");
            Assert.Equal(2, edges1.Count());
            Assert.Equal(2, members1.Count());

            // Delete 1 edge
            await connector.ExecuteAsync("g.V('service1').outE('members').where(__.otherV().hasId('profile1')).drop()", null);

            _output.WriteLine("\n=== After deleting 1 edge ===");
            var edges2 = db.GetAllEdges();
            var members2 = await connector.ExecuteAsync("g.V('service1').out('members')", null);
            _output.WriteLine($"Total edges: {edges2.Count()}");
            _output.WriteLine($"Members via traversal: {members2.Count()}");
            Assert.Single(edges2);
            Assert.Single(members2);

            // Add new edge
            await connector.ExecuteAsync("g.V('service1').addE('members').to(V('profile3'))", null);

            _output.WriteLine("\n=== After adding new edge ===");
            var edges3 = db.GetAllEdges();
            var members3 = await connector.ExecuteAsync("g.V('service1').out('members')", null);
            _output.WriteLine($"Total edges: {edges3.Count()}");
            _output.WriteLine($"Members via traversal: {members3.Count()}");
            Assert.Equal(2, edges3.Count());
            Assert.Equal(2, members3.Count()); // CRITICAL: This should return 2, not 0!

            var memberIds = (await connector.ExecuteAsync("g.V('service1').out('members')", null))
                .Select(v => v.id?.ToString())
                .OrderBy(id => id)
                .ToList();
            Assert.Contains("profile2", memberIds);
            Assert.Contains("profile3", memberIds);
            Assert.DoesNotContain("profile1", memberIds);
        }
    }
}
