using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class DebugEdgeTest
    {
        private readonly ITestOutputHelper _output;

        public DebugEdgeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestBasicEdgeCreation()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Create two vertices first
            _output.WriteLine("Creating vertices...");
            var v1Result = await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Jonas')", new Dictionary<string, object>());
            var v2Result = await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('name', 'Kine')", new Dictionary<string, object>());
            
            _output.WriteLine($"V1 result: {v1Result.FirstOrDefault()}");
            _output.WriteLine($"V2 result: {v2Result.FirstOrDefault()}");
            
            // Try creating an edge
            _output.WriteLine("Creating edge...");
            var edgeResult = await connector.ExecuteAsync("g.V('v1').addE('parent').to(g.V('v2'))", new Dictionary<string, object>());
            
            _output.WriteLine($"Edge result: {edgeResult.FirstOrDefault()}");
            
            // Verify edge exists
            _output.WriteLine("Checking edge exists...");
            var edgeCheckResult = await connector.ExecuteAsync("g.V('v2').in('parent')", new Dictionary<string, object>());
            
            _output.WriteLine($"Edge check result: {edgeCheckResult.FirstOrDefault()}");
            
            Assert.NotNull(edgeCheckResult.FirstOrDefault());
        }

        [Fact]
        public async Task TestComplexEdgeQuery()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Create vertices
            await connector.ExecuteAsync("g.addV('person').property('id', 'fromVertex').property('name', 'From')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'toVertex').property('name', 'To')", new Dictionary<string, object>());
            
            // Try the complex query pattern that the Edge class generates
            _output.WriteLine("Testing complex edge creation pattern...");
            var complexResult = await connector.ExecuteAsync("g.V('fromVertex').as('t').V('toVertex').as('s').addE('testRelation').property('id', 'edge123').from('s').to('t')", new Dictionary<string, object>());
            
            _output.WriteLine($"Complex edge result: {complexResult.FirstOrDefault()}");
            
            // Verify
            var verifyResult = await connector.ExecuteAsync("g.V('toVertex').out('testRelation')", new Dictionary<string, object>());
            _output.WriteLine($"Verify result: {verifyResult.FirstOrDefault()}");
            
            Assert.NotNull(verifyResult.FirstOrDefault());
        }

        [Fact]
        public async Task DebugComplexQueryStepByStep()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Create vertices
            _output.WriteLine("=== Creating test vertices ===");
            await connector.ExecuteAsync("g.addV('person').property('id', 'fromVertex').property('name', 'From')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'toVertex').property('name', 'To')", new Dictionary<string, object>());
            
            // Test each part of the complex query
            _output.WriteLine("\n=== Testing V('fromVertex').as('t') ===");
            var step1 = await connector.ExecuteAsync("g.V('fromVertex').as('t')", new Dictionary<string, object>());
            _output.WriteLine($"Step 1 result: {step1.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Testing V('fromVertex').as('t').V('toVertex') ===");
            var step2 = await connector.ExecuteAsync("g.V('fromVertex').as('t').V('toVertex')", new Dictionary<string, object>());
            _output.WriteLine($"Step 2 result: {step2.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Testing V('fromVertex').as('t').V('toVertex').as('s') ===");
            var step3 = await connector.ExecuteAsync("g.V('fromVertex').as('t').V('toVertex').as('s')", new Dictionary<string, object>());
            _output.WriteLine($"Step 3 result: {step3.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Testing addE('testRelation') ===");
            var step4 = await connector.ExecuteAsync("g.V('fromVertex').as('t').V('toVertex').as('s').addE('testRelation')", new Dictionary<string, object>());
            _output.WriteLine($"Step 4 result: {step4.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Testing with property ===");
            var step5 = await connector.ExecuteAsync("g.V('fromVertex').as('t').V('toVertex').as('s').addE('testRelation').property('id', 'edge123')", new Dictionary<string, object>());
            _output.WriteLine($"Step 5 result: {step5.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Testing with from modulator ===");
            var step6 = await connector.ExecuteAsync("g.V('fromVertex').as('t').V('toVertex').as('s').addE('testRelation').property('id', 'edge123').from('s')", new Dictionary<string, object>());
            _output.WriteLine($"Step 6 result: {step6.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Testing complete query ===");
            var step7 = await connector.ExecuteAsync("g.V('fromVertex').as('t').V('toVertex').as('s').addE('testRelation').property('id', 'edge123').from('s').to('t')", new Dictionary<string, object>());
            _output.WriteLine($"Step 7 result: {step7.FirstOrDefault()}");
            
            // Verify all edges in database
            _output.WriteLine("\n=== All edges in database ===");
            var allEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            foreach (var edge in allEdges)
            {
                _output.WriteLine($"Edge: {edge}");
            }
            
            // Verify traversal
            _output.WriteLine("\n=== Verify toVertex -> out('testRelation') ===");
            var verify1 = await connector.ExecuteAsync("g.V('toVertex').out('testRelation')", new Dictionary<string, object>());
            _output.WriteLine($"toVertex out result: {verify1.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Verify fromVertex -> in('testRelation') ===");
            var verify2 = await connector.ExecuteAsync("g.V('fromVertex').in('testRelation')", new Dictionary<string, object>());
            _output.WriteLine($"fromVertex in result: {verify2.FirstOrDefault()}");
        }

        [Fact]
        public async Task TestEdgeClassPattern()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Create vertices
            _output.WriteLine("=== Creating test vertices ===");
            await connector.ExecuteAsync("g.addV('person').property('id', 'Jonas').property('name', 'Jonas')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'Tor').property('name', 'Tor')", new Dictionary<string, object>());
            
            // Test the exact pattern generated by Edge<T>.CreateAddEdgeExpression
            // From CreateAddEdgeExpression: g.V(FromId).as('t').V(ToId).as('s').addE(label).property('id', guid).from('s').to('t')
            // Where: FromId = parent (Jonas), ToId = vertex (Tor)
            // This means: Jonas.Parents.Add(Tor) should create edge from Tor to Jonas
            _output.WriteLine("\n=== Testing Edge class pattern: Jonas.Parents.Add(Tor) ===");
            var edgeQuery = "g.V('Jonas').as('t').V('Tor').as('s').addE('parent').property('id', 'edge123').from('s').to('t')";
            _output.WriteLine($"Query: {edgeQuery}");
            
            var result = await connector.ExecuteAsync(edgeQuery, new Dictionary<string, object>());
            _output.WriteLine($"Result: {result.FirstOrDefault()}");
            
            // Verify all edges
            _output.WriteLine("\n=== All edges in database ===");
            var allEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            foreach (var edge in allEdges)
            {
                _output.WriteLine($"Edge: {edge}");
            }
            
            // Test parent relationship: Jonas should have Tor as parent
            _output.WriteLine("\n=== Testing Jonas.Parents (should find Tor) ===");
            var jonasParents = await connector.ExecuteAsync("g.V('Jonas').in('parent')", new Dictionary<string, object>());
            foreach (var parent in jonasParents)
            {
                _output.WriteLine($"Jonas parent: {parent}");
            }
            
            // Test child relationship: Tor should have Jonas as child
            _output.WriteLine("\n=== Testing Tor.Children (should find Jonas) ===");
            var torChildren = await connector.ExecuteAsync("g.V('Tor').out('parent')", new Dictionary<string, object>());
            foreach (var child in torChildren)
            {
                _output.WriteLine($"Tor child: {child}");
            }

            Assert.NotNull(jonasParents.FirstOrDefault());
        }
    }
}