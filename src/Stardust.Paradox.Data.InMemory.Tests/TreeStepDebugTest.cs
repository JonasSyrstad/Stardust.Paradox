using Microsoft.VisualStudio.TestPlatform.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class TreeStepDebugTest
    {
        private readonly ITestOutputHelper _output;

        public TreeStepDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public async Task DebugTreeStep_CosmosDBFormat()
        {
            Console.WriteLine("=== Tree Step CosmosDB Format Debug ===");
            
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Create test data similar to the CosmosDB scenario
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', '550e8324-e29b-41d4-a716-446655440324').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', 'child1').property('entityType', 'userGroup')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('tenantEntity').property('id', 'child2').property('entityType', 'userGroup')", new Dictionary<string, object>());
            
            // Create member relationships
            await connector.ExecuteAsync("g.V('550e8324-e29b-41d4-a716-446655440324').addE('members').to(g.V('child1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('550e8324-e29b-41d4-a716-446655440324').addE('members').to(g.V('child2'))", new Dictionary<string, object>());
            
            Console.WriteLine("\n1. Testing simple tree: g.V('550e8324-e29b-41d4-a716-446655440324').tree()");
            var result1 = await connector.ExecuteAsync("g.V('550e8324-e29b-41d4-a716-446655440324').tree()", new Dictionary<string, object>());
            Console.WriteLine($"Result count: {result1?.Count() ?? 0}");
            foreach (var item in result1 ?? new List<dynamic>())
            {
                Console.WriteLine($"Result type: {item?.GetType().Name}");
                Console.WriteLine($"JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }

            _output.WriteLine("\n2. Testing tree with emit/repeat query similar to user's example:");
            _output.WriteLine("g.V('550e8324-e29b-41d4-a716-446655440324').emit().repeat(__.out('members')).until(__.loops().is(3)).has('entityType', 'userGroup').tree()");
            
            try
            {
                var result2 = await connector.ExecuteAsync("g.V('550e8324-e29b-41d4-a716-446655440324').emit().repeat(__.out('members')).until(__.loops().is(3)).has('entityType', 'userGroup').tree()", new Dictionary<string, object>());
                _output.WriteLine(JsonConvert.SerializeObject(result2));
                Console.WriteLine($"Result count: {result2?.Count() ?? 0}");
                foreach (var item in result2 ?? new List<dynamic>())
                {
                    Console.WriteLine($"Result type: {item?.GetType().Name}");
                    Console.WriteLine($"JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Complex query failed: {ex.Message}");
                // Fall back to simpler version
                var result2b = await connector.ExecuteAsync("g.V('550e8324-e29b-41d4-a716-446655440324').out('members').tree()", new Dictionary<string, object>());
                Console.WriteLine($"Fallback result count: {result2b?.Count() ?? 0}");
                foreach (var item in result2b ?? new List<dynamic>())
                {
                    Console.WriteLine($"Fallback result type: {item?.GetType().Name}");
                    Console.WriteLine($"Fallback JSON: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
                }
            }
        }
    }
}