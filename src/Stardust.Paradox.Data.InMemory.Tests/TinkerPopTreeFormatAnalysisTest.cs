using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Stardust.Paradox.Data.InMemory;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class TinkerPopTreeFormatAnalysisTest
    {
        [Fact]
        public async Task AnalyzeTinkerPopTreeFormat()
        {
            Console.WriteLine("=== TinkerPop Tree Format Analysis ===");
            
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Test Case 1: Simple single vertex tree (your example)
            var rootId = "550e8324-e29b-41d4-a716-446655440324";
            await connector.ExecuteAsync($"g.addV('tenantEntity').property('id', '{rootId}').property('entityType', 'userGroup')", new Dictionary<string, object>());
            
            var result1 = await connector.ExecuteAsync($"g.V('{rootId}').tree()", new Dictionary<string, object>());
            Console.WriteLine("=== Single Vertex Tree ===");
            Console.WriteLine($"Result count: {result1?.Count() ?? 0}");
            foreach (var item in result1 ?? new List<dynamic>())
            {
                Console.WriteLine($"Raw JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }
            
            // Test Case 2: Tree with children (matching your actual use case)
            await connector.ExecuteAsync($"g.addV('tenantEntity').property('id', 'child1').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync($"g.addV('tenantEntity').property('id', 'child2').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync($"g.V('{rootId}').addE('members').to(g.V('child1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync($"g.V('{rootId}').addE('members').to(g.V('child2'))", new Dictionary<string, object>());
            
            var result2 = await connector.ExecuteAsync($"g.V('{rootId}').repeat(__.out('members')).until(__.outE('members').count().is(0)).tree()", new Dictionary<string, object>());
            Console.WriteLine("\n=== Tree with Children ===");
            Console.WriteLine($"Result count: {result2?.Count() ?? 0}");
            foreach (var item in result2 ?? new List<dynamic>())
            {
                Console.WriteLine($"Raw JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }
            
            // Test Case 3: Expected CosmosDB format structure analysis
            Console.WriteLine("\n=== Expected CosmosDB Format Analysis ===");
            Console.WriteLine("Based on your example: [{'550e8324-e29b-41d4-a716-446655440324':[{'id':'550e8324-e29b-41d4-a716-446655440324','label':'tenantEntity','type':'vertex'},{}]}]");
            
            // This means:
            // - The response is an array with one element
            // - That element is an object with vertex ID as key
            // - The value is an array: [vertex_data, children_map]
            // - vertex_data contains: {id, label, type, ...custom_properties}
            // - children_map contains child nodes in the same format, or {} if no children
        }
    }
}