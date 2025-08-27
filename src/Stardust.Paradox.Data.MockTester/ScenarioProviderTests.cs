using Stardust.Paradox.Data.Mocker;
using Stardust.Paradox.Data.Mocker.Scenarios;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Stardust.Paradox.Data.MockTester
{
    public class ScenarioProviderTests
    {
        [Fact]
        public void ScenarioRegistry_Should_RegisterBuiltInScenarios()
        {
            // Arrange & Act
            var availableScenarios = ScenarioRegistry.GetScenarioNames().ToArray();

            // Assert
            Assert.True(availableScenarios.Contains("SocialNetwork"));
            Assert.True(availableScenarios.Contains("Organization"));
            Assert.True(availableScenarios.Contains("ECommerce"));
            Assert.True(availableScenarios.Contains("UserManagement"));
            Assert.True(availableScenarios.Length >= 4);
        }

        [Fact]
        public void Should_CreateConnector_WithBuiltInScenario()
        {
            // Arrange & Act
            var connector = MockGremlinConnectorFactory.CreateWithScenario("SocialNetwork");

            // Assert
            Assert.NotNull(connector);
            Assert.True(connector.ConsumedRU >= 0);
        }

        [Fact]
        public async Task Should_ExecuteQuery_WithSocialNetworkScenario()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateWithScenario("SocialNetwork");

            // Act
            var users = await connector.ExecuteAsync("g.V().hasLabel('user')", new System.Collections.Generic.Dictionary<string, object>());

            // Assert
            Assert.NotNull(users);
            // The scenario should have populated some user data
        }

        [Fact]
        public void Should_RegisterCustomScenario()
        {
            // Arrange
            var customScenario = new TestCustomScenario();

            // Act
            ScenarioRegistry.Register(customScenario);
            var retrievedScenario = ScenarioRegistry.GetScenario("TestCustom");

            // Assert
            Assert.NotNull(retrievedScenario);
            Assert.Equal("TestCustom", retrievedScenario.ScenarioName);
            Assert.Equal("Test custom scenario", retrievedScenario.Description);

            // Cleanup
            ScenarioRegistry.Unregister("TestCustom");
        }

        [Fact]
        public void Should_CreateConnector_WithCustomScenario()
        {
            // Arrange
            var customScenario = new TestCustomScenario();

            // Act
            var connector = MockGremlinConnectorFactory.CreateWithScenario(customScenario);

            // Assert
            Assert.NotNull(connector);
        }

        [Fact]
        public void Should_CombineMultipleScenarios()
        {
            // Arrange & Act
            var connector = MockGremlinConnectorFactory.CreateWithScenarios("SocialNetwork", "UserManagement");

            // Assert
            Assert.NotNull(connector);
        }

        [Fact]
        public void Should_ApplyScenario_ToExistingConnector()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();

            // Act
            MockGremlinConnectorFactory.ApplyScenario(connector, "ECommerce");

            // Assert
            Assert.NotNull(connector);
        }

        [Fact]
        public void Should_GetScenarioInformation()
        {
            // Arrange & Act
            var scenario = ScenarioRegistry.GetScenario("Organization");

            // Assert
            Assert.NotNull(scenario);
            Assert.Equal("Organization", scenario.ScenarioName);
            Assert.Contains("organization", scenario.Description.ToLower());
        }

        [Fact]
        public void Should_HandleUnknownScenario()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<System.ArgumentException>(() =>
                MockGremlinConnectorFactory.CreateWithScenario("NonExistentScenario"));

            Assert.Contains("not found", exception.Message);
        }

        // Test custom scenario for demonstration
        private class TestCustomScenario : ScenarioProviderBase
        {
            public override string ScenarioName => "TestCustom";
            public override string Description => "Test custom scenario";

            protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
            {
                var vertices = new VertexDefinition[]
                {
                    new VertexDefinition("test1", "testEntity", Props(("name", "Test Entity 1")))
                };

                var edges = new EdgeDefinition[]
                {
                    // No edges for this simple test
                };

                return (vertices, edges);
            }

            protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
            {
                connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('testEntity'\)", (query, parameters) =>
                {
                    return MockExtensions.CreateVertexCollection(
                        ("test1", "testEntity", new System.Collections.Generic.Dictionary<string, object> { { "name", "Test Entity 1" } })
                    );
                });
            }
        }
    }
}