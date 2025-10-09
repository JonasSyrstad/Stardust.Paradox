using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FluentAssertions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Debug test to understand why repeat().until().tree() returns empty
    /// </summary>
    public class TreeRepeatDebugTest
    {
        private readonly ITestOutputHelper _output;

        public TreeRepeatDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Debug_RepeatUntilTree_PathTracking()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create simple hierarchy: root -> child1 -> grandchild1
            await connector.ExecuteAsync("g.addV('person').property('id', 'root').property('name', 'Root')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'child1').property('name', 'Child1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'grandchild1').property('name', 'Grandchild1')", new Dictionary<string, object>());

            // Create edges
            await connector.ExecuteAsync("g.V('root').addE('parent').to(g.V('child1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('child1').addE('parent').to(g.V('grandchild1'))", new Dictionary<string, object>());

            _output.WriteLine("=== Test 1: Simple out() with tree() ===");
            var simpleResult = await connector.ExecuteAsync("g.V('root').out('parent').tree()", new Dictionary<string, object>());
            _output.WriteLine($"Result count: {simpleResult.Count()}");
            foreach (var item in simpleResult)
            {
                _output.WriteLine($"Result: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }

            _output.WriteLine("\n=== Test 2: Two out() steps with tree() ===");
            var twoOutResult = await connector.ExecuteAsync("g.V('root').out('parent').out('parent').tree()", new Dictionary<string, object>());
            _output.WriteLine($"Result count: {twoOutResult.Count()}");
            foreach (var item in twoOutResult)
            {
                _output.WriteLine($"Result: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }

            _output.WriteLine("\n=== Test 3: repeat().times() with tree() ===");
            var repeatTimesResult = await connector.ExecuteAsync("g.V('root').repeat(out('parent')).times(2).tree()", new Dictionary<string, object>());
            _output.WriteLine($"Result count: {repeatTimesResult.Count()}");
            foreach (var item in repeatTimesResult)
            {
                _output.WriteLine($"Result: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }

            _output.WriteLine("\n=== Test 4: repeat().until() with tree() ===");
            var repeatUntilResult = await connector.ExecuteAsync("g.V('root').repeat(__.out('parent')).until(__.outE('parent').count().is(0)).tree()", new Dictionary<string, object>());
            _output.WriteLine($"Result count: {repeatUntilResult.Count()}");
            foreach (var item in repeatUntilResult)
            {
                _output.WriteLine($"Result: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
            }

            // This test should help us understand where path tracking breaks
            repeatUntilResult.Should().NotBeNull();
            repeatUntilResult.Should().HaveCount(1);
        }

        [Fact]
        public async Task Debug_RepeatUntil_Without_Tree()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create simple hierarchy
            await connector.ExecuteAsync("g.addV('person').property('id', 'root').property('name', 'Root')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'child1').property('name', 'Child1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'grandchild1').property('name', 'Grandchild1')", new Dictionary<string, object>());

            await connector.ExecuteAsync("g.V('root').addE('parent').to(g.V('child1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('child1').addE('parent').to(g.V('grandchild1'))", new Dictionary<string, object>());

            _output.WriteLine("=== Testing repeat().until() WITHOUT tree() ===");
            var result = await connector.ExecuteAsync("g.V('root').repeat(__.out('parent')).until(__.outE('parent').count().is(0))", new Dictionary<string, object>());
            
            _output.WriteLine($"Result count: {result.Count()}");
            foreach (var item in result)
            {
                _output.WriteLine($"Item type: {item?.GetType().Name}");
                _output.WriteLine($"Item: {JsonConvert.SerializeObject(item, Formatting.Indented)}");
                
                // Try to access id
                try
                {
                    string id = item.id;
                    _output.WriteLine($"  ID: {id}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Could not get ID: {ex.Message}");
                }
            }

            // This should return the leaf vertex (grandchild1)
            result.Should().HaveCount(1);
            var vertex = result.First();
            string vertexId = vertex.id;
            vertexId.Should().Be("grandchild1");
        }
    }
}
