using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory.Extensions
{
    /// <summary>
    /// Extension methods for loading scenarios into InMemory database
    /// </summary>
    public static class InMemoryScenarioExtensions
    {
        /// <summary>
        /// Create an InMemoryGremlinLanguageConnector with a specific scenario applied
        /// </summary>
        /// <param name="scenarioName">Name of the scenario to apply</param>
        /// <param name="options">Optional database options</param>
        /// <returns>Configured connector</returns>
        public static InMemoryGremlinLanguageConnector CreateWithScenario(string scenarioName, InMemoryDatabaseOptions options = null)
        {
            // Ensure built-in scenarios are registered
            InMemoryScenarioRegistry.EnsureBuiltInScenariosRegistered();
            
            var connector = new InMemoryGremlinLanguageConnector(options ?? new InMemoryDatabaseOptions());
            
            if (!InMemoryScenarioRegistry.ApplyScenario(connector.Database, scenarioName))
            {
                throw new ArgumentException($"Scenario '{scenarioName}' not found. Available scenarios: {string.Join(", ", InMemoryScenarioRegistry.GetScenarioNames())}");
            }
            
            return connector;
        }

        /// <summary>
        /// Create an InMemoryGremlinLanguageConnector with multiple scenarios applied
        /// </summary>
        /// <param name="scenarioNames">Names of the scenarios to apply</param>
        /// <param name="options">Optional database options</param>
        /// <returns>Configured connector</returns>
        public static InMemoryGremlinLanguageConnector CreateWithScenarios(string[] scenarioNames, InMemoryDatabaseOptions options = null)
        {
            // Ensure built-in scenarios are registered
            InMemoryScenarioRegistry.EnsureBuiltInScenariosRegistered();
            
            var connector = new InMemoryGremlinLanguageConnector(options ?? new InMemoryDatabaseOptions());
            
            int applied = InMemoryScenarioRegistry.ApplyScenarios(connector.Database, scenarioNames);
            if (applied == 0)
            {
                throw new ArgumentException($"No scenarios could be applied. Available scenarios: {string.Join(", ", InMemoryScenarioRegistry.GetScenarioNames())}");
            }
            
            return connector;
        }

        /// <summary>
        /// Create an InMemoryGremlinLanguageConnector with multiple scenarios applied
        /// </summary>
        /// <param name="scenarioNames">Names of the scenarios to apply</param>
        /// <param name="options">Optional database options</param>
        /// <returns>Configured connector</returns>
        public static InMemoryGremlinLanguageConnector CreateWithScenarios(InMemoryDatabaseOptions options, params string[] scenarioNames)
        {
            return CreateWithScenarios(scenarioNames, options);
        }

        /// <summary>
        /// Apply a scenario to an existing connector
        /// </summary>
        /// <param name="connector">The connector to configure</param>
        /// <param name="scenarioName">Name of the scenario to apply</param>
        /// <returns>The same connector for method chaining</returns>
        public static InMemoryGremlinLanguageConnector WithScenario(this InMemoryGremlinLanguageConnector connector, string scenarioName)
        {
            if (!InMemoryScenarioRegistry.ApplyScenario(connector.Database, scenarioName))
            {
                throw new ArgumentException($"Scenario '{scenarioName}' not found. Available scenarios: {string.Join(", ", InMemoryScenarioRegistry.GetScenarioNames())}");
            }
            
            return connector;
        }

        /// <summary>
        /// Apply multiple scenarios to an existing connector
        /// </summary>
        /// <param name="connector">The connector to configure</param>
        /// <param name="scenarioNames">Names of the scenarios to apply</param>
        /// <returns>The same connector for method chaining</returns>
        public static InMemoryGremlinLanguageConnector WithScenarios(this InMemoryGremlinLanguageConnector connector, params string[] scenarioNames)
        {
            int applied = InMemoryScenarioRegistry.ApplyScenarios(connector.Database, scenarioNames);
            if (applied == 0)
            {
                throw new ArgumentException($"No scenarios could be applied. Available scenarios: {string.Join(", ", InMemoryScenarioRegistry.GetScenarioNames())}");
            }
            
            return connector;
        }

        /// <summary>
        /// Apply a scenario provider directly to an existing connector
        /// </summary>
        /// <param name="connector">The connector to configure</param>
        /// <param name="scenario">The scenario provider to apply</param>
        /// <returns>The same connector for method chaining</returns>
        public static InMemoryGremlinLanguageConnector WithScenario(this InMemoryGremlinLanguageConnector connector, IInMemoryScenarioProvider scenario)
        {
            scenario.ConfigureScenario(connector.Database);
            return connector;
        }

        /// <summary>
        /// Clear all data and scenarios from a connector
        /// </summary>
        /// <param name="connector">The connector to clear</param>
        /// <returns>The same connector for method chaining</returns>
        public static InMemoryGremlinLanguageConnector ClearScenarios(this InMemoryGremlinLanguageConnector connector)
        {
            connector.Clear();
            return connector;
        }

        /// <summary>
        /// Get information about available scenarios
        /// </summary>
        /// <returns>Dictionary of scenario names and descriptions</returns>
        public static System.Collections.Generic.Dictionary<string, string> GetAvailableScenarios()
        {
            // Ensure built-in scenarios are registered
            InMemoryScenarioRegistry.EnsureBuiltInScenariosRegistered();
            
            return InMemoryScenarioRegistry.GetAllScenarios()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Description);
        }

        /// <summary>
        /// List all available scenario names
        /// </summary>
        /// <returns>Array of scenario names</returns>
        public static string[] ListAvailableScenarios()
        {
            // Ensure built-in scenarios are registered
            InMemoryScenarioRegistry.EnsureBuiltInScenariosRegistered();
            
            return InMemoryScenarioRegistry.GetScenarioNames().ToArray();
        }
    }
}
