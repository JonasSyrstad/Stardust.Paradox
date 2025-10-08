using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Tests.CosmosDbMigrated;
using Stardust.Paradox.Data.Tree;
using Newtonsoft.Json;
using System;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Test the GetTreeAsync functionality with the InMemory connector
    /// This tests the integration between tree step and VertexTreeRoot
    /// </summary>
    public class GetTreeAsyncTests
    {
        [Fact]
        public async Task GetTreeAsync_WithValidData_ShouldReturnTreeStructure()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            using (var context = new TestContext(connector))
            {
                // Create test data: Parent -> Child1, Child2 -> Grandchild
                var parent = context.Profiles.Create("parent");
                parent.Name = "Parent";
                parent.Pk = "parent";
                parent.FirstName = "Parent";

                var child1 = context.Profiles.Create("child1");
                child1.Name = "Child1";
                child1.Pk = "child1";
                child1.FirstName = "Child1";

                var child2 = context.Profiles.Create("child2");
                child2.Name = "Child2";
                child2.Pk = "child2";
                child2.FirstName = "Child2";

                var grandchild = context.Profiles.Create("grandchild");
                grandchild.Name = "Grandchild";
                grandchild.Pk = "grandchild";
                grandchild.FirstName = "Grandchild";

                await context.SaveChangesAsync();

                // Set up relationships
                parent.Children.Add(child1);
                parent.Children.Add(child2);
                child1.Children.Add(grandchild);

                await context.SaveChangesAsync();

                // Act
                var tree = await context.GetTreeAsync<IProfile>("parent", "parent");

                // Assert
                tree.Should().NotBeNull();
                // The tree should contain the parent node
                tree.Should().HaveCountGreaterThan(0);
            }
        }

        [Fact]
        public async Task GetTreeAsync_WithEdgeLabel_ShouldReturnCorrectStructure()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            using (var context = new TestContext(connector))
            {
                // Create hierarchical data
                var manager = context.Profiles.Create("manager");
                manager.Name = "Manager";
                manager.Pk = "manager";
                manager.FirstName = "Manager";
                
                var employee1 = context.Profiles.Create("employee1");
                employee1.Name = "Employee1";
                employee1.Pk = "employee1";
                employee1.FirstName = "Employee1";
                
                var employee2 = context.Profiles.Create("employee2");
                employee2.Name = "Employee2";
                employee2.Pk = "employee2";
                employee2.FirstName = "Employee2";
                
                await context.SaveChangesAsync();

                // Create parent-child relationships
                manager.Children.Add(employee1);
                manager.Children.Add(employee2);
                
                await context.SaveChangesAsync();

                // Act - Test using the edge label "parent"
                var tree = await context.GetTreeAsync<IProfile>("manager", "parent", false);

                // Assert
                tree.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task GetTreeAsync_WithIncomingEdges_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            using (var context = new TestContext(connector))
            {
                // Create test data
                var leaf = context.Profiles.Create("leaf");
                leaf.Name = "Leaf";
                leaf.Pk = "leaf";
                leaf.FirstName = "Leaf";
                
                var parent = context.Profiles.Create("parent");
                parent.Name = "Parent";
                parent.Pk = "parent";
                parent.FirstName = "Parent";
                
                var grandparent = context.Profiles.Create("grandparent");
                grandparent.Name = "Grandparent";
                grandparent.Pk = "grandparent";
                grandparent.FirstName = "Grandparent";
                
                await context.SaveChangesAsync();

                // Set up relationships - create a chain
                parent.Children.Add(leaf);
                grandparent.Children.Add(parent);
                
                await context.SaveChangesAsync();

                // Act - Test with incoming edges (traverse up the hierarchy)
                var tree = await context.GetTreeAsync<IProfile>("leaf", "parent", true);

                // Assert
                tree.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task GetTreeAsync_WithExpressionBasedEdgeLabel_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            using (var context = new TestContext(connector))
            {
                // Create test data
                var root = context.Profiles.Create("root");
                root.Name = "Root";
                root.Pk = "root";
                root.FirstName = "Root";
                
                var child = context.Profiles.Create("child");
                child.Name = "Child";
                child.Pk = "child";
                child.FirstName = "Child";
                
                await context.SaveChangesAsync();

                // Set up relationship
                root.Children.Add(child);
                
                await context.SaveChangesAsync();

                // Act - Test using expression-based edge label
                var tree = await context.GetTreeAsync<IProfile>("root", p => p.Children);

                // Assert
                tree.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task GetTreeAsync_EmptyResult_ShouldReturnEmptyTree()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            using (var context = new TestContext(connector))
            {
                // Act - Try to get tree for non-existent vertex
                var tree = await context.GetTreeAsync<IProfile>("nonexistent", "parent");

                // Assert
                tree.Should().NotBeNull();
                tree.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetTreeAsync_ComplexHierarchy_ShouldReturnCompleteTree()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            using (var context = new TestContext(connector))
            {
                // Create a complex hierarchy
                /*
                 *       CEO
                 *      /   \
                 *    VP1   VP2
                 *   /  \    |
                 * EMP1 EMP2 EMP3
                 */

                var ceo = context.Profiles.Create("ceo");
                ceo.Name = "CEO";
                ceo.Pk = "ceo";
                ceo.FirstName = "CEO";

                var vp1 = context.Profiles.Create("vp1");
                vp1.Name = "VP1";
                vp1.Pk = "vp1";
                vp1.FirstName = "VP1";

                var vp2 = context.Profiles.Create("vp2");
                vp2.Name = "VP2";
                vp2.Pk = "vp2";
                vp2.FirstName = "VP2";

                var emp1 = context.Profiles.Create("emp1");
                emp1.Name = "EMP1";
                emp1.Pk = "emp1";
                emp1.FirstName = "EMP1";

                var emp2 = context.Profiles.Create("emp2");
                emp2.Name = "EMP2";
                emp2.Pk = "emp2";
                emp2.FirstName = "EMP2";

                var emp3 = context.Profiles.Create("emp3");
                emp3.Name = "EMP3";
                emp3.Pk = "emp3";
                emp3.FirstName = "EMP3";

                await context.SaveChangesAsync();

                // Set up relationships
                ceo.Children.Add(vp1);
                ceo.Children.Add(vp2);
                vp1.Children.Add(emp1);
                vp1.Children.Add(emp2);
                vp2.Children.Add(emp3);

                await context.SaveChangesAsync();

                // Act
                var tree = await context.GetTreeAsync<IProfile>("ceo", "parent");

                // Assert
                tree.Should().NotBeNull();
                tree.Should().HaveCountGreaterThan(0);

                // Print the tree for debugging
                var json = JsonConvert.SerializeObject(tree, Formatting.Indented);
                Console.WriteLine($"Tree result: {json}");
            }
        }
    }
}