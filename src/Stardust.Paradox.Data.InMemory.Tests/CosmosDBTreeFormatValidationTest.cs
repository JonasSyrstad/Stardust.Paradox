using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Stardust.Paradox.Data.InMemory;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;

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
            
            // Validate the structure matches our CosmosDB-compatible format
            Assert.True(treeJObject.ContainsKey(rootId));
            
            var rootNode = treeJObject[rootId] as JObject;
            Assert.NotNull(rootNode);
            
            // Validate the structure has "key" and "value" properties (our format)
            Assert.True(rootNode.ContainsKey("key"));
            Assert.True(rootNode.ContainsKey("value"));
            
            // Validate vertex key data structure
            var keyData = rootNode["key"] as JObject;
            Assert.NotNull(keyData);
            Assert.Equal(rootId, keyData["id"]?.ToString());
            Assert.Equal("tenantEntity", keyData["label"]?.ToString());
            Assert.Equal("vertex", keyData["type"]?.ToString());
            
            // The properties should be in CosmosDB format
            var properties = keyData["properties"] as JObject;
            Assert.NotNull(properties);
            
            // Validate children object (should be empty for this query since we're not navigating children)
            var valueData = rootNode["value"] as JObject;
            Assert.NotNull(valueData);
            Assert.Empty(valueData); // No children for this specific query
            
            // Output for manual verification
            var jsonOutput = JsonConvert.SerializeObject(treeResult, Formatting.Indented);
            Console.WriteLine("Tree result JSON:");
            Console.WriteLine(jsonOutput);
            
            // Verify it can be deserialized into the format expected by the other tests
            // This ensures compatibility with VertexTreeRoot
            var serializedResult = JsonConvert.SerializeObject(new[] { treeResult });
            var canDeserialize = true;
            try
            {
                var testDeserialization = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(serializedResult);
                Assert.NotNull(testDeserialization);
            }
            catch
            {
                canDeserialize = false;
            }
            
            Assert.True(canDeserialize, "Tree result should be deserializable in the expected format");
        }
    }
}