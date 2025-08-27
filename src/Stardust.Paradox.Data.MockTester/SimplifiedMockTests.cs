using Stardust.Paradox.Data.Mocker;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Stardust.Paradox.Data.MockTester
{
    public class SimplifiedMockTests
    {
        [Fact]
        public async Task Should_Work_WithZeroConfiguration()
        {
            // Arrange - One line setup!
            var connector = MockGremlinConnectorFactory.CreateForTesting();

            // Act & Assert - Should work immediately
            
            // Create a vertex
            var createResult = await connector.ExecuteAsync("g.addV('person').property('name', 'John')", 
                new Dictionary<string, object>());
            Assert.Single(createResult);
            Assert.Equal("person", createResult.First().label);

            // Query vertices - should find the created vertex
            var queryResult = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
            Assert.Single(queryResult);
            Assert.Equal("person", queryResult.First().label);
        }

        [Fact]
        public async Task Should_Work_WithQuickPopulate()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();

            // Quick populate test data
            connector.QuickPopulate(
                vertices: new VertexDefinition[] 
                { 
                    new VertexDefinition("user1", "person", 
                        new KeyValuePair<string, object>("name", "John"),
                        new KeyValuePair<string, object>("age", 30)), 
                    new VertexDefinition("user2", "person",
                        new KeyValuePair<string, object>("name", "Jane"),
                        new KeyValuePair<string, object>("age", 25)) 
                },
                edges: new EdgeDefinition[] 
                { 
                    new EdgeDefinition("e1", "knows", "user1", "user2") 
                }
            );

            // Act
            var users = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
            var edges = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());

            // Assert
            Assert.Equal(2, users.Count());
            Assert.Single(edges);
        }

        [Fact]
        public async Task Should_Work_WithSocialNetworkTemplate()
        {
            // Arrange - Use pre-built template
            var connector = MockGremlinConnectorFactory.CreateSocialNetworkScenario();

            // Act - Template should have pre-populated data
            var users = await connector.ExecuteAsync("g.V().hasLabel('user')", new Dictionary<string, object>());
            var posts = await connector.ExecuteAsync("g.V().hasLabel('post')", new Dictionary<string, object>());
            var friendships = await connector.ExecuteAsync("g.E().hasLabel('friends')", new Dictionary<string, object>());

            // Assert - Data should be available immediately
            Assert.True(users.Count() >= 3, $"Expected at least 3 users, got {users.Count()}");
            Assert.True(posts.Count() >= 1, $"Expected at least 1 post, got {posts.Count()}");
            Assert.True(friendships.Count() >= 1, $"Expected at least 1 friendship, got {friendships.Count()}");
        }

        [Fact]
        public async Task Should_Work_WithAutoConfiguration()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create();
            
            // Auto-configure for specific labels
            connector.AutoConfigureFor("person", "company");

            // Act - Should work with auto-configured patterns
            var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());

            // Assert
            Assert.Single(result);
            Assert.Equal("person", result.First().label);
        }

        [Fact]
        public void Should_Create_RandomVertices()
        {
            // Act
            var vertices = MockExtensions.CreateRandomVertices("person", count: 3, "name", "email", "age");

            // Assert
            Assert.Equal(3, vertices.Count());
            foreach (var vertex in vertices)
            {
                Assert.Equal("person", vertex.label);
                Assert.NotNull(vertex.properties);
                var props = (Dictionary<string, object>)vertex.properties;
                Assert.True(props.ContainsKey("name"));
                Assert.True(props.ContainsKey("email"));
                Assert.True(props.ContainsKey("age"));
            }
        }

        [Fact]
        public void Should_Create_VertexWithProperties()
        {
            // Act
            var properties = new[]
            {
                new KeyValuePair<string, object>("name", "John"),
                new KeyValuePair<string, object>("age", 30),
                new KeyValuePair<string, object>("email", "john@example.com")
            };
            var vertex = MockExtensions.CreateVertexResponse("user123", "person", properties.ToDictionary(p => p.Key, p => p.Value));

            // Assert
            Assert.Equal("user123", vertex.id);
            Assert.Equal("person", vertex.label);
            
            var props = (Dictionary<string, object>)vertex.properties;
            Assert.Equal("John", props["name"]);
            Assert.Equal(30, props["age"]);
            Assert.Equal("john@example.com", props["email"]);
        }

        [Fact]
        public async Task Should_Work_WithOrganizationTemplate()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateOrganizationScenario();

            // Act
            var employees = await connector.ExecuteAsync("g.V().hasLabel('employee')", new Dictionary<string, object>());
            var departments = await connector.ExecuteAsync("g.V().hasLabel('department')", new Dictionary<string, object>());

            // Assert
            Assert.True(employees.Count() >= 2);
            Assert.True(departments.Count() >= 2);
        }

        [Fact]
        public async Task Should_Work_WithECommerceTemplate()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateECommerceScenario();

            // Act
            var customers = await connector.ExecuteAsync("g.V().hasLabel('customer')", new Dictionary<string, object>());
            var products = await connector.ExecuteAsync("g.V().hasLabel('product')", new Dictionary<string, object>());

            // Assert
            Assert.True(customers.Count() >= 2);
            Assert.True(products.Count() >= 2);
        }

        [Fact]
        public async Task Should_Work_WithUserManagementTemplate()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateUserManagementScenario();

            // Act
            var users = await connector.ExecuteAsync("g.V().hasLabel('user')", new Dictionary<string, object>());
            var roles = await connector.ExecuteAsync("g.V().hasLabel('role')", new Dictionary<string, object>());
            var permissions = await connector.ExecuteAsync("g.V().hasLabel('permission')", new Dictionary<string, object>());

            // Assert
            Assert.True(users.Count() >= 3);
            Assert.True(roles.Count() >= 3);
            Assert.True(permissions.Count() >= 3);
        }
    }
}