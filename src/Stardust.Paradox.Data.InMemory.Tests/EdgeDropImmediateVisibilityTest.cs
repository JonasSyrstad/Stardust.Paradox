using System;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Xunit;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Test to reproduce and verify fix for INMEMORY_DB_BUG_REPORT:
    /// Edge deletions via Edge.Drop() should be immediately visible in subsequent traversal queries
    /// </summary>
    public class EdgeDropImmediateVisibilityTest
    {
        [Fact]
        public async Task Drop_Edge_ShouldBeImmediatelyVisibleInSubsequentQuery()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Add two vertices
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice"
            }, "alice");
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Bob"
            }, "bob");
            
            // Add an edge between them
            connector.AddEdge("knows", "alice", "bob", new System.Collections.Generic.Dictionary<string, object>
            {
                ["since"] = 2020
            }, "edge1");
            
            // Verify edge exists
            var edgesBeforeDrop = await connector.ExecuteAsync("g.V('alice').outE('knows')", null);
            Assert.Single(edgesBeforeDrop);
            
            // Act: Drop the edge
            await connector.ExecuteAsync("g.E('edge1').drop()", null);
            
            // Assert: Edge should NOT be visible in subsequent query
            var edgesAfterDrop = await connector.ExecuteAsync("g.V('alice').outE('knows')", null);
            Assert.Empty(edgesAfterDrop);
            
            // Also verify from the other direction
            var edgesFromBob = await connector.ExecuteAsync("g.V('bob').inE('knows')", null);
            Assert.Empty(edgesFromBob);
        }
        
        [Fact]
        public async Task Drop_Edge_ShouldBeImmediatelyVisible_InVertexTraversal()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Add three vertices
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice"
            }, "alice");
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Bob"
            }, "bob");
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Charlie"
            }, "charlie");
            
            // Add edges
            connector.AddEdge("knows", "alice", "bob", null, "edge1");
            connector.AddEdge("knows", "alice", "charlie", null, "edge2");
            
            // Verify both edges exist via vertex traversal
            var friendsBeforeDrop = await connector.ExecuteAsync("g.V('alice').out('knows')", null);
            Assert.Equal(2, friendsBeforeDrop.Count());
            
            // Act: Drop one edge
            await connector.ExecuteAsync("g.E('edge1').drop()", null);
            
            // Assert: Only one friend should remain
            var friendsAfterDrop = await connector.ExecuteAsync("g.V('alice').out('knows')", null);
            Assert.Single(friendsAfterDrop);
            
            var remainingFriend = friendsAfterDrop.First();
            Assert.Equal("charlie", remainingFriend.id.ToString());
        }
        
        [Fact]
        public async Task Drop_Multiple_Edges_ShouldAllBeImmediatelyVisible()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Create a scenario similar to the license management bug
            connector.AddVertex("service", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "MyService",
                ["licenseLimit"] = 2
            }, "service1");
            
            connector.AddVertex("profile", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "User1"
            }, "user1");
            
            connector.AddVertex("profile", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "User2"
            }, "user2");
            
            // Add member edges (simulating license assignments)
            connector.AddEdge("member", "service1", "user1", null, "member1");
            connector.AddEdge("member", "service1", "user2", null, "member2");
            
            // Verify 2 members
            var membersBefore = await connector.ExecuteAsync("g.V('service1').outE('member')", null);
            Assert.Equal(2, membersBefore.Count());
            
            // Act: Remove one member
            await connector.ExecuteAsync("g.E('member1').drop()", null);
            
            // Assert: Should have only 1 member now
            var membersAfter = await connector.ExecuteAsync("g.V('service1').outE('member')", null);
            Assert.Single(membersAfter);
            
            // Verify we can get the count correctly
            var countResult = await connector.ExecuteAsync("g.V('service1').outE('member').count()", null);
            var count = Convert.ToInt32(countResult.First());
            Assert.Equal(1, count);
        }
        
        [Fact]
        public async Task Drop_Edge_Using_Traversal_ShouldBeImmediatelyVisible()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice"
            }, "alice");
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Bob"
            }, "bob");
            
            connector.AddEdge("knows", "alice", "bob", new System.Collections.Generic.Dictionary<string, object>
            {
                ["since"] = 2020
            }, "edge1");
            
            // Verify edge exists
            var edgesBefore = await connector.ExecuteAsync("g.V('alice').outE('knows')", null);
            Assert.Single(edgesBefore);
            
            // Act: Drop edge using traversal (as mentioned in bug report)
            await connector.ExecuteAsync("g.V('alice').bothE().hasLabel('knows').where(__.otherV().hasId('bob')).drop()", null);
            
            // Assert: Edge should be gone
            var edgesAfter = await connector.ExecuteAsync("g.V('alice').outE('knows')", null);
            Assert.Empty(edgesAfter);
        }
    }
}
