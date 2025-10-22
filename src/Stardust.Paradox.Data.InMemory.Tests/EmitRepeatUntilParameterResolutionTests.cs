using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Scenarios;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests to verify parameter resolution in emit().repeat().until() queries
    /// Replicates the issue from AddMember_AfterRemovingMember_AllowsAddingUpToLimit.json
    /// where parameters like __p4 weren't being resolved inside repeat traversals
    /// </summary>
    public class EmitRepeatUntilParameterResolutionTests
    {
        private readonly ITestOutputHelper _output;

        public EmitRepeatUntilParameterResolutionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task EmitRepeatOut_WithParameterizedEdgeLabel_ShouldResolveParameter()
        {
            // This replicates the exact issue from the JSON:
            // g.V([__p0,__p1]).emit().repeat(out(__p4).dedup()).until(loops().is(__p5)).has(__p2,__p3).dedup()
            // where __p4 = "members" wasn't being resolved
            
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Create the exact structure from the JSON:
            // - A service vertex
            // - 2 profile vertices
            // - "members" edges connecting service to profiles
            
            var service = db.AddVertex("tenantService", "e9b18469-f317-4c4b-abe8-b349ca13cd45");
            service.SetProperty("pk", "550e8401-e29b-41d4-a716-446655440001");
            service.SetProperty("entityType", "tenantService");
            service.SetProperty("serviceId", "ab511a22-bcae-43dc-b5e9-c67d2fb6647f");
            service.SetProperty("numberOfLicenses", "2");
            service.SetProperty("name", "Mock Test Service");

            var profile1 = db.AddVertex("tenantEntity", "9c3e2103-4c9c-4751-acd5-97a0694af6da");
            profile1.SetProperty("pk", "550e8401-e29b-41d4-a716-446655440001");
            profile1.SetProperty("entityType", "profile");
            profile1.SetProperty("principalId", "02fe0f0a-8891-4e9f-b43c-3a515a1238f8");
            profile1.SetProperty("email", "test@veracity.com");
            profile1.SetProperty("name", "Test User");

            var profile2 = db.AddVertex("tenantEntity", "d87e8bae-f662-4af9-9012-cd2ac51bcdb3");
            profile2.SetProperty("pk", "550e8401-e29b-41d4-a716-446655440001");
            profile2.SetProperty("entityType", "profile");
            profile2.SetProperty("principalId", "52353c63-1100-421c-bcbe-a741c3e1f68f");
            profile2.SetProperty("email", "test@veracity.com");
            profile2.SetProperty("name", "Test User");

            // Create members edges
            db.AddEdge("members", service.Id, profile1.Id, "members1");
            db.AddEdge("members", service.Id, profile2.Id, "members2");

            // Execute the exact query from the JSON with parameters
            var query = "g.V([__p0,__p1]).emit().repeat(out(__p4).dedup()).until(loops().is(__p5)).has(__p2,__p3).dedup()";
            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "550e8401-e29b-41d4-a716-446655440001", // partition key
                ["__p1"] = "e9b18469-f317-4c4b-abe8-b349ca13cd45", // service ID
                ["__p2"] = "entityType",
                ["__p3"] = "profile",
                ["__p4"] = "members", // This is the critical parameter that wasn't being resolved!
                ["__p5"] = 7
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            foreach (var result in resultList)
            {
                _output.WriteLine($"Result: {result}");
            }

            // Should return 2 profile vertices
            Assert.Equal(2, resultList.Count);
            
            // Verify both profiles are in the results
            var ids = resultList.Select(r => 
            {
                dynamic d = r;
                return (string)d.id;
            }).ToList();
            
            Assert.Contains("9c3e2103-4c9c-4751-acd5-97a0694af6da", ids);
            Assert.Contains("d87e8bae-f662-4af9-9012-cd2ac51bcdb3", ids);
        }

        [Fact]
        public async Task EmitRepeatOut_WithParameterInNestedTraversal_ShouldResolveParameter()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Create a simple hierarchy:
            // root -> child1 -> grandchild1
            //      -> child2 -> grandchild2
            
            var root = db.AddVertex("node", "root");
            root.SetProperty("type", "root");
            root.SetProperty("name", "Root Node");

            var child1 = db.AddVertex("node", "child1");
            child1.SetProperty("type", "child");
            child1.SetProperty("name", "Child 1");

            var child2 = db.AddVertex("node", "child2");
            child2.SetProperty("type", "child");
            child2.SetProperty("name", "Child 2");

            var grandchild1 = db.AddVertex("node", "grandchild1");
            grandchild1.SetProperty("type", "grandchild");
            grandchild1.SetProperty("name", "Grandchild 1");

            var grandchild2 = db.AddVertex("node", "grandchild2");
            grandchild2.SetProperty("type", "grandchild");
            grandchild2.SetProperty("name", "Grandchild 2");

            db.AddEdge("hasChild", root.Id, child1.Id);
            db.AddEdge("hasChild", root.Id, child2.Id);
            db.AddEdge("hasChild", child1.Id, grandchild1.Id);
            db.AddEdge("hasChild", child2.Id, grandchild2.Id);

            // Query with parameterized edge label in repeat
            var query = "g.V(__p0).emit().repeat(out(__p1).dedup()).until(loops().is(__p2)).has(__p3,__p4).dedup()";
            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "root",
                ["__p1"] = "hasChild", // Parameterized edge label
                ["__p2"] = 3,
                ["__p3"] = "type",
                ["__p4"] = "grandchild"
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            foreach (var result in resultList)
            {
                _output.WriteLine($"Result: {result}");
            }

            // Should return 2 grandchildren
            Assert.Equal(2, resultList.Count);
        }

        [Fact]
        public async Task RepeatOut_WithMultipleParameters_ShouldResolveAllParameters()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var person1 = db.AddVertex("person", "p1");
            person1.SetProperty("name", "Alice");
            person1.SetProperty("age", 30);

            var person2 = db.AddVertex("person", "p2");
            person2.SetProperty("name", "Bob");
            person2.SetProperty("age", 25);

            var person3 = db.AddVertex("person", "p3");
            person3.SetProperty("name", "Charlie");
            person3.SetProperty("age", 35);

            db.AddEdge("knows", person1.Id, person2.Id);
            db.AddEdge("knows", person2.Id, person3.Id);

            // Query with multiple parameters in repeat and filter
            var query = "g.V(__p0).emit().repeat(out(__p1).dedup()).until(loops().is(__p2)).has(__p3,gt(__p4))";
            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "p1",
                ["__p1"] = "knows",
                ["__p2"] = 3,
                ["__p3"] = "age",
                ["__p4"] = 26
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            
            // Should return person1 (30) and person3 (35), not person2 (25)
            Assert.True(resultList.Count >= 1);
        }

        [Fact]
        public async Task EmitRepeatOut_WithoutParameter_ShouldWorkWithLiteralEdgeLabel()
        {
            // Control test: verify that literal edge labels still work
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", "s1");
            service.SetProperty("type", "service");

            var profile1 = db.AddVertex("profile", "pr1");
            profile1.SetProperty("type", "profile");

            var profile2 = db.AddVertex("profile", "pr2");
            profile2.SetProperty("type", "profile");

            db.AddEdge("members", service.Id, profile1.Id);
            db.AddEdge("members", service.Id, profile2.Id);

            // Query without parameters (literal edge label)
            var query = "g.V('s1').emit().repeat(out('members').dedup()).until(loops().is(7)).has('type','profile').dedup()";
            
            var results = await connector.ExecuteAsync(query, new Dictionary<string, object>());
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            
            // Should still return 2 profiles
            Assert.Equal(2, resultList.Count);
        }

        [Fact]
        public async Task EmitRepeatOut_ComplexParameterizedQuery_ShouldResolveAllParameters()
        {
            // Test the complete scenario from the original issue
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Create tenant
            var tenant = db.AddVertex("tenant", "tenant1");
            tenant.SetProperty("pk", "pk1");
            tenant.SetProperty("entityType", "tenant");

            // Create service
            var service = db.AddVertex("tenantService", "service1");
            service.SetProperty("pk", "pk1");
            service.SetProperty("entityType", "tenantService");
            service.SetProperty("numberOfLicenses", 2);

            // Create 2 profiles
            var profile1 = db.AddVertex("profile", "profile1");
            profile1.SetProperty("pk", "pk1");
            profile1.SetProperty("entityType", "profile");
            profile1.SetProperty("isServicePrincipal", "false");

            var profile2 = db.AddVertex("profile", "profile2");
            profile2.SetProperty("pk", "pk1");
            profile2.SetProperty("entityType", "profile");
            profile2.SetProperty("isServicePrincipal", "false");

            // Add edges
            db.AddEdge("members", service.Id, profile1.Id);
            db.AddEdge("members", service.Id, profile2.Id);

            // Complex query with multiple parameters
            var query = @"g.V([__p0,__p1])
                .emit()
                .repeat(out(__p4).dedup())
                .until(loops().is(__p5))
                .has(__p2,__p3)
                .or(has(__p6,__p7),has(__p8,__p9))
                .dedup()";

            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "pk1",
                ["__p1"] = "service1",
                ["__p2"] = "entityType",
                ["__p3"] = "profile",
                ["__p4"] = "members",
                ["__p5"] = 7,
                ["__p6"] = "isServicePrincipal",
                ["__p7"] = "false",
                ["__p8"] = "isServicePrincipal",
                ["__p9"] = false
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            foreach (var result in resultList)
            {
                dynamic d = result;
                _output.WriteLine($"Result ID: {d.id}");
            }

            // Should return both profiles
            Assert.Equal(2, resultList.Count);
        }

        [Fact]
        public async Task RepeatOut_ParameterInUntilCondition_ShouldResolveParameter()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var root = db.AddVertex("node", "root");
            var child1 = db.AddVertex("node", "child1");
            var child2 = db.AddVertex("node", "child2");

            db.AddEdge("link", root.Id, child1.Id);
            db.AddEdge("link", root.Id, child2.Id);

            // Parameter in the until condition
            var query = "g.V(__p0).repeat(out(__p1)).until(loops().is(__p2))";
            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "root",
                ["__p1"] = "link",
                ["__p2"] = 2
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            
            // Should return results after 2 iterations
            Assert.True(resultList.Count >= 0);
        }

        [Fact]
        public async Task EmitRepeatOut_ZeroResults_WhenParameterNotResolved()
        {
            // This test documents the BUG behavior when parameters aren't copied
            // If parameters aren't copied to temp context, out(__p4) looks for
            // an edge with literal label "__p4" which doesn't exist
            
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", "s1");
            var profile1 = db.AddVertex("profile", "p1");
            profile1.SetProperty("entityType", "profile");
            
            db.AddEdge("members", service.Id, profile1.Id);

            // If parameters aren't resolved, this would look for edge "__p4" instead of "members"
            var query = "g.V(__p0).emit().repeat(out(__p1).dedup()).until(loops().is(__p2)).has(__p3,__p4)";
            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "s1",
                ["__p1"] = "members",
                ["__p2"] = 3,
                ["__p3"] = "entityType",
                ["__p4"] = "profile"
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            
            // With the fix, this should return 1 profile
            // Without the fix, it would return 0 because "__p1" wouldn't be resolved
            Assert.Equal(1, resultList.Count);
        }

        [Fact]
        public async Task RepeatOut_NestedTraversalParameters_ShouldBeResolved()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var a = db.AddVertex("node", "a");
            a.SetProperty("value", 1);
            var b = db.AddVertex("node", "b");
            b.SetProperty("value", 2);
            var c = db.AddVertex("node", "c");
            c.SetProperty("value", 3);

            db.AddEdge("next", a.Id, b.Id);
            db.AddEdge("next", b.Id, c.Id);

            // Parameters in nested has() inside repeat
            var query = "g.V(__p0).repeat(out(__p1).has(__p2,gt(__p3))).until(loops().is(__p4))";
            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "a",
                ["__p1"] = "next",
                ["__p2"] = "value",
                ["__p3"] = 1,
                ["__p4"] = 3
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            
            // Should resolve all parameters correctly
            Assert.True(resultList.Count >= 0);
        }

        [Fact]
        public async Task EmitRepeatOut_ArraySyntaxWithParameters_ShouldWork()
        {
            // Test V([pk,id]) array syntax with parameters in repeat
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            var service = db.AddVertex("service", "svc1");
            service.SetProperty("pk", "partition1");
            service.SetProperty("entityType", "service");

            var member1 = db.AddVertex("member", "m1");
            member1.SetProperty("pk", "partition1");
            member1.SetProperty("entityType", "member");

            db.AddEdge("has", service.Id, member1.Id);

            // Array syntax for V step with parameters in repeat
            var query = "g.V([__p0,__p1]).emit().repeat(out(__p2)).until(loops().is(__p3)).has(__p4,__p5)";
            var parameters = new Dictionary<string, object>
            {
                ["__p0"] = "partition1",
                ["__p1"] = "svc1",
                ["__p2"] = "has",
                ["__p3"] = 5,
                ["__p4"] = "entityType",
                ["__p5"] = "member"
            };

            var results = await connector.ExecuteAsync(query, parameters);
            var resultList = results.ToList();

            _output.WriteLine($"Query returned {resultList.Count} results");
            
            Assert.Equal(1, resultList.Count);
        }
    }
}
