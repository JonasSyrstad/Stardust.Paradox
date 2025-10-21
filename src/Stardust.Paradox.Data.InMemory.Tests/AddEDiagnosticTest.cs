using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory.Core;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class AddEDiagnosticTest
    {
        private readonly ITestOutputHelper _output;

        public AddEDiagnosticTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Diagnostic_AddE_Simple()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            var db = connector.Database;

            // Add vertices directly
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            _output.WriteLine("=== Vertices added ===");
            _output.WriteLine($"Alice: {db.GetVertex("alice") != null}");
            _output.WriteLine($"Bob: {db.GetVertex("bob") != null}");

            // Try adding edge via Gremlin
            _output.WriteLine("\n=== Executing: g.V('alice').addE('knows').to(V('bob')) ===");
            try
            {
                var result = await connector.ExecuteAsync("g.V('alice').addE('knows').to(V('bob'))", null);
                _output.WriteLine($"Result count: {result.Count()}");
                foreach (var item in result)
                {
                    _output.WriteLine($"  Result: {item}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"ERROR: {ex.Message}");
                _output.WriteLine($"Stack: {ex.StackTrace}");
            }

            // Check if edge was created
            _output.WriteLine("\n=== Checking database ===");
            var allEdges = db.GetAllEdges();
            _output.WriteLine($"Total edges in database: {allEdges.Count()}");
            foreach (var edge in allEdges)
            {
                _output.WriteLine($"  Edge: {edge.Id} ({edge.Label}): {edge.OutVertexId} -> {edge.InVertexId}");
            }

            var outEdges = db.GetOutEdges("alice");
            _output.WriteLine($"Out edges from alice: {outEdges.Count()}");
            foreach (var edge in outEdges)
            {
                _output.WriteLine($"  Edge: {edge.Id} ({edge.Label}): {edge.OutVertexId} -> {edge.InVertexId}");
            }
        }

        [Fact]
        public void Diagnostic_AddE_Direct()
        {
            var db = new InMemoryGraphDatabase();

            // Add vertices
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Alice" }, "alice");
            db.AddVertex("person", new Dictionary<string, object> { ["name"] = "Bob" }, "bob");

            _output.WriteLine("=== Vertices added ===");
            _output.WriteLine($"Alice: {db.GetVertex("alice") != null}");
            _output.WriteLine($"Bob: {db.GetVertex("bob") != null}");

            // Add edge directly
            _output.WriteLine("\n=== Adding edge directly via database.AddEdge() ===");
            var edge = db.AddEdge("knows", "alice", "bob", "e1");
            _output.WriteLine($"Edge created: {edge != null}");
            if (edge != null)
            {
                _output.WriteLine($"  ID: {edge.Id}");
                _output.WriteLine($"  Label: {edge.Label}");
                _output.WriteLine($"  From: {edge.OutVertexId}");
                _output.WriteLine($"  To: {edge.InVertexId}");
            }

            // Check database
            _output.WriteLine("\n=== Checking database ===");
            var allEdges = db.GetAllEdges();
            _output.WriteLine($"Total edges: {allEdges.Count()}");

            var outEdges = db.GetOutEdges("alice");
            _output.WriteLine($"Out edges from alice: {outEdges.Count()}");

            var inEdges = db.GetInEdges("bob");
            _output.WriteLine($"In edges to bob: {inEdges.Count()}");
        }
    }
}
