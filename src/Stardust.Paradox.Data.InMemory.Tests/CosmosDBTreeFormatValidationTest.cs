using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Stardust.Paradox.Data.InMemory;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class CosmosDBTreeFormatValidationTest
    {
        [Fact]
        public async Task ValidateTreeFormat_MatchesCosmosDBResponse()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Create test data matching your scenario
            var rootId = "550e8324-e29b-41d4-a716-446655440324";
            var tenantId = "tenant123";
            
            await connector.ExecuteAsync($"g.addV('tenantEntity').property('id', '{rootId}').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync($"g.addV('tenantEntity').property('id', 'child1').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync($"g.addV('tenantEntity').property('id', 'child2').property('entityType', 'userGroup')", new Dictionary<string, object>());
            
            // Create member relationships
            await connector.ExecuteAsync($"g.V('{rootId}').addE('members').to(g.V('child1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync($"g.V('{rootId}').addE('members').to(g.V('child2'))", new Dictionary<string, object>());
            
            // Act - Execute query similar to yours (simplified for testing)
            var result = await connector.ExecuteAsync($"g.V('{rootId}').has('entityType', 'userGroup').tree()", new Dictionary<string, object>());
            
            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            
            var treeResult = result.First();
            Assert.IsType<JObject>(treeResult);
            
            var treeJObject = (JObject)treeResult;
            
            // Validate the structure matches CosmosDB format
            Assert.True(treeJObject.ContainsKey(rootId));
            
            var rootNode = treeJObject[rootId] as JArray;
            Assert.NotNull(rootNode);
            Assert.Equal(2, rootNode.Count); // [vertex_data, children_object]
            
            // Validate vertex data structure
            var vertexData = rootNode[0] as JObject;
            Assert.NotNull(vertexData);
            Assert.Equal(rootId, vertexData["id"]?.ToString());
            Assert.Equal("tenantEntity", vertexData["label"]?.ToString());
            Assert.Equal("vertex", vertexData["type"]?.ToString());
            Assert.Equal("userGroup", vertexData["entityType"]?.ToString());
            
            // Validate children object (should be empty for this query)
            var children = rootNode[1] as JObject;
            Assert.NotNull(children);
            Assert.Empty(children); // No children for this specific query
            
            // Output for manual verification
            var jsonOutput = JsonConvert.SerializeObject(treeResult, Formatting.Indented);
            Console.WriteLine("Tree result JSON:");
            Console.WriteLine(jsonOutput);
            
            // Verify it matches the expected CosmosDB format
            var expectedPattern = new JObject
            {
                [rootId] = new JArray(
                    new JObject
                    {
                        ["id"] = rootId,
                        ["label"] = "tenantEntity", 
                        ["type"] = "vertex",
                        ["entityType"] = "userGroup"
                    },
                    new JObject()
                )
            };
            
            // Compare the essential structure (ignoring property order)
            Assert.Equal(expectedPattern.ToString(Formatting.None), treeJObject.ToString(Formatting.None));
        }
    }
}