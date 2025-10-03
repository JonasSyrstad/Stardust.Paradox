using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Stardust.Paradox.Data.InMemory;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class TreeStepDebugTest
    {
        [Fact]
        public async Task DebugTreeStep_BasicCase()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Test 1: Simple vertex + tree
            await connector.ExecuteAsync("g.addV('person').property('id', 'test')", new Dictionary<string, object>());
            
            var result1 = await connector.ExecuteAsync("g.V('test').tree()", new Dictionary<string, object>());
            Console.WriteLine($"V('test').tree() result count: {result1?.Count() ?? 0}");
            foreach (var item in result1 ?? new List<dynamic>())
            {
                Console.WriteLine($"Result type: {item?.GetType().Name}, Value: {item}");
                if (item is JObject jobj)
                {
                    Console.WriteLine($"JObject content: {jobj}");
                }
            }
            
            // Test 2: Empty tree - should always return 1 result (empty tree)
            var result2 = await connector.ExecuteAsync("g.V('nonexistent').tree()", new Dictionary<string, object>());
            Console.WriteLine($"V('nonexistent').tree() result count: {result2?.Count() ?? 0}");
            foreach (var item in result2 ?? new List<dynamic>())
            {
                Console.WriteLine($"Result type: {item?.GetType().Name}, Value: {item}");
            }
            
            // Test 3: Empty tree via limit
            var result3 = await connector.ExecuteAsync("g.V().limit(0).tree()", new Dictionary<string, object>());
            Console.WriteLine($"V().limit(0).tree() result count: {result3?.Count() ?? 0}");
            foreach (var item in result3 ?? new List<dynamic>())
            {
                Console.WriteLine($"Result type: {item?.GetType().Name}, Value: {item}");
            }
            
            // Test 4: Direct call to empty V set should also work
            var result4 = await connector.ExecuteAsync("g.V('nonexistent').out('child').tree()", new Dictionary<string, object>());
            Console.WriteLine($"V('nonexistent').out('child').tree() result count: {result4?.Count() ?? 0}");
            foreach (var item in result4 ?? new List<dynamic>())
            {
                Console.WriteLine($"Result type: {item?.GetType().Name}, Value: {item}");
            }
        }
    }
}