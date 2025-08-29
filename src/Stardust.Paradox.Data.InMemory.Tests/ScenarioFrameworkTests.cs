using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests for the InMemory scenario loader framework
    /// </summary>
    public class ScenarioFrameworkTests
    {
        public ScenarioFrameworkTests()
        {
            // Ensure built-in scenarios are registered for all tests
            InMemoryScenarioRegistry.EnsureBuiltInScenariosRegistered();
        }

        [Fact]
        public void InMemoryScenarioRegistry_ShouldHaveBuiltInScenarios()
        {
            // Arrange & Act
            var scenarios = InMemoryScenarioRegistry.GetScenarioNames().ToArray();

            // Assert
            scenarios.Should().NotBeEmpty();
            scenarios.Should().Contain("BasicSocialNetwork");
            scenarios.Should().Contain("SimpleECommerce");
            scenarios.Should().Contain("OrganizationHierarchy");
            scenarios.Should().Contain("UserRoleManagement");
            scenarios.Should().Contain("GraphTraversalTest");
        }

        [Fact]
        public void InMemoryScenarioRegistry_ShouldRegisterCustomScenarios()
        {
            // Arrange
            var customScenario = new TestCustomScenario();

            // Act
            InMemoryScenarioRegistry.Register(customScenario);

            // Assert
            InMemoryScenarioRegistry.IsRegistered("TestCustom").Should().BeTrue();
            var retrieved = InMemoryScenarioRegistry.GetScenario("TestCustom");
            retrieved.Should().NotBeNull();
            retrieved.Should().Be(customScenario);
        }

        [Fact]
        public void InMemoryConnectorFactory_CreateWithScenario_ShouldLoadScenarioData()
        {
            // Arrange & Act
            var connector = InMemoryConnectorFactory.CreateWithScenario("BasicSocialNetwork");

            // Assert
            var stats = connector.GetStatistics();
            stats.VertexCount.Should().BeGreaterThan(0);
            stats.EdgeCount.Should().BeGreaterThan(0);

            // Check specific vertices exist
            connector.GetVertex("john").Should().NotBeNull();
            connector.GetVertex("jane").Should().NotBeNull();
        }

        [Fact]
        public async Task BasicSocialNetworkScenario_ShouldSupportTraversalQueries()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateSocialNetwork();

            // Act
            var people = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
            var friendsOfJohn = await connector.ExecuteAsync("g.V('john').out('knows')", new Dictionary<string, object>());

            // Assert
            people.Should().NotBeEmpty();
            people.Count().Should().BeGreaterThanOrEqualTo(4); // john, jane, bob, alice

            friendsOfJohn.Should().NotBeEmpty();
            friendsOfJohn.Count().Should().BeGreaterThanOrEqualTo(2); // jane, alice
        }

        [Fact]
        public async Task SimpleECommerceScenario_ShouldSupportECommerceQueries()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateECommerce();

            // Act
            var products = await connector.ExecuteAsync("g.V().hasLabel('product')", new Dictionary<string, object>());
            var customers = await connector.ExecuteAsync("g.V().hasLabel('customer')", new Dictionary<string, object>());

            // Assert
            products.Should().NotBeEmpty();
            customers.Should().NotBeEmpty();

            // Check specific data
            connector.GetVertex("laptop").Should().NotBeNull();
            connector.GetVertex("customer1").Should().NotBeNull();
        }

        [Fact]
        public async Task OrganizationHierarchyScenario_ShouldSupportHierarchyQueries()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateOrganization();

            // Act
            var employees = await connector.ExecuteAsync("g.V().hasLabel('employee')", new Dictionary<string, object>());
            var departments = await connector.ExecuteAsync("g.V().hasLabel('department')", new Dictionary<string, object>());

            // Assert
            employees.Should().NotBeEmpty();
            departments.Should().NotBeEmpty();

            // Check hierarchy exists
            connector.GetVertex("ceo").Should().NotBeNull();
            connector.GetVertex("eng_manager").Should().NotBeNull();
        }

        [Fact]
        public async Task UserRoleManagementScenario_ShouldSupportRoleQueries()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateUserManagement();

            // Act
            var users = await connector.ExecuteAsync("g.V().hasLabel('user')", new Dictionary<string, object>());
            var roles = await connector.ExecuteAsync("g.V().hasLabel('role')", new Dictionary<string, object>());
            var permissions = await connector.ExecuteAsync("g.V().hasLabel('permission')", new Dictionary<string, object>());

            // Assert
            users.Should().NotBeEmpty();
            roles.Should().NotBeEmpty();
            permissions.Should().NotBeEmpty();

            // Check specific entities
            connector.GetVertex("admin").Should().NotBeNull();
            connector.GetVertex("admin_role").Should().NotBeNull();
        }

        [Fact]
        public void CreateWithScenarios_MultipleScenarios_ShouldCombineData()
        {
            // Arrange & Act
            var connector = InMemoryConnectorFactory.CreateWithScenarios(
                new[] { "BasicSocialNetwork", "SimpleECommerce" });

            // Assert
            var stats = connector.GetStatistics();
            stats.VertexCount.Should().BeGreaterThan(6); // Combined data from both scenarios
            stats.EdgeCount.Should().BeGreaterThan(6);

            // Should have data from both scenarios
            connector.GetVertex("john").Should().NotBeNull(); // From social network
            connector.GetVertex("laptop").Should().NotBeNull(); // From e-commerce
        }

        [Fact]
        public void FluentExtensions_WithScenario_ShouldChainCorrectly()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();

            // Act
            connector.WithScenario("BasicSocialNetwork")
                    .WithScenario("SimpleECommerce");

            // Assert
            var stats = connector.GetStatistics();
            stats.VertexCount.Should().BeGreaterThan(6);
            
            connector.GetVertex("john").Should().NotBeNull();
            connector.GetVertex("laptop").Should().NotBeNull();
        }

        [Fact]
        public void ClearScenarios_ShouldRemoveAllData()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateSocialNetwork();
            var initialStats = connector.GetStatistics();
            initialStats.VertexCount.Should().BeGreaterThan(0);

            // Act
            connector.ClearScenarios();

            // Assert
            var clearedStats = connector.GetStatistics();
            clearedStats.VertexCount.Should().Be(0);
            clearedStats.EdgeCount.Should().Be(0);
        }

        [Fact]
        public void ApplyScenario_ToExistingDatabase_ShouldAddData()
        {
            // Arrange
            var database = new InMemoryGraphDatabase();
            database.AddVertex("test", "testVertex");

            // Act
            var applied = InMemoryScenarioRegistry.ApplyScenario(database, "BasicSocialNetwork");

            // Assert
            applied.Should().BeTrue();
            var stats = database.GetStatistics();
            stats.VertexCount.Should().BeGreaterThan(1); // Original + scenario data
        }

        [Fact]
        public void ApplyScenario_NonExistentScenario_ShouldReturnFalse()
        {
            // Arrange
            var database = new InMemoryGraphDatabase();

            // Act
            var applied = InMemoryScenarioRegistry.ApplyScenario(database, "NonExistentScenario");

            // Assert
            applied.Should().BeFalse();
        }

        [Fact]
        public void CreateWithScenario_NonExistentScenario_ShouldThrowException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                InMemoryConnectorFactory.CreateWithScenario("NonExistentScenario"));

            exception.Message.Should().Contain("not found");
            exception.Message.Should().Contain("Available scenarios");
        }

        [Fact]
        public void GetAvailableScenarios_ShouldReturnDescriptions()
        {
            // Act
            var scenarios = InMemoryScenarioExtensions.GetAvailableScenarios();

            // Assert
            scenarios.Should().NotBeEmpty();
            scenarios.Should().ContainKey("BasicSocialNetwork");
            scenarios["BasicSocialNetwork"].Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ListAvailableScenarios_ShouldReturnNames()
        {
            // Act
            var names = InMemoryScenarioExtensions.ListAvailableScenarios();

            // Assert
            names.Should().NotBeEmpty();
            names.Should().Contain("BasicSocialNetwork");
            names.Should().Contain("SimpleECommerce");
        }

        [Fact]
        public async Task GraphTraversalTestScenario_ShouldSupportComplexTraversals()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateWithScenario("GraphTraversalTest");

            // Act
            var allNodes = await connector.ExecuteAsync("g.V().hasLabel('node')", new Dictionary<string, object>());
            var typeANodes = await connector.ExecuteAsync("g.V().hasLabel('typeA')", new Dictionary<string, object>());

            // Assert
            allNodes.Should().NotBeEmpty();
            allNodes.Count().Should().BeGreaterThanOrEqualTo(5);

            typeANodes.Should().NotBeEmpty();
            typeANodes.Count().Should().Be(2); // t1 and t3
        }

        [Fact]
        public void CustomScenario_Registration_ShouldWorkCorrectly()
        {
            // Arrange
            var customScenario = new TestCustomScenario();

            // Act
            InMemoryScenarioRegistry.Register(customScenario);
            var connector = InMemoryConnectorFactory.CreateWithScenario("TestCustom");

            // Assert
            connector.GetVertex("custom1").Should().NotBeNull();
            connector.GetVertex("custom2").Should().NotBeNull();
            
            var stats = connector.GetStatistics();
            stats.VertexCount.Should().Be(2);
            stats.EdgeCount.Should().Be(1);
        }

        [Fact]
        public async Task CustomScenario_CustomResponses_ShouldWork()
        {
            // Arrange
            InMemoryScenarioRegistry.Register(new TestCustomScenario());
            var connector = InMemoryConnectorFactory.CreateWithScenario("TestCustom");

            // Act
            var customResult = await connector.ExecuteAsync("g.V().hasLabel('custom').count()", new Dictionary<string, object>());

            // Assert
            customResult.Should().NotBeEmpty();
            // The custom response should return a specific count
        }

        [Fact]
        public void DatabaseOptions_ShouldBeConfigurable()
        {
            // Arrange & Act
            var connector = InMemoryConnectorFactory.CreateWithScenario("BasicSocialNetwork", options =>
            {
                options.EnableDebugLogging = true;
                options.AutoGenerateIds = false;
                options.CaseSensitiveLabels = true;
            });

            // Assert
            connector.Options.EnableDebugLogging.Should().BeTrue();
            connector.Options.AutoGenerateIds.Should().BeFalse();
            connector.Options.CaseSensitiveLabels.Should().BeTrue();
        }

        [Fact]
        public void WithScenario_UsingScenarioProvider_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            var customScenario = new TestCustomScenario();

            // Act
            connector.WithScenario(customScenario);

            // Assert
            connector.GetVertex("custom1").Should().NotBeNull();
        }

        [Fact]
        public void ScenarioRegistry_Unregister_ShouldRemoveScenario()
        {
            // Arrange
            var customScenario = new TestCustomScenario();
            InMemoryScenarioRegistry.Register(customScenario);
            InMemoryScenarioRegistry.IsRegistered("TestCustom").Should().BeTrue();

            // Act
            var removed = InMemoryScenarioRegistry.Unregister("TestCustom");

            // Assert
            removed.Should().BeTrue();
            InMemoryScenarioRegistry.IsRegistered("TestCustom").Should().BeFalse();
        }

        [Fact]
        public void ScenarioRegistry_Clear_ShouldRemoveAllScenarios()
        {
            // Arrange
            var initialCount = InMemoryScenarioRegistry.GetScenarioNames().Count();
            initialCount.Should().BeGreaterThan(0);

            // Act
            InMemoryScenarioRegistry.Clear();

            // Assert
            InMemoryScenarioRegistry.GetScenarioNames().Should().BeEmpty();

            // Note: This affects global state, so we need to restore built-ins
            // In a real test, you'd use a fresh registry or reset mechanism
        }
    }
}