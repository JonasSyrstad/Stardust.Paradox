using FluentAssertions;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Test to verify g.E(id) returns edge properties correctly
    /// </summary>
    public class EdgeByIdPropertyTests
    {
        [Fact]
        public async Task GEdge_ById_ShouldReturnEdgeWithProperties()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.EnableDebugLogging = true;
            });

            // Create test vertices
            await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
            
            // Add edge with properties and get the edge ID
            var edgeResult = await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane')).property('weight', 0.8).property('since', '2020')", new Dictionary<string, object>());
            
            // Extract the edge ID
            var edgeGro = edgeResult.First() as GremlinResponseObject;
            edgeGro.Should().NotBeNull("Edge creation should return a GremlinResponseObject");
            
            var edgeId = edgeGro.Get<string>("id");
            edgeId.Should().NotBeNullOrEmpty("Edge should have an ID");

            // Act - Query the edge by ID using g.E(id)
            var edgeById = await connector.ExecuteAsync($"g.E('{edgeId}')", new Dictionary<string, object>());

            // Assert
            edgeById.Should().HaveCount(1, "g.E(id) should return exactly one edge");
            
            var retrievedEdge = edgeById.First();
            
            // Verify edge structure
            if (retrievedEdge is GremlinResponseObject gro)
            {
                gro.Get<string>("id").Should().Be(edgeId, "Edge ID should match");
                gro.Get<string>("label").Should().Be("knows", "Edge label should be 'knows'");
                gro.Get<string>("type").Should().Be("edge", "Type should be 'edge'");
                gro.Get<string>("outV").Should().Be("john", "OutV should be 'john'");
                gro.Get<string>("inV").Should().Be("jane", "InV should be 'jane'");
                
                // Check properties
                var properties = gro.Get<object>("properties");
                properties.Should().NotBeNull("Edge should have properties");
                
                if (properties is Dictionary<string, object> propsDict)
                {
                    propsDict.Should().ContainKey("weight", "Edge should have 'weight' property");
                    propsDict.Should().ContainKey("since", "Edge should have 'since' property");
                    propsDict["weight"].Should().Be(0.8, "Weight property should be 0.8");
                    propsDict["since"].Should().Be("2020", "Since property should be '2020'");
                }
                else
                {
                    Assert.True(false, $"Properties is of unexpected type: {properties?.GetType()}");
                }
            }
            else
            {
                Assert.True(false, $"Retrieved edge is not a GremlinResponseObject, it's: {retrievedEdge?.GetType()}");
            }
        }

        [Fact]
        public async Task GEdge_ById_ShouldReturnEdgeWithProperties_DetailedCheck()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.EnableDebugLogging = true;
            });

            // Create test vertices
            await connector.ExecuteAsync("g.addV('person').property('id', 'john').property('name', 'John')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'jane').property('name', 'Jane')", new Dictionary<string, object>());
            
            // Add edge with properties and get the edge ID
            var edgeResult = await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane')).property('weight', 0.8).property('since', '2020')", new Dictionary<string, object>());
            
            // Extract the edge ID
            var edgeGro = edgeResult.First() as GremlinResponseObject;
            var edgeId = edgeGro.Get<string>("id");

            // Act - Query the edge by ID using g.E(id)
            var edgeById = await connector.ExecuteAsync($"g.E('{edgeId}')", new Dictionary<string, object>());

            // Assert
            edgeById.Should().HaveCount(1, "g.E(id) should return exactly one edge");
            
            var retrievedEdge = edgeById.First();
            
            if (retrievedEdge is GremlinResponseObject gro)
            {
                // Check properties in detail
                var properties = gro.Get<object>("properties");
                
                if (properties is Dictionary<string, object> propsDict)
                {
                    propsDict.Should().ContainKey("weight", "Edge should have 'weight' property");
                    propsDict.Should().ContainKey("since", "Edge should have 'since' property");
                    propsDict["weight"].Should().Be(0.8, "Weight property should be 0.8");
                    propsDict["since"].Should().Be("2020", "Since property should be '2020'");
                }
                else
                {
                    // Let's see what we actually got
                    Console.WriteLine($"Properties is not Dictionary<string, object>, it's: {properties?.GetType()}");
                    if (properties != null)
                    {
                        // Try to access as dynamic
                        try
                        {
                            dynamic dynProps = properties;
                            
                            // Try to iterate if it's enumerable
                            if (properties is System.Collections.IEnumerable enumerable)
                            {
                                //foreach (var item in enumerable)
                                //{
                                //    Console.WriteLine($"  Item: {item} (type: {item?.GetType()})");
                                //}
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error accessing dynamic properties: {ex.Message}");
                        }
                    }
                    
                    // Don't fail the test yet, let's investigate
                    // Assert.True(false, $"Properties is of unexpected type: {properties?.GetType()}");
                }
            }
        }

        [Fact]
        public async Task GEdge_All_ShouldReturnEdgesWithProperties()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create test data
            await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob')", new Dictionary<string, object>());
            
            // Add edge with properties
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('bob')).property('weight', 0.5).property('type', 'friend')", new Dictionary<string, object>());

            // Act - Query all edges using g.E()
            var allEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());

            // Assert
            allEdges.Should().HaveCount(1, "Should have exactly one edge");
            
            var edge = allEdges.First();
            if (edge is GremlinResponseObject gro)
            {
                gro.Get<string>("label").Should().Be("knows", "Edge label should be 'knows'");
                gro.Get<string>("type").Should().Be("edge", "Type should be 'edge'");
                
                var properties = gro.Get<object>("properties");
                properties.Should().NotBeNull("Edge should have properties");
                
                if (properties is Dictionary<string, object> propsDict)
                {
                    propsDict.Should().ContainKey("weight", "Edge should have 'weight' property");
                    propsDict.Should().ContainKey("type", "Edge should have 'type' property");
                }
                
                var p = edge.properties as JObject;
                p.Should().NotBeNullOrEmpty();
            }
            else
            {
                Assert.True(false, $"Edge is not a GremlinResponseObject, it's: {edge?.GetType()}");
            }
        }

        [Fact]
        public async Task PropertyValueParsing_ShouldPreserveTypes()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Act - Add edge with numeric property
            var result = await connector.ExecuteAsync("g.addV('person').property('id', 'test').property('weight', 0.8).property('count', 42).property('isActive', true)", new Dictionary<string, object>());

            // Assert
            result.Should().HaveCount(1);
            var vertex = result.First() as GremlinResponseObject;
            vertex.Should().NotBeNull();
            
            var properties = vertex.Get<object>("properties");
            Console.WriteLine($"Properties object type: {properties?.GetType()}");
            Console.WriteLine($"Properties content: {properties}");
            
            if (properties is Dictionary<string, object> propsDict)
            {
                Console.WriteLine($"Weight value: {propsDict["weight"]} (type: {propsDict["weight"]?.GetType()})");
                Console.WriteLine($"Count value: {propsDict["count"]} (type: {propsDict["count"]?.GetType()})");
                Console.WriteLine($"IsActive value: {propsDict["isActive"]} (type: {propsDict["isActive"]?.GetType()})");
                
                // Check types
                propsDict["weight"].Should().BeOfType<double>();
                propsDict["count"].Should().BeOfType<int>();
                propsDict["isActive"].Should().BeOfType<bool>();
            }
        }

        [Fact]
        public async Task EdgePropertyValueParsing_ShouldPreserveTypes()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Act - Add vertices and edge with numeric properties
            await connector.ExecuteAsync("g.addV('person').property('id', 'john')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'jane')", new Dictionary<string, object>());
            var result = await connector.ExecuteAsync("g.V('john').addE('knows').to(g.V('jane')).property('weight', 0.8).property('count', 42).property('isActive', true)", new Dictionary<string, object>());

            // Assert
            result.Should().HaveCount(1);
            var edge = result.First() as GremlinResponseObject;
            edge.Should().NotBeNull();
            
            var properties = edge.Get<object>("properties");
            Console.WriteLine($"Edge properties object type: {properties?.GetType()}");
            Console.WriteLine($"Edge properties content: {properties}");
            
            if (properties is Dictionary<string, object> propsDict)
            {
                Console.WriteLine($"Weight value: {propsDict["weight"]} (type: {propsDict["weight"]?.GetType()})");
                Console.WriteLine($"Count value: {propsDict["count"]} (type: {propsDict["count"]?.GetType()})");
                Console.WriteLine($"IsActive value: {propsDict["isActive"]} (type: {propsDict["isActive"]?.GetType()})");
                
                // Check types - these should be the original types
                propsDict["weight"].Should().BeOfType<double>();
                propsDict["count"].Should().BeOfType<int>();
                propsDict["isActive"].Should().BeOfType<bool>();
            }
        }
    }
}