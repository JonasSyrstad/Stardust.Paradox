using System;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Factory for creating and configuring mock Gremlin connectors
    /// </summary>
    public static class MockGremlinConnectorFactory
    {
        /// <summary>
        /// Create a basic mock connector with default settings
        /// </summary>
        public static MockGremlinLanguageConnector Create()
        {
            return new MockGremlinLanguageConnector();
        }

        /// <summary>
        /// Create a mock connector with custom options
        /// </summary>
        public static MockGremlinLanguageConnector Create(MockConnectorOptions options)
        {
            return new MockGremlinLanguageConnector(options);
        }

        /// <summary>
        /// Create a mock connector with configuration action
        /// </summary>
        public static MockGremlinLanguageConnector Create(Action<MockConnectorOptions> configureOptions)
        {
            var options = new MockConnectorOptions();
            configureOptions?.Invoke(options);
            return new MockGremlinLanguageConnector(options);
        }

        /// <summary>
        /// Create a scenario builder for the connector
        /// </summary>
        public static MockScenarioBuilder CreateScenario()
        {
            var connector = Create();
            return new MockScenarioBuilder(connector);
        }

        /// <summary>
        /// Create a scenario builder with a specific connector
        /// </summary>
        public static MockScenarioBuilder CreateScenario(MockGremlinLanguageConnector connector)
        {
            return new MockScenarioBuilder(connector);
        }

        /// <summary>
        /// Create a pre-configured mock connector for testing common scenarios
        /// </summary>
        public static MockGremlinLanguageConnector CreateForTesting()
        {
            return Create(options =>
            {
                options.LogQueries = true;
                options.EnableOperationSimulation = true;
                options.MaintainGraphState = true;
                options.SimulatedRUPerQuery = 1.0;
            });
        }

        /// <summary>
        /// Create a mock connector with a pre-built scenario from the registry
        /// </summary>
        /// <param name="scenarioName">Name of the scenario to load</param>
        /// <returns>Configured mock connector with scenario data</returns>
        public static MockGremlinLanguageConnector CreateWithScenario(string scenarioName)
        {
            var connector = CreateForTesting();
            ApplyScenario(connector, scenarioName);
            return connector;
        }

        /// <summary>
        /// Create a mock connector with a custom scenario provider
        /// </summary>
        /// <param name="scenarioProvider">The scenario provider to use</param>
        /// <returns>Configured mock connector with scenario data</returns>
        public static MockGremlinLanguageConnector CreateWithScenario(IScenarioProvider scenarioProvider)
        {
            var connector = CreateForTesting();
            ApplyScenario(connector, scenarioProvider);
            return connector;
        }

        /// <summary>
        /// Create a mock connector with multiple scenarios
        /// </summary>
        /// <param name="scenarioNames">Names of scenarios to combine</param>
        /// <returns>Configured mock connector with all scenario data</returns>
        public static MockGremlinLanguageConnector CreateWithScenarios(params string[] scenarioNames)
        {
            var connector = CreateForTesting();
            foreach (var scenarioName in scenarioNames)
            {
                ApplyScenario(connector, scenarioName);
            }
            return connector;
        }

        /// <summary>
        /// Create a mock connector from a pre-built template (legacy method - use CreateWithScenario)
        /// </summary>
        [Obsolete("Use CreateWithScenario instead for better scenario management")]
        public static MockGremlinLanguageConnector CreateFromTemplate(string templateName)
        {
            return CreateWithScenario(templateName);
        }

        /// <summary>
        /// Apply a scenario to an existing connector
        /// </summary>
        /// <param name="connector">The connector to configure</param>
        /// <param name="scenarioName">Name of the scenario to apply</param>
        public static void ApplyScenario(MockGremlinLanguageConnector connector, string scenarioName)
        {
            var scenario = ScenarioRegistry.GetScenario(scenarioName);
            if (scenario == null)
            {
                throw new ArgumentException($"Scenario '{scenarioName}' not found. Available scenarios: {string.Join(", ", ScenarioRegistry.GetScenarioNames())}");
            }

            ApplyScenario(connector, scenario);
        }

        /// <summary>
        /// Apply a scenario provider to an existing connector
        /// </summary>
        /// <param name="connector">The connector to configure</param>
        /// <param name="scenarioProvider">The scenario provider to apply</param>
        public static void ApplyScenario(MockGremlinLanguageConnector connector, IScenarioProvider scenarioProvider)
        {
            if (connector == null) throw new ArgumentNullException(nameof(connector));
            if (scenarioProvider == null) throw new ArgumentNullException(nameof(scenarioProvider));

            scenarioProvider.ConfigureScenario(connector);
        }

        /// <summary>
        /// Create a connector pre-configured for social network scenarios (legacy method)
        /// </summary>
        [Obsolete("Use CreateWithScenario(\"SocialNetwork\") instead")]
        public static MockGremlinLanguageConnector CreateSocialNetworkScenario()
        {
            return CreateWithScenario("SocialNetwork");
        }

        /// <summary>
        /// Create a connector pre-configured for organization scenarios (legacy method)
        /// </summary>
        [Obsolete("Use CreateWithScenario(\"Organization\") instead")]
        public static MockGremlinLanguageConnector CreateOrganizationScenario()
        {
            return CreateWithScenario("Organization");
        }

        /// <summary>
        /// Create a connector pre-configured for e-commerce scenarios (legacy method)
        /// </summary>
        [Obsolete("Use CreateWithScenario(\"ECommerce\") instead")]
        public static MockGremlinLanguageConnector CreateECommerceScenario()
        {
            return CreateWithScenario("ECommerce");
        }

        /// <summary>
        /// Create a connector pre-configured for user management scenarios (legacy method)
        /// </summary>
        [Obsolete("Use CreateWithScenario(\"UserManagement\") instead")]
        public static MockGremlinLanguageConnector CreateUserManagementScenario()
        {
            return CreateWithScenario("UserManagement");
        }
    }
}