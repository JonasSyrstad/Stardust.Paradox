using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Test for edge property filtering issue
    /// </summary>
    public class EdgePropertyFilterTests
    {
        [Fact]
        public async Task EdgeProperties_ShouldBeReturnedInQueries()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.EnableDebugLogging = true;
            });

            // Create test vertices
            await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
            
            // Add edge with properties
            await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane')).property('weight', 0.8).property('since', '2020')", new Dictionary<string, object>());

            // Act 1: Get all outgoing edges (should include properties)
            var allOutEdges = await connector.ExecuteAsync("g.V('john').outE('knows')", new Dictionary<string, object>());
            
            // Act 2: Get edges with where clause
            var filteredEdges = await connector.ExecuteAsync("g.V('john').outE('knows').where(has('weight', 0.8))", new Dictionary<string, object>());
            
            // Act 3: Alternative has() syntax
            var filteredEdges2 = await connector.ExecuteAsync("g.V('john').outE('knows').has('weight', 0.8)", new Dictionary<string, object>());

            // Assert
            allOutEdges.Should().HaveCount(1);
            
            var edge = allOutEdges.First();
            Console.WriteLine($"Edge result: {edge}");
            Console.WriteLine($"Edge type: {edge.GetType()}");
            
            // Verify edge has properties
            if (edge is GremlinResponseObject gro)
            {
                var properties = gro.Get<object>("properties");
                properties.Should().NotBeNull("Edge should have properties");
                Console.WriteLine($"Properties: {properties}");
            }

            // The where() clause should return the same edge if properties are accessible
            filteredEdges.Should().HaveCount(1, "where() clause should find the edge with weight=0.8");
            filteredEdges2.Should().HaveCount(1, "has() clause should find the edge with weight=0.8");
        }

        [Fact]
        public async Task EdgeProperties_ShouldBeAccessibleInFiltering()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create test data with multiple edges having different weights
            await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'charlie').property('name', 'Charlie')", new Dictionary<string, object>());
            
            // Add edges with different weights
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('bob')).property('weight', 0.8)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('charlie')).property('weight', 0.3)", new Dictionary<string, object>());

            // Act - filter edges by weight
            var heavyEdges = await connector.ExecuteAsync("g.V('alice').outE('knows').has('weight', 0.8)", new Dictionary<string, object>());
            var lightEdges = await connector.ExecuteAsync("g.V('alice').outE('knows').has('weight', 0.3)", new Dictionary<string, object>());

            // Assert
            heavyEdges.Should().HaveCount(1, "Should find exactly one edge with weight 0.8");
            lightEdges.Should().HaveCount(1, "Should find exactly one edge with weight 0.3");
        }
    }
}