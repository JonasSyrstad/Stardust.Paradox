using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class EdgeCreationDebugTest
    {
        private readonly ITestOutputHelper _output;

        public EdgeCreationDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestSimpleEdgeCreation()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Create vertices
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2')", new Dictionary<string, object>());
            
            // Try simple edge creation that should work
            _output.WriteLine("=== Simple edge creation ===");
            var result = await connector.ExecuteAsync("g.V('v1').addE('knows').to(g.V('v2'))", new Dictionary<string, object>());
            _output.WriteLine($"Result: {result.FirstOrDefault()}");
            
            var edges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            _output.WriteLine($"Edges count: {edges.Count()}");
            foreach (var edge in edges)
            {
                _output.WriteLine($"Edge: {edge}");
            }
            
            Assert.True(edges.Any());
        }

        [Fact]
        public async Task TestComplexEdgeSteps()
        {
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Create vertices
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2')", new Dictionary<string, object>());
            
            // Test individual complex steps
            _output.WriteLine("=== Testing steps individually ===");
            
            _output.WriteLine("\n1. V('v1').as('a')");
            var step1 = await connector.ExecuteAsync("g.V('v1').as('a')", new Dictionary<string, object>());
            _output.WriteLine($"Result: {step1.FirstOrDefault()}");
            
            _output.WriteLine("\n2. V('v1').as('a').V('v2').as('b')");
            var step2 = await connector.ExecuteAsync("g.V('v1').as('a').V('v2').as('b')", new Dictionary<string, object>());
            _output.WriteLine($"Result: {step2.FirstOrDefault()}");
            
            _output.WriteLine("\n3. V('v1').as('a').V('v2').as('b').addE('rel')");
            var step3 = await connector.ExecuteAsync("g.V('v1').as('a').V('v2').as('b').addE('rel')", new Dictionary<string, object>());
            _output.WriteLine($"Result: {step3.FirstOrDefault()}");
            
            _output.WriteLine("\n4. V('v1').as('a').V('v2').as('b').addE('rel').from('a')");
            var step4 = await connector.ExecuteAsync("g.V('v1').as('a').V('v2').as('b').addE('rel').from('a')", new Dictionary<string, object>());
            _output.WriteLine($"Result: {step4.FirstOrDefault()}");
            
            _output.WriteLine("\n5. V('v1').as('a').V('v2').as('b').addE('rel').from('a').to('b')");
            var step5 = await connector.ExecuteAsync("g.V('v1').as('a').V('v2').as('b').addE('rel').from('a').to('b')", new Dictionary<string, object>());
            _output.WriteLine($"Result: {step5.FirstOrDefault()}");
            
            _output.WriteLine("\n=== Checking database ===");
            var edges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            _output.WriteLine($"Edges count: {edges.Count()}");
            foreach (var edge in edges)
            {
                _output.WriteLine($"Edge: {edge}");
            }
        }
    }
}