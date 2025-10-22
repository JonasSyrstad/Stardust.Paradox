using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;
using Stardust.Paradox.Data.InMemory.Scenarios;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Test to reproduce the AddMember_AfterRemovingMember_AllowsAddingUpToLimit issue
    /// Based on the query log from AddMember_AfterRemovingMember_AllowsAddingUpToLimit.json
    /// </summary>
    public class AddMemberAfterRemovingMemberTest
    {
        private readonly ITestOutputHelper _output;
        private readonly InMemoryGraphDatabase _database;
        private readonly TinkerGraphQueryParser _parser;

        public AddMemberAfterRemovingMemberTest(ITestOutputHelper output)
        {
            _output = output;
            _database = new InMemoryGraphDatabase();
            _parser = new TinkerGraphQueryParser(_database);
        }

        [Fact]
        public void ReproduceQuerySequenceFromJsonDump()
        {
            // This test reproduces the exact query sequence from the JSON dump
            // to identify why the last query returns 0 results when it should return 2

            var tenantId = "550e8401-e29b-41d4-a716-446655440001";
            var serviceId = "e9b18469-f317-4c4b-abe8-b349ca13cd45";
            var profile1Id = "9c3e2103-4c9c-4751-acd5-97a0694af6da";
            var profile2Id = "d87e8bae-f662-4af9-9012-cd2ac51bcdb3";

            // Query 1: Create service entity (timestamp: 2025-10-21T12:05:27.2368771Z)
            var q1 = "g.addV(__label).property('id',___ekey).property('pk',__pk).property('createDateTime',__createDateTime)" +
                     ".property('entityType',__entityType).property('serviceId',__serviceId)" +
                     ".property('autoAssignSubscription',__autoAssignSubscription).property('numberOfLicenses',__numberOfLicenses)" +
                     ".property('production',__production).property('prefix',__prefix).property('subscriptionCap',__subscriptionCap)" +
                     ".property('managementMode',__managementMode).property('accessLevels',__accessLevels)" +
                     ".property('state',__state).property('useApplyForFlow',__useApplyForFlow)" +
                     ".property('name',__name).property('modifiedDateTime',__modifiedDateTime)";
            
            var p1 = new Dictionary<string, object>
            {
                ["__pk"] = tenantId,
                ["__createDateTime"] = 1761048327,
                ["__entityType"] = "tenantService",
                ["__serviceId"] = "ab511a22-bcae-43dc-b5e9-c67d2fb6647f",
                ["__autoAssignSubscription"] = "false",
                ["__numberOfLicenses"] = "2",
                ["__production"] = "false",
                ["__prefix"] = "TEST",
                ["__subscriptionCap"] = "0",
                ["__managementMode"] = "legacy",
                ["__accessLevels"] = "read,write,admin",
                ["__state"] = "active",
                ["__useApplyForFlow"] = "false",
                ["__name"] = "Mock Test Service",
                ["__modifiedDateTime"] = 1761048327,
                ["___ekey"] = serviceId,
                ["__label"] = "tenantEntity"
            };

            var result1 = _parser.ParseAndExecute(q1, p1).ToList();
            _output.WriteLine($"Query 1 created {result1.Count} vertex (service)");
            Assert.Single(result1);

            // Verify service was created
            var service = _database.GetVertex(serviceId);
            Assert.NotNull(service);
            Assert.Equal("tenantService", service.GetProperty<string>("entityType"));
            Assert.Equal("2", service.GetProperty<string>("numberOfLicenses"));

            // Query 2: Create first profile (timestamp: 2025-10-21T12:05:27.4976252Z)
            var q2 = "g.addV(__label).property('id',___ekey).property('pk',__pk).property('createDateTime',__createDateTime)" +
                     ".property('entityType',__entityType).property('email',__email).property('principalId',__principalId)" +
                     ".property('isServicePrincipal',__isServicePrincipal).property('name',__name)" +
                     ".property('modifiedDateTime',__modifiedDateTime)";
            
            var p2 = new Dictionary<string, object>
            {
                ["__pk"] = tenantId,
                ["__createDateTime"] = 1761048327,
                ["__entityType"] = "profile",
                ["__email"] = "test@veracity.com",
                ["__principalId"] = "02fe0f0a-8891-4e9f-b43c-3a515a1238f8",
                ["__isServicePrincipal"] = "false",
                ["__name"] = "Test User",
                ["__modifiedDateTime"] = 1761048327,
                ["___ekey"] = profile1Id,
                ["__label"] = "tenantEntity"
            };

            var result2 = _parser.ParseAndExecute(q2, p2).ToList();
            _output.WriteLine($"Query 2 created {result2.Count} vertex (profile1)");
            Assert.Single(result2);

            // Query 3: Create member edges for profile1 (timestamp: 2025-10-21T12:05:27.8266128Z - memberOf)
            var q3 = "g.V(inVId).as('a').V(outVID).as('b').addE(___label).from('b').to('a')" +
                     ".property('id',___ekey).property('memberType',__memberType).property('name',__name)" +
                     ".property('createDateTime',__createDateTime).property('userId',__userId)" +
                     ".property('createdBy',__createdBy).property('addedDate',__addedDate)" +
                     ".property('email',__email).property('isServicePrincipal',__isServicePrincipal)" +
                     ".property('nullLicense',__nullLicense).property('accessLevel',__accessLevel)" +
                     ".property('isAdmin',__isAdmin)";
            
            var p3 = new Dictionary<string, object>
            {
                ["__memberType"] = "profileSubscription",
                ["__name"] = "Test User",
                ["__createDateTime"] = 1761048327,
                ["__userId"] = "02fe0f0a-8891-4e9f-b43c-3a515a1238f8",
                ["__createdBy"] = "550e8400-e29b-41d4-a716-446655440000",
                ["__addedDate"] = 1760997600,
                ["__email"] = "test@veracity.com",
                ["__isServicePrincipal"] = "false",
                ["__nullLicense"] = false,
                ["__accessLevel"] = "read",
                ["__isAdmin"] = false,
                ["inVId"] = serviceId,
                ["outVID"] = profile1Id,
                ["___label"] = "memberOf",
                ["___ekey"] = $"memberOf{serviceId}{profile1Id}"
            };

            var result3 = _parser.ParseAndExecute(q3, p3).ToList();
            _output.WriteLine($"Query 3 created {result3.Count} edge (profile1 -> memberOf -> service)");
            Assert.Single(result3);

            // Query 4: Create members edge for profile1 (timestamp: 2025-10-21T12:05:27.8575268Z - members)
            var p4 = new Dictionary<string, object>
            {
                ["__memberType"] = "profileSubscription",
                ["__name"] = "Test User",
                ["__createDateTime"] = 1761048327,
                ["__userId"] = "02fe0f0a-8891-4e9f-b43c-3a515a1238f8",
                ["__createdBy"] = "550e8400-e29b-41d4-a716-446655440000",
                ["__addedDate"] = 1760997600,
                ["__email"] = "test@veracity.com",
                ["__isServicePrincipal"] = "false",
                ["__nullLicense"] = false,
                ["__accessLevel"] = "read",
                ["__isAdmin"] = false,
                ["inVId"] = profile1Id,
                ["outVID"] = serviceId,
                ["___label"] = "members",
                ["___ekey"] = $"members{profile1Id}{serviceId}"
            };

            var result4 = _parser.ParseAndExecute(q3, p4).ToList();
            _output.WriteLine($"Query 4 created {result4.Count} edge (service -> members -> profile1)");
            Assert.Single(result4);

            // DEBUG: Export internal structures after edge creation
            _output.WriteLine("\n=== INTERNAL STRUCTURES AFTER EDGE CREATION ===");
            var structures = _database.ExportInternalStructures();
            var outVertexIndex = structures["outVertexIndex"] as Dictionary<string, Dictionary<string, List<string>>>;
            if (outVertexIndex != null && outVertexIndex.ContainsKey(serviceId))
            {
                _output.WriteLine($"OutVertexIndex for service {serviceId}:");
                foreach (var labelKvp in outVertexIndex[serviceId])
                {
                    _output.WriteLine($"  Label '{labelKvp.Key}': {labelKvp.Value.Count} vertices");
                    foreach (var vId in labelKvp.Value)
                    {
                        _output.WriteLine($"    - {vId}");
                    }
                }
            }
            else
            {
                _output.WriteLine($"OutVertexIndex does not contain service {serviceId}");
            }

            // DEBUG: Check all edges in database
            _output.WriteLine("\n=== DEBUG: All edges in database ===");
            var allEdges = _database.GetAllEdges().ToList();
            _output.WriteLine($"Total edges: {allEdges.Count}");
            foreach (var edge in allEdges)
            {
                _output.WriteLine($"  Edge ID: {edge.Id}, Label: {edge.Label}, From: {edge.OutVertexId} -> To: {edge.InVertexId}");
            }

            // DEBUG: Check what V([__p0,__p1]) returns
            _output.WriteLine("\n=== DEBUG: Testing V([__p0,__p1]) ===");
            var qDebugV = "g.V(__p0,__p1)";
            var pDebugV = new Dictionary<string, object>
            {
                ["__p0"] = tenantId,
                ["__p1"] = serviceId
            };
            var debugV = _parser.ParseAndExecute(qDebugV, pDebugV).ToList();
            _output.WriteLine($"V(__p0,__p1) returned {debugV.Count} results");
            foreach (var item in debugV)
            {
                _output.WriteLine($"  {item}");
            }

            // DEBUG: Check what out('members') returns
            _output.WriteLine("\n=== DEBUG: Testing V(__p0,__p1).out('members') ===");
            var qDebugOut = "g.V(__p0,__p1).out(__p4)";
            var pDebugOut = new Dictionary<string, object>
            {
                ["__p0"] = tenantId,
                ["__p1"] = serviceId,
                ["__p4"] = "members"
            };
            var debugOut = _parser.ParseAndExecute(qDebugOut, pDebugOut).ToList();
            _output.WriteLine($"V(__p0,__p1).out('members') returned {debugOut.Count} results");
            foreach (var item in debugOut)
            {
                _output.WriteLine($"  {item}");
            }

            // DEBUG: Check outgoing edges from service
            _output.WriteLine("\n=== DEBUG: Checking outgoing edges from service ===");
            var outEdges = _database.GetOutEdges(serviceId).ToList();
            _output.WriteLine($"Service {serviceId} has {outEdges.Count} outgoing edges");
            foreach (var edge in outEdges)
            {
                _output.WriteLine($"  Edge: {edge.Label} to {edge.InVertexId}");
            }
            
            var membersEdges = _database.GetOutEdges(serviceId, "members").ToList();
            _output.WriteLine($"Service {serviceId} has {membersEdges.Count} outgoing 'members' edges");
            foreach (var edge in membersEdges)
            {
                _output.WriteLine($"  Edge: {edge.Label} to {edge.InVertexId}");
            }

            // DEBUG: Check what emit().repeat() returns
            _output.WriteLine("\n=== DEBUG: Testing V(__p0,__p1).emit().repeat(out('members').dedup()).until(loops().is(7)) ===");
            var qDebugEmit = "g.V(__p0,__p1).emit().repeat(out(__p4).dedup()).until(loops().is(__p5))";
            var pDebugEmit = new Dictionary<string, object>
            {
                ["__p0"] = tenantId,
                ["__p1"] = serviceId,
                ["__p4"] = "members",
                ["__p5"] = 7
            };
            var debugEmit = _parser.ParseAndExecute(qDebugEmit, pDebugEmit).ToList();
            _output.WriteLine($"emit().repeat() returned {debugEmit.Count} results (expected 2)");
            foreach (var item in debugEmit)
            {
                _output.WriteLine($"  {item}");
            }

            // Query 5: Count members before deletion (timestamp: 2025-10-21T12:05:27.8102055Z)
            var q5 = "g.V(__p0,__p1).emit().repeat(out(__p4).dedup()).until(loops().is(__p5))" +
                     ".has(__p2,__p3).dedup().or(has(__p6,__p7),has(__p8,__p9)).count()";
            
            var p5 = new Dictionary<string, object>
            {
                ["__p0"] = tenantId,
                ["__p1"] = serviceId,
                ["__p2"] = "entityType",
                ["__p3"] = "profile",
                ["__p4"] = "members",
                ["__p5"] = 7,
                ["__p6"] = "isServicePrincipal",
                ["__p7"] = "false",
                ["__p8"] = "isServicePrincipal",
                ["__p9"] = false
            };

            var result5 = _parser.ParseAndExecute(q5, p5).ToList();
            _output.WriteLine($"Query 5 returned count: {result5.FirstOrDefault()}");
            Assert.Equal(1L, result5.FirstOrDefault());

            // Create second profile
            var q6 = q2;
            var p6 = new Dictionary<string, object>
            {
                ["__pk"] = tenantId,
                ["__createDateTime"] = 1761048327,
                ["__entityType"] = "profile",
                ["__email"] = "test@veracity.com",
                ["__principalId"] = "52353c63-1100-421c-bcbe-a741c3e1f68f",
                ["__isServicePrincipal"] = "false",
                ["__name"] = "Test User",
                ["__modifiedDateTime"] = 1761048327,
                ["___ekey"] = profile2Id,
                ["__label"] = "tenantEntity"
            };

            var result6 = _parser.ParseAndExecute(q6, p6).ToList();
            _output.WriteLine($"Query 6 created {result6.Count} vertex (profile2)");
            Assert.Single(result6);

            // Create edges for profile2
            var p7 = new Dictionary<string, object>
            {
                ["__memberType"] = "profileSubscription",
                ["__name"] = "Test User",
                ["__createDateTime"] = 1761048327,
                ["__userId"] = "52353c63-1100-421c-bcbe-a741c3e1f68f",
                ["__createdBy"] = "550e8400-e29b-41d4-a716-446655440000",
                ["__addedDate"] = 1760997600,
                ["__email"] = "test@veracity.com",
                ["__isServicePrincipal"] = "false",
                ["__nullLicense"] = false,
                ["__accessLevel"] = "read",
                ["__isAdmin"] = false,
                ["inVId"] = profile2Id,
                ["outVID"] = serviceId,
                ["___label"] = "members",
                ["___ekey"] = $"members{profile2Id}{serviceId}"
            };

            var result7 = _parser.ParseAndExecute(q3, p7).ToList();
            _output.WriteLine($"Query 7 created {result7.Count} edge (service -> members -> profile2)");

            var p8 = new Dictionary<string, object>
            {
                ["__memberType"] = "profileSubscription",
                ["__name"] = "Test User",
                ["__createDateTime"] = 1761048327,
                ["__userId"] = "52353c63-1100-421c-bcbe-a741c3e1f68f",
                ["__createdBy"] = "550e8400-e29b-41d4-a716-446655440000",
                ["__addedDate"] = 1760997600,
                ["__email"] = "test@veracity.com",
                ["__isServicePrincipal"] = "false",
                ["__nullLicense"] = false,
                ["__accessLevel"] = "read",
                ["__isAdmin"] = false,
                ["inVId"] = serviceId,
                ["outVID"] = profile2Id,
                ["___label"] = "memberOf",
                ["___ekey"] = $"memberOf{serviceId}{profile2Id}"
            };

            var result8 = _parser.ParseAndExecute(q3, p8).ToList();
            _output.WriteLine($"Query 8 created {result8.Count} edge (profile2 -> memberOf -> service)");

            // Verify we have 2 members now
            var debugAllEdges = _database.GetAllEdges().ToList();
            _output.WriteLine($"Total edges in database: {allEdges.Count}");
            foreach (var edge in allEdges)
            {
                _output.WriteLine($"  Edge: {edge.Label} from {edge.OutVertexId} to {edge.InVertexId}");
            }

            // Now test the final query that's failing
            var q9 = "g.V([__p0,__p1]).emit().repeat(out(__p4).dedup()).until(loops().is(__p5)).has(__p2,__p3).dedup()";
            var p9 = new Dictionary<string, object>
            {
                ["__p0"] = tenantId,
                ["__p1"] = serviceId,
                ["__p2"] = "entityType",
                ["__p3"] = "profile",
                ["__p4"] = "members",
                ["__p5"] = 7
            };

            _output.WriteLine($"\nExecuting final query: {q9}");
            _output.WriteLine($"Parameters: __p0={p9["__p0"]}, __p1={p9["__p1"]}, __p2={p9["__p2"]}, __p3={p9["__p3"]}, __p4={p9["__p4"]}, __p5={p9["__p5"]}");

            var finalResult = _parser.ParseAndExecute(q9, p9).ToList();
            _output.WriteLine($"Query 9 (final) returned {finalResult.Count} results (expected 2)");

            // Debug: Try simpler query to see if basic traversal works
            var simpleQuery = "g.V([__p0,__p1]).out(__p4)";
            var simpleParams = new Dictionary<string, object>
            {
                ["__p0"] = tenantId,
                ["__p1"] = serviceId,
                ["__p4"] = "members"
            };
            var simpleResult = _parser.ParseAndExecute(simpleQuery, simpleParams).ToList();
            _output.WriteLine($"Simple query g.V().out('members') returned {simpleResult.Count} results");

            // Print details of what we got
            foreach (var item in finalResult)
            {
                _output.WriteLine($"  Result item: {item}");
            }

            // THIS IS THE BUG: We expect 2 profiles but get 0
            Assert.Equal(2, finalResult.Count);
        }
    }
}

