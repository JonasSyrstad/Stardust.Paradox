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
    /// Tests for complex traversal queries (Emit/Repeat/Until) after edge deletion
    /// 
    /// Addresses the specific query pattern from ROOT_CAUSE_ANALYSIS:
    /// g.V(id, tenantId)
    ///   .Emit()
    ///   .Repeat(_ => _.Out("members").Dedup())
    ///   .Until(__ => __.Loops().Is(7))
    ///   .Has("entityType", "profile")
    ///   .Dedup()
    /// </summary>
    public class ComplexTraversalAfterEdgeDeletionTests
    {
        private readonly ITestOutputHelper _output;

        public ComplexTraversalAfterEdgeDeletionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ComplexTraversal_EmitRepeatUntil_WorksAfterEdgeDeletion()
        {
            // Arrange: Setup exact scenario from bug report
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Create service with partition key (tenant)
            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["name"] = "TestService",
                ["licenseLimit"] = 2,
                ["pk"] = "tenant1"
            }, "service1");

            // Create profiles with entityType property
            var profile1 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["name"] = "User1",
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile1");

            var profile2 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["name"] = "User2",
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile2");

            var profile3 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["name"] = "User3",
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile3");

            // Add bidirectional edges (memberOf and members)
            db.AddEdge("memberOf", "profile1", "service1", null, "memberOf_profile1_service1");
            db.AddEdge("members", "service1", "profile1", null, "members_service1_profile1");

            db.AddEdge("memberOf", "profile2", "service1", null, "memberOf_profile2_service1");
            db.AddEdge("members", "service1", "profile2", null, "members_service1_profile2");

            _output.WriteLine("=== Initial State ===");
            _output.WriteLine($"Total edges: {db.GetAllEdges().Count()}");
            _output.WriteLine($"Service outgoing edges: {db.GetOutEdges("service1").Count()}");

            // Query using the EXACT pattern from the bug report
            var queryBefore = @"g.V(['tenant1','service1'])
                .emit()
                .repeat(__.out('members').dedup())
                .until(__.loops().is(7))
                .has('entityType', 'profile')
                .dedup()";

            var resultsBefore = await connector.ExecuteAsync(queryBefore, null);
            _output.WriteLine($"Members before deletion: {resultsBefore.Count()}");
            Assert.Equal(2, resultsBefore.Count());

            // Act: Delete profile1's edges
            _output.WriteLine("\n=== Deleting Profile1 Edges ===");
            await connector.ExecuteAsync(
                "g.V(['tenant1','profile1']).outE('memberOf').where(__.otherV().hasId('service1')).drop()", 
                null);
            await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).outE('members').where(__.otherV().hasId('profile1')).drop()", 
                null);

            _output.WriteLine($"Total edges after deletion: {db.GetAllEdges().Count()}");
            _output.WriteLine($"Service outgoing edges after deletion: {db.GetOutEdges("service1").Count()}");

            // Verify deletion worked at low level
            Assert.Equal(2, db.GetAllEdges().Count());
            Assert.Single(db.GetOutEdges("service1"));

            // Query after deletion - THIS IS THE CRITICAL TEST
            var queryAfterDelete = @"g.V(['tenant1','service1'])
                .emit()
                .repeat(__.out('members').dedup())
                .until(__.loops().is(7))
                .has('entityType', 'profile')
                .dedup()";

            var resultsAfterDelete = await connector.ExecuteAsync(queryAfterDelete, null);
            _output.WriteLine($"Members after deletion: {resultsAfterDelete.Count()}");
            
            // Should return 1 member (profile2), not 0
            Assert.Equal(1, resultsAfterDelete.Count());
            var member = resultsAfterDelete.First();
            Assert.Equal("profile2", member.id?.ToString());

            // Add profile3
            _output.WriteLine("\n=== Adding Profile3 ===");
            db.AddEdge("memberOf", "profile3", "service1", null, "memberOf_profile3_service1");
            db.AddEdge("members", "service1", "profile3", null, "members_service1_profile3");

            _output.WriteLine($"Total edges after adding profile3: {db.GetAllEdges().Count()}");
            _output.WriteLine($"Service outgoing edges after adding profile3: {db.GetOutEdges("service1").Count()}");

            // Query after adding new member
            var queryFinal = @"g.V(['tenant1','service1'])
                .emit()
                .repeat(__.out('members').dedup())
                .until(__.loops().is(7))
                .has('entityType', 'profile')
                .dedup()";

            var resultsFinal = await connector.ExecuteAsync(queryFinal, null);
            _output.WriteLine($"Members after adding profile3: {resultsFinal.Count()}");
            
            // Should return 2 members (profile2, profile3), not 0
            Assert.Equal(2, resultsFinal.Count());

            var memberIds = resultsFinal.Select(m => m.id?.ToString()).OrderBy(id => id).ToList();
            Assert.Contains("profile2", memberIds);
            Assert.Contains("profile3", memberIds);
            Assert.DoesNotContain("profile1", memberIds);
        }

        [Fact]
        public async Task ComplexTraversal_SimpleOut_WorksAfterEdgeDeletion()
        {
            // Test simpler version without Emit/Repeat/Until to isolate issue
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["pk"] = "tenant1"
            }, "service1");

            var profile1 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile1");

            var profile2 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile2");

            db.AddEdge("members", "service1", "profile1", null, "e1");
            db.AddEdge("members", "service1", "profile2", null, "e2");

            // Simple query before deletion
            var resultsBefore = await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).out('members').has('entityType', 'profile')", 
                null);
            Assert.Equal(2, resultsBefore.Count());

            // Delete edge
            await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).outE('members').where(__.otherV().hasId('profile1')).drop()", 
                null);

            // Simple query after deletion - should work
            var resultsAfter = await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).out('members').has('entityType', 'profile')", 
                null);
            
            _output.WriteLine($"Simple query results after deletion: {resultsAfter.Count()}");
            Assert.Equal(1, resultsAfter.Count());
            Assert.Equal("profile2", resultsAfter.First().id?.ToString());
        }

        [Fact]
        public async Task ComplexTraversal_WithEmit_WorksAfterEdgeDeletion()
        {
            // Test with Emit() but without Repeat/Until
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["pk"] = "tenant1"
            }, "service1");

            var profile1 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile1");

            var profile2 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile2");

            db.AddEdge("members", "service1", "profile1", null, "e1");
            db.AddEdge("members", "service1", "profile2", null, "e2");

            // Query with emit before deletion
            var resultsBefore = await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).emit().out('members').has('entityType', 'profile')", 
                null);
            
            _output.WriteLine($"With emit before deletion: {resultsBefore.Count()}");

            // Delete edge
            await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).outE('members').where(__.otherV().hasId('profile1')).drop()", 
                null);

            // Query with emit after deletion
            var resultsAfter = await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).emit().out('members').has('entityType', 'profile')", 
                null);
            
            _output.WriteLine($"With emit after deletion: {resultsAfter.Count()}");
            // Emit should emit the starting vertex plus the traversed vertices
            // After deletion: service1 (emitted) + profile2 (traversed) = but filtered by has('entityType', 'profile')
            // So should return just profile2
            Assert.True(resultsAfter.Count() >= 1, "Should return at least profile2");
        }

        [Fact]
        public async Task ComplexTraversal_RepeatWithoutEmit_WorksAfterEdgeDeletion()
        {
            // Test Repeat/Until without Emit
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["pk"] = "tenant1"
            }, "service1");

            var profile1 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile1");

            var profile2 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile2");

            db.AddEdge("members", "service1", "profile1", null, "e1");
            db.AddEdge("members", "service1", "profile2", null, "e2");

            // Query with repeat/until before deletion
            var resultsBefore = await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).repeat(__.out('members')).until(__.loops().is(1)).has('entityType', 'profile')", 
                null);
            
            _output.WriteLine($"Repeat without emit before deletion: {resultsBefore.Count()}");
            Assert.Equal(2, resultsBefore.Count());

            // Delete edge
            await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).outE('members').where(__.otherV().hasId('profile1')).drop()", 
                null);

            // Query with repeat/until after deletion
            var resultsAfter = await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).repeat(__.out('members')).until(__.loops().is(1)).has('entityType', 'profile')", 
                null);
            
            _output.WriteLine($"Repeat without emit after deletion: {resultsAfter.Count()}");
            Assert.Equal(1, resultsAfter.Count());
            Assert.Equal("profile2", resultsAfter.First().id?.ToString());
        }

        [Fact]
        public async Task ComplexTraversal_FullPattern_MultipleIterations()
        {
            // Test the full Emit/Repeat/Until pattern with multiple levels
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Create hierarchy: service -> team -> user
            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["pk"] = "tenant1"
            }, "service1");

            var team1 = db.AddVertex("team", new Dictionary<string, object>
            {
                ["entityType"] = "team",
                ["pk"] = "tenant1"
            }, "team1");

            var user1 = db.AddVertex("user", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "user1");

            var user2 = db.AddVertex("user", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "user2");

            // Service -> Team -> Users
            db.AddEdge("members", "service1", "team1", null, "e1");
            db.AddEdge("members", "team1", "user1", null, "e2");
            db.AddEdge("members", "team1", "user2", null, "e3");

            // Query before deletion - should find users through team
            var queryBefore = @"g.V(['tenant1','service1'])
                .emit()
                .repeat(__.out('members').dedup())
                .until(__.loops().is(7))
                .has('entityType', 'profile')
                .dedup()";

            var resultsBefore = await connector.ExecuteAsync(queryBefore, null);
            _output.WriteLine($"Users before deletion (through team): {resultsBefore.Count()}");
            Assert.Equal(2, resultsBefore.Count());

            // Delete user1's edge from team
            await connector.ExecuteAsync(
                "g.V(['tenant1','team1']).outE('members').where(__.otherV().hasId('user1')).drop()", 
                null);

            // Query after deletion - should find only user2
            var queryAfter = @"g.V(['tenant1','service1'])
                .emit()
                .repeat(__.out('members').dedup())
                .until(__.loops().is(7))
                .has('entityType', 'profile')
                .dedup()";

            var resultsAfter = await connector.ExecuteAsync(queryAfter, null);
            _output.WriteLine($"Users after deletion: {resultsAfter.Count()}");
            Assert.Equal(1, resultsAfter.Count());
            Assert.Equal("user2", resultsAfter.First().id?.ToString());
        }

        [Fact]
        public async Task ComplexTraversal_VerifyEdgeIndexConsistency()
        {
            // Verify that edge indices stay consistent after deletion
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["pk"] = "tenant1"
            }, "service1");

            var profile1 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile1");

            var profile2 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile2");

            db.AddEdge("members", "service1", "profile1", null, "e1");
            db.AddEdge("members", "service1", "profile2", null, "e2");

            // Verify initial indices
            var outEdgesBefore = db.GetOutEdges("service1", "members");
            var edgesByLabelBefore = db.GetEdgesByLabel("members");
            
            _output.WriteLine($"OutEdges before: {outEdgesBefore.Count()}");
            _output.WriteLine($"EdgesByLabel before: {edgesByLabelBefore.Count()}");
            
            Assert.Equal(2, outEdgesBefore.Count());
            Assert.Equal(2, edgesByLabelBefore.Count());

            // Delete edge
            await connector.ExecuteAsync(
                "g.E('e1').drop()", 
                null);

            // Verify indices after deletion
            var outEdgesAfter = db.GetOutEdges("service1", "members");
            var edgesByLabelAfter = db.GetEdgesByLabel("members");
            
            _output.WriteLine($"OutEdges after: {outEdgesAfter.Count()}");
            _output.WriteLine($"EdgesByLabel after: {edgesByLabelAfter.Count()}");
            
            Assert.Single(outEdgesAfter);
            Assert.Single(edgesByLabelAfter);

            // Now verify traversal uses these indices correctly
            var traversalResult = await connector.ExecuteAsync(
                "g.V(['tenant1','service1']).out('members')", 
                null);
            
            _output.WriteLine($"Traversal result: {traversalResult.Count()}");
            Assert.Single(traversalResult);
            Assert.Equal("profile2", traversalResult.First().id?.ToString());
        }

        [Fact]
        public async Task ComplexTraversal_AddEdgeAfterDeletion_IsDiscoverable()
        {
            // Verify newly added edges are discoverable by complex traversal
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", new Dictionary<string, object>
            {
                ["pk"] = "tenant1"
            }, "service1");

            var profile1 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile1");

            var profile2 = db.AddVertex("profile", new Dictionary<string, object>
            {
                ["entityType"] = "profile",
                ["pk"] = "tenant1"
            }, "profile2");

            db.AddEdge("members", "service1", "profile1", null, "e1");

            // Initial query - should find 1
            var query = @"g.V(['tenant1','service1'])
                .emit()
                .repeat(__.out('members').dedup())
                .until(__.loops().is(7))
                .has('entityType', 'profile')
                .dedup()";

            var results1 = await connector.ExecuteAsync(query, null);
            _output.WriteLine($"Initial members: {results1.Count()}");
            Assert.Single(results1);

            // Add new edge
            db.AddEdge("members", "service1", "profile2", null, "e2");

            // Query after adding - should find 2
            var results2 = await connector.ExecuteAsync(query, null);
            _output.WriteLine($"After adding edge: {results2.Count()}");
            Assert.Equal(2, results2.Count());

            // Delete first edge
            await connector.ExecuteAsync("g.E('e1').drop()", null);

            // Query after deletion - should find 1 (profile2)
            var results3 = await connector.ExecuteAsync(query, null);
            _output.WriteLine($"After deletion: {results3.Count()}");
            Assert.Single(results3);
            Assert.Equal("profile2", results3.First().id?.ToString());
        }
    }
}
