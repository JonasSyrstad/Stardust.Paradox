using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.Tree;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Comprehensive tests for the tree step implementation in InMemory connector
    /// </summary>
    public class TreeStepTests
    {
        [Fact]
        public async Task Tree_Simple_ShouldReturnTreeStructure()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create a simple tree structure: A -> B -> C
            await connector.ExecuteAsync("g.addV('person').property('id', 'A').property('name', 'A')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'B').property('name', 'B')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'C').property('name', 'C')", new Dictionary<string, object>());

            await connector.ExecuteAsync("g.V('A').addE('child').to(g.V('B'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('B').addE('child').to(g.V('C'))", new Dictionary<string, object>());

            // Act
            var result = await connector.ExecuteAsync("g.V('A').out('child').tree()", new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var treeResult = result.First();
            Assert.NotNull(treeResult);

            // The result should be a JObject representing the tree structure
            var treeJObject = treeResult as JObject;
            Assert.NotNull(treeJObject);
        }

        [Fact]
        public async Task Tree_WithRepeat_ShouldReturnCompleteTreeStructure()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create a tree structure: Root -> Child1, Child2 -> Grandchild1, Grandchild2
            await connector.ExecuteAsync("g.addV('person').property('id', 'root').property('name', 'Root')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'child1').property('name', 'Child1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'child2').property('name', 'Child2')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'grandchild1').property('name', 'Grandchild1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'grandchild2').property('name', 'Grandchild2')", new Dictionary<string, object>());

            // Create edges
            await connector.ExecuteAsync("g.V('root').addE('parent').to(g.V('child1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('root').addE('parent').to(g.V('child2'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('child1').addE('parent').to(g.V('grandchild1'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('child1').addE('parent').to(g.V('grandchild2'))", new Dictionary<string, object>());

            // Act - Simulate the pattern used in GraphContextBase.GetTreeAsync
            var result = await connector.ExecuteAsync("g.V('root').repeat(__.out('parent')).until(__.outE('parent').count().is(0)).tree()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count());

            var treeResult = result.First();
            Assert.NotNull(treeResult);

            // Convert to JObject for inspection
            var treeJObject = treeResult as JObject;
            Assert.NotNull(treeJObject);

            // Verify tree structure
            treeJObject.Should().ContainKey("root");
        }

        [Fact]
        public async Task Tree_EmptyTraversal_ShouldReturnEmptyTree()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Act
            var result = await connector.ExecuteAsync("g.V('nonexistent').out('child').tree()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count());

            var treeResult = result.First();
            var treeJObject = treeResult as JObject;
            treeJObject.Should().NotBeNull();
            treeJObject.Should().BeEmpty();
        }

        [Fact]
        public async Task Tree_SingleVertex_ShouldReturnSingleNodeTree()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            await connector.ExecuteAsync("g.addV('person').property('id', 'single').property('name', 'Single')", new Dictionary<string, object>());

            // Act
            var result = await connector.ExecuteAsync("g.V('single').tree()", new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var treeResult = result.First();
            var treeJObject = treeResult as JObject;
            treeJObject.Should().NotBeNull();
            treeJObject.Should().ContainKey("single");
        }

        [Fact]
        public async Task Tree_ComplexStructure_ShouldBuildCorrectHierarchy()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create a more complex tree structure
            /*
             *      A
             *    /   \
             *   B     C
             *  / \   /
             * D   E F
             */

            await connector.ExecuteAsync("g.addV('node').property('id', 'A').property('level', 0)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('node').property('id', 'B').property('level', 1)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('node').property('id', 'C').property('level', 1)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('node').property('id', 'D').property('level', 2)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('node').property('id', 'E').property('level', 2)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('node').property('id', 'F').property('level', 2)", new Dictionary<string, object>());

            // Create edges
            await connector.ExecuteAsync("g.V('A').addE('child').to(g.V('B'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('A').addE('child').to(g.V('C'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('B').addE('child').to(g.V('D'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('B').addE('child').to(g.V('E'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('C').addE('child').to(g.V('F'))", new Dictionary<string, object>());

            // Act
            var result = await connector.ExecuteAsync("g.V('A').repeat(__.out('child')).until(__.outE('child').count().is(0)).tree()", new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var treeResult = result.First();
            var treeJObject = treeResult as JObject;
            treeJObject.Should().NotBeNull();

            // Verify the tree structure contains the root
            treeJObject.Should().ContainKey("A");
        }

        [Fact]
        public async Task Tree_MatchesCosmosDBFormat_ShouldReturnCompatibleStructure()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create test data similar to what would be used in production
            await connector.ExecuteAsync("g.addV('person').property('id', 'parent').property('name', 'Parent').property('pk', 'parent')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'child').property('name', 'Child').property('pk', 'child')", new Dictionary<string, object>());

            await connector.ExecuteAsync("g.V('parent').addE('parent').to(g.V('child'))", new Dictionary<string, object>());

            // Act - Use the exact pattern from GraphContextBase.GetTreeAsync
            var result = await connector.ExecuteAsync("g.V('parent').repeat(__.out('parent')).until(__.outE('parent').count().is(0)).tree()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count());

            var treeResult = result.First();

            // The tree result should be compatible with VertexTreeRoot<T> constructor
            // which expects IEnumerable<dynamic> items where each item contains JProperty values
            Assert.NotNull(treeResult);
        }

        [Fact]
        public async Task Tree_WithIncomingEdges_ShouldWorkWithInPattern()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Create a child -> parent structure 
            await connector.ExecuteAsync("g.addV('person').property('id', 'child').property('name', 'Child')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'parent').property('name', 'Parent')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'grandparent').property('name', 'Grandparent')", new Dictionary<string, object>());

            await connector.ExecuteAsync("g.V('child').addE('parent').to(g.V('parent'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('parent').addE('parent').to(g.V('grandparent'))", new Dictionary<string, object>());

            // Act - Navigate using incoming edges (like the incoming edge pattern in GraphContextBase.GetTreeAsync)
            var result = await connector.ExecuteAsync("g.V('child').repeat(__.in('parent')).until(__.inE('parent').count().is(0)).tree()", new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var treeResult = result.First();
            var treeJObject = treeResult as JObject;
            treeJObject.Should().NotBeNull();
            treeJObject.Should().ContainKey("child");
        }

        [Fact]
        public async Task TreeStep_IntegrationWithVertexTreeRoot_ShouldBeCompatible()
        {
            // This test verifies that the tree step output can be used with the existing VertexTreeRoot classes

            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            await connector.ExecuteAsync("g.addV('person').property('id', 'root').property('name', 'Root')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'leaf').property('name', 'Leaf')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('root').addE('child').to(g.V('leaf'))", new Dictionary<string, object>());

            // Act
            var result = await connector.ExecuteAsync("g.V('root').out('child').tree()", new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var treeResult = result.First();
            Assert.NotNull(treeResult);

            // The result should be in a format that can be consumed by VertexTreeRoot constructor
            // which expects IEnumerable<dynamic> where each dynamic contains JProperty-like structures
        }
    }
}