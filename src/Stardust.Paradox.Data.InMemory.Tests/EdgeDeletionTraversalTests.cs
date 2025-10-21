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
    /// Tests to verify edge deletion bug fix - ensures traversal queries work correctly after edge deletion
    /// 
    /// Bug Description:
    /// When edges are deleted using Drop(), subsequent graph traversal queries fail to properly 
    /// reflect the deletion, causing queries to return 0 results instead of the actual remaining data.
    /// 
    /// Root Cause:
    /// The InMemory database's Drop() implementation removes edges from the raw edge collection
    /// but fails to properly maintain graph traversal indices, causing traversal operations to 
    /// fail or return incorrect results.
    /// </summary>
    public class EdgeDeletionTraversalTests
    {
        private readonly ITestOutputHelper _output;

        public EdgeDeletionTraversalTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task EdgeDeletion_MaintainsGraphTraversalState()
        {
            // Arrange: Create a service with 2 members (simulating license management scenario)
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Create service vertex
            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["name"] = "TestService",
                ["licenseLimit"] = 2
            }, "service1");

            // Create profile vertices
            var profile1 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["name"] = "User1"
            }, "profile1");

            var profile2 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["name"] = "User2"
            }, "profile2");

            // Add members edges (bidirectional: memberOf and members)
            var edge1MemberOf = db.AddEdge("memberOf", "profile1", "service1", new Dictionary<string, object>(), "memberOf_profile1_service1");
            var edge1Members = db.AddEdge("members", "service1", "profile1", new Dictionary<string, object>(), "members_service1_profile1");

            var edge2MemberOf = db.AddEdge("memberOf", "profile2", "service1", new Dictionary<string, object>(), "memberOf_profile2_service1");
            var edge2Members = db.AddEdge("members", "service1", "profile2", new Dictionary<string, object>(), "members_service1_profile2");

            _output.WriteLine("=== Initial State ===");
            _output.WriteLine($"Total edges: {db.GetAllEdges().Count()}");
            _output.WriteLine($"Edges from service1: {db.GetOutEdges("service1").Count()}");

            // Act: Query members before deletion
            var membersBefore = await connector.ExecuteAsync("g.V('service1').out('members').hasLabel('profile')", null);
            _output.WriteLine($"Members before deletion: {membersBefore.Count()}");
            Assert.Equal(2, membersBefore.Count());

            // Delete profile1's edges (simulating member removal)
            _output.WriteLine("\n=== Deleting Profile1 Edges ===");
            var deleteQuery1 = "g.V('profile1').outE('memberOf').where(__.otherV().hasId('service1')).drop()";
            await connector.ExecuteAsync(deleteQuery1, null);

            var deleteQuery2 = "g.V('service1').outE('members').where(__.otherV().hasId('profile1')).drop()";
            await connector.ExecuteAsync(deleteQuery2, null);

            _output.WriteLine($"Total edges after deletion: {db.GetAllEdges().Count()}");
            _output.WriteLine($"Edges from service1 after deletion: {db.GetOutEdges("service1").Count()}");

            // Assert: Raw edge count should be correct (2 edges remaining)
            Assert.Equal(2, db.GetAllEdges().Count());

            // Critical Test: Traversal query should return 1 member (profile2), not 0
            var membersAfter = await connector.ExecuteAsync("g.V('service1').out('members').hasLabel('profile')", null);
            _output.WriteLine($"Members after deletion: {membersAfter.Count()}");
            
            // THIS IS THE BUG: Should be 1, but returns 0 due to corrupted traversal state
            Assert.Equal(1, membersAfter.Count());

            // Verify the correct member is returned
            var member = membersAfter.FirstOrDefault();
            Assert.NotNull(member);
            var memberId = member.id?.ToString();
            Assert.Equal("profile2", memberId);
        }

        [Fact]
        public async Task EdgeDeletion_AllowsAddingNewEdgesAfterDeletion()
        {
            // Arrange: Simulate the full license management scenario
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["name"] = "TestService",
                ["licenseLimit"] = 2
            }, "service1");

            var profile1 = db.AddVertex("profile", new Dictionary<string, object> { ["name"] = "User1" }, "profile1");
            var profile2 = db.AddVertex("profile", new Dictionary<string, object> { ["name"] = "User2" }, "profile2");
            var profile3 = db.AddVertex("profile", new Dictionary<string, object> { ["name"] = "User3" }, "profile3");

            // Fill capacity
            db.AddEdge("memberOf", "profile1", "service1", null, "memberOf_profile1_service1");
            db.AddEdge("members", "service1", "profile1", null, "members_service1_profile1");
            db.AddEdge("memberOf", "profile2", "service1", null, "memberOf_profile2_service1");
            db.AddEdge("members", "service1", "profile2", null, "members_service1_profile2");

            // Act: Remove profile1
            await connector.ExecuteAsync("g.V('profile1').outE('memberOf').where(__.otherV().hasId('service1')).drop()", null);
            await connector.ExecuteAsync("g.V('service1').outE('members').where(__.otherV().hasId('profile1')).drop()", null);

            // Add profile3 (replacing profile1)
            db.AddEdge("memberOf", "profile3", "service1", null, "memberOf_profile3_service1");
            db.AddEdge("members", "service1", "profile3", null, "members_service1_profile3");

            // Assert: Should have 2 members (profile2 and profile3)
            var members = await connector.ExecuteAsync("g.V('service1').out('members').hasLabel('profile')", null);
            Assert.Equal(2, members.Count());

            var memberIds = members.Select(m => m.id?.ToString()).OrderBy(id => id).ToList();
            Assert.Contains("profile2", memberIds);
            Assert.Contains("profile3", memberIds);
            Assert.DoesNotContain("profile1", memberIds);
        }

        [Fact]
        public async Task EdgeDeletion_PreservesOtherEdges()
        {
            // Arrange: Create a more complex graph
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", null, "service1");
            var profile1 = db.AddVertex("profile", null, "profile1");
            var profile2 = db.AddVertex("profile", null, "profile2");
            var profile3 = db.AddVertex("profile", null, "profile3");

            // Create multiple relationships
            db.AddEdge("members", "service1", "profile1", null, "e1");
            db.AddEdge("members", "service1", "profile2", null, "e2");
            db.AddEdge("members", "service1", "profile3", null, "e3");
            db.AddEdge("knows", "profile1", "profile2", null, "e4"); // Additional edge type

            // Act: Delete only profile1's membership edge
            await connector.ExecuteAsync("g.V('service1').outE('members').where(__.otherV().hasId('profile1')).drop()", null);

            // Assert: Other members edges should still work
            var members = await connector.ExecuteAsync("g.V('service1').out('members')", null);
            Assert.Equal(2, members.Count());

            // Assert: The "knows" edge should be unaffected
            var friends = await connector.ExecuteAsync("g.V('profile1').out('knows')", null);
            Assert.Single(friends);
        }

        [Fact]
        public async Task EdgeDeletion_WorksWithMultipleDeletions()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", null, "service1");
            for (int i = 1; i <= 5; i++)
            {
                var profile = db.AddVertex("profile", new Dictionary<string, object> { ["name"] = $"User{i}" }, $"profile{i}");
                db.AddEdge("members", "service1", $"profile{i}", null, $"edge{i}");
            }

            // Act: Delete edges one by one and verify traversal works after each deletion
            for (int i = 1; i <= 3; i++)
            {
                await connector.ExecuteAsync($"g.V('service1').outE('members').where(__.otherV().hasId('profile{i}')).drop()", null);
                
                var remaining = await connector.ExecuteAsync("g.V('service1').out('members')", null);
                var expectedCount = 5 - i;
                _output.WriteLine($"After deleting profile{i}: {remaining.Count()} members (expected {expectedCount})");
                Assert.Equal(expectedCount, remaining.Count());
            }
        }

        [Fact]
        public async Task EdgeDeletion_HandlesEmptyResultsCorrectly()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", null, "service1");
            var profile = db.AddVertex("profile", null, "profile1");
            db.AddEdge("members", "service1", "profile1", null, "edge1");

            // Act: Delete the only edge
            await connector.ExecuteAsync("g.V('service1').outE('members').drop()", null);

            // Assert: Traversal should return empty results (not fail)
            var members = await connector.ExecuteAsync("g.V('service1').out('members')", null);
            Assert.Empty(members);
        }

        [Fact]
        public async Task EdgeDeletion_WorksWithComplexTraversals()
        {
            // Arrange: Create a hierarchical structure
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", null, "service1");
            var team1 = db.AddVertex("team", null, "team1");
            var team2 = db.AddVertex("team", null, "team2");
            var user1 = db.AddVertex("user", null, "user1");
            var user2 = db.AddVertex("user", null, "user2");

            db.AddEdge("hasTeam", "service1", "team1", null, "st1");
            db.AddEdge("hasTeam", "service1", "team2", null, "st2");
            db.AddEdge("memberOf", "user1", "team1", null, "ut1");
            db.AddEdge("memberOf", "user2", "team2", null, "ut2");

            // Act: Delete team1 relationships
            await connector.ExecuteAsync("g.V('service1').outE('hasTeam').where(__.otherV().hasId('team1')).drop()", null);

            // Assert: Complex traversal should still work
            var remainingTeams = await connector.ExecuteAsync("g.V('service1').out('hasTeam')", null);
            Assert.Single(remainingTeams);

            var teamMembers = await connector.ExecuteAsync("g.V('service1').out('hasTeam').in('memberOf')", null);
            Assert.Single(teamMembers); // Only user2 should be reachable
        }

        [Fact]
        public void EdgeDeletion_RawDatabaseRemovalWorks()
        {
            // Test the low-level database edge removal
            var db = new InMemoryGraphDatabase();

            var v1 = db.AddVertex("person", null, "v1");
            var v2 = db.AddVertex("person", null, "v2");
            var edge = db.AddEdge("knows", "v1", "v2", null, "e1");

            // Remove edge
            var removed = db.RemoveEdge("e1");
            Assert.True(removed);

            // Verify edge is removed
            Assert.Null(db.GetEdge("e1"));
            Assert.Empty(db.GetAllEdges());

            // Verify indices are updated
            Assert.Empty(db.GetOutEdges("v1"));
            Assert.Empty(db.GetInEdges("v2"));
        }

        [Fact]
        public void EdgeDeletion_UpdatesAllIndices()
        {
            // Test that all indices are properly updated after edge deletion
            var db = new InMemoryGraphDatabase();

            var v1 = db.AddVertex("person", null, "v1");
            var v2 = db.AddVertex("person", null, "v2");
            var edge = db.AddEdge("knows", "v1", "v2", new Dictionary<string, object>
            {
                ["since"] = 2020
            }, "e1");

            // Verify indices before deletion
            Assert.Single(db.GetEdgesByLabel("knows"));
            Assert.Single(db.GetEdgesByProperty("since", 2020));
            Assert.Single(db.GetOutEdges("v1"));
            Assert.Single(db.GetInEdges("v2"));

            // Remove edge
            db.RemoveEdge("e1");

            // Verify all indices are updated
            Assert.Empty(db.GetEdgesByLabel("knows"));
            Assert.Empty(db.GetEdgesByProperty("since", 2020));
            Assert.Empty(db.GetOutEdges("v1"));
            Assert.Empty(db.GetInEdges("v2"));
        }
    }
}
