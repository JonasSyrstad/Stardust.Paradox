using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Stardust.Paradox.Data.InMemory;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class TreeStepComplexFormatTest
    {
        [Fact]
        public async Task TestComplexTreeWithChildren()
        {
            Console.WriteLine("=== Complex Tree Format Test ===");
            
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Create a tree structure: root -> child1, child2 -> grandchild1, grandchild2
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', 'root').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', 'child1').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', 'child2').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', 'grandchild1').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', 'grandchild2').property('entityType', 'userGroup')", new Dictionary<string, object>());
            
            // Create member relationships
            await connector.ExecuteAsync("g.V('root').addE('members').to(g.V('child1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('root').addE('members').to(g.V('child2'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('child1').addE('members').to(g.V('grandchild1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('child1').addE('members').to(g.V('grandchild2'))", new Dictionary<string, object>());
            
            Console.WriteLine("\n1. Testing simple navigation tree: g.V('root').out('members').tree()");
            var result1 = await connector.ExecuteAsync("g.V('root').out('members').tree()", new Dictionary<string, object>());
            Console.WriteLine($"Result count: {result1?.Count() ?? 0}");
            foreach (var item in result1 ?? new List<dynamic>())
            {
                Console.WriteLine($"JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }
            
            Console.WriteLine("\n2. Testing full tree traversal: g.V('root').repeat(__.out('members')).until(__.outE('members').count().is(0)).tree()");
            var result2 = await connector.ExecuteAsync("g.V('root').repeat(__.out('members')).until(__.outE('members').count().is(0)).tree()", new Dictionary<string, object>());
            Console.WriteLine($"Result count: {result2?.Count() ?? 0}");
            foreach (var item in result2 ?? new List<dynamic>())
            {
                Console.WriteLine($"JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }
            
            // If the repeat/until isn't working, let's try a simpler emit approach
            Console.WriteLine("\n3. Testing emit tree: g.V('root').emit().repeat(__.out('members')).times(3).tree()");
            try
            {
                var result3 = await connector.ExecuteAsync("g.V('root').emit().repeat(__.out('members')).times(3).tree()", new Dictionary<string, object>());
                Console.WriteLine($"Result count: {result3?.Count() ?? 0}");
                foreach (var item in result3 ?? new List<dynamic>())
                {
                    Console.WriteLine($"JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Emit approach failed: {ex.Message}");
            }
        }
    }
}