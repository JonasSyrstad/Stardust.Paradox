using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Tests.CosmosDbMigrated;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class EdgePersistenceDebugTest
    {
        private readonly ITestOutputHelper _output;

        public EdgePersistenceDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DirectEdgeCreationTest()
        {
            var options = new InMemoryDatabaseOptions { EnableDebugLogging = true, EnableQueryLogging = true };
            var connector = new InMemoryGremlinLanguageConnector(options);
            
            _output.WriteLine("=== Direct Edge Creation Test ===");
            
            // Create vertices directly
            _output.WriteLine("Creating vertices...");
            await connector.ExecuteAsync("g.addV('person').property('id', 'Jonas')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'Tor')", new Dictionary<string, object>());
            
            // Check vertices exist
            var jonasResult = await connector.ExecuteAsync("g.V('Jonas')", new Dictionary<string, object>());
            var torResult = await connector.ExecuteAsync("g.V('Tor')", new Dictionary<string, object>());
            _output.WriteLine($"Jonas exists: {jonasResult.Any()}");
            _output.WriteLine($"Tor exists: {torResult.Any()}");
            
            // Try the exact query pattern from Edge<T> class
            _output.WriteLine("Executing Edge<T> pattern...");
            var edgeQuery = "g.V('Jonas').as('t').V('Tor').as('s').addE('parent').property('id', 'edge123').from('s').to('t')";
            _output.WriteLine($"Query: {edgeQuery}");
            
            var edgeResult = await connector.ExecuteAsync(edgeQuery, new Dictionary<string, object>());
            _output.WriteLine($"Edge creation result: {edgeResult.FirstOrDefault()}");
            
            // Check if edge was created
            var allEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            _output.WriteLine($"Total edges in database: {allEdges.Count()}");
            foreach (var edge in allEdges)
            {
                _output.WriteLine($"Edge: {edge}");
            }
            
            // Check specific edge traversal
            var parentCheck = await connector.ExecuteAsync("g.V('Jonas').in('parent')", new Dictionary<string, object>());
            _output.WriteLine($"Jonas parents: {parentCheck.Count()}");
            foreach (var parent in parentCheck)
            {
                _output.WriteLine($"Parent: {parent}");
            }
            
            Assert.True(allEdges.Any(), "Expected at least one edge to be created");
        }

        [Fact]
        public async Task HighLevelEdgeCollectionTest()
        {
            _output.WriteLine("=== High-Level EdgeCollection Test ===");
            
            var options = new InMemoryDatabaseOptions { EnableDebugLogging = true, EnableQueryLogging = true };
            var connector = new InMemoryGremlinLanguageConnector(options);
            
            using (var tc = new TestContext(connector))
            {
                _output.WriteLine("Creating entities...");
                var jonas = tc.CreateEntity<IProfile>("Jonas");
                jonas.Name = "Jonas";
                jonas.Pk = "Jonas";
                
                var tor = tc.CreateEntity<IProfile>("Tor");
                tor.Name = "Tor";
                tor.Pk = "Tor";
                
                await tc.SaveChangesAsync();
                _output.WriteLine("Entities saved");
                
                // Add parent relationship
                _output.WriteLine("Adding parent relationship...");
                jonas.Parents.Add(tor);
                
                _output.WriteLine("Calling SaveChangesAsync...");
                await tc.SaveChangesAsync();
                _output.WriteLine("SaveChangesAsync completed");
                
                // Check if it worked
                _output.WriteLine("Checking relationship...");
                var parents = await jonas.Parents.ToVerticesAsync();
                _output.WriteLine($"Jonas has {parents.Count()} parents");
                
                Assert.NotEmpty(parents);
            }
        }
    }
}