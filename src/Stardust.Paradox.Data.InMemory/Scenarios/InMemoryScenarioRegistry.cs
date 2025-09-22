using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stardust.Paradox.Data.InMemory.Scenarios
{
    /// <summary>
    /// Registry for managing InMemory database scenario providers
    /// </summary>
    public static class InMemoryScenarioRegistry
    {
        private static readonly Dictionary<string, IInMemoryScenarioProvider> _scenarios = 
            new Dictionary<string, IInMemoryScenarioProvider>(StringComparer.OrdinalIgnoreCase);
        private static bool _builtInScenariosRegistered = false;

        static InMemoryScenarioRegistry()
        {
            RegisterBuiltInScenarios();
        }

        /// <summary>
        /// Register a scenario provider
        /// </summary>
        /// <param name="scenario">The scenario to register</param>
        public static void Register(IInMemoryScenarioProvider scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (string.IsNullOrWhiteSpace(scenario.ScenarioName))
                throw new ArgumentException("Scenario name cannot be null or empty", nameof(scenario));

            _scenarios[scenario.ScenarioName] = scenario;
        }

        /// <summary>
        /// Register multiple scenarios
        /// </summary>
        /// <param name="scenarios">The scenarios to register</param>
        public static void Register(params IInMemoryScenarioProvider[] scenarios)
        {
            foreach (var scenario in scenarios)
            {
                Register(scenario);
            }
        }

        /// <summary>
        /// Get a scenario by name
        /// </summary>
        /// <param name="scenarioName">The name of the scenario</param>
        /// <returns>The scenario provider if found, null otherwise</returns>
        public static IInMemoryScenarioProvider GetScenario(string scenarioName)
        {
            _scenarios.TryGetValue(scenarioName, out var scenario);
            return scenario;
        }

        /// <summary>
        /// Check if a scenario is registered
        /// </summary>
        /// <param name="scenarioName">The name of the scenario</param>
        /// <returns>True if the scenario is registered</returns>
        public static bool IsRegistered(string scenarioName)
        {
            return _scenarios.ContainsKey(scenarioName);
        }

        /// <summary>
        /// Get all registered scenarios
        /// </summary>
        /// <returns>Dictionary of scenario name to scenario provider</returns>
        public static IReadOnlyDictionary<string, IInMemoryScenarioProvider> GetAllScenarios()
        {
            return _scenarios.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Get all registered scenario names
        /// </summary>
        /// <returns>Collection of scenario names</returns>
        public static IEnumerable<string> GetScenarioNames()
        {
            return _scenarios.Keys;
        }

        /// <summary>
        /// Remove a scenario from the registry
        /// </summary>
        /// <param name="scenarioName">The name of the scenario to remove</param>
        /// <returns>True if the scenario was removed, false if it wasn't found</returns>
        public static bool Unregister(string scenarioName)
        {
            return _scenarios.Remove(scenarioName);
        }

        /// <summary>
        /// Clear all scenarios from the registry
        /// </summary>
        public static void Clear()
        {
            _scenarios.Clear();
            _builtInScenariosRegistered = false;
        }

        /// <summary>
        /// Register all built-in scenarios
        /// </summary>
        private static void RegisterBuiltInScenarios()
        {
            if (_builtInScenariosRegistered) return;

            try
            {
                Register(
                    new BasicSocialNetworkScenario(),
                    new SimpleECommerceScenario(),
                    new OrganizationHierarchyScenario(),
                    new UserRoleManagementScenario(),
                    new GraphTraversalTestScenario()
                );

                _builtInScenariosRegistered = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register built-in scenarios: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensure built-in scenarios are registered (for testing purposes)
        /// </summary>
        public static void EnsureBuiltInScenariosRegistered()
        {
            RegisterBuiltInScenarios();
        }

        /// <summary>
        /// Automatically discover and register scenarios from assemblies
        /// </summary>
        /// <param name="assemblies">Assemblies to scan for scenarios</param>
        public static void DiscoverAndRegister(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                assemblies = new[] { Assembly.GetCallingAssembly() };
            }

            foreach (var assembly in assemblies)
            {
                var scenarioTypes = assembly.GetTypes()
                    .Where(t => typeof(IInMemoryScenarioProvider).IsAssignableFrom(t) && 
                               !t.IsInterface && 
                               !t.IsAbstract &&
                               t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in scenarioTypes)
                {
                    try
                    {
                        var scenario = (IInMemoryScenarioProvider)Activator.CreateInstance(type);
                        Register(scenario);
                    }
                    catch (Exception ex)
                    {
                        // Log warning about failed scenario registration
                        System.Diagnostics.Debug.WriteLine($"Failed to register InMemory scenario type {type.FullName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Apply a scenario to an existing database
        /// </summary>
        /// <param name="database">Database to configure</param>
        /// <param name="scenarioName">Name of the scenario to apply</param>
        /// <returns>True if scenario was found and applied</returns>
        public static bool ApplyScenario(InMemoryGraphDatabase database, string scenarioName)
        {
            var scenario = GetScenario(scenarioName);
            if (scenario != null)
            {
                scenario.ConfigureScenario(database);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Apply multiple scenarios to an existing database
        /// </summary>
        /// <param name="database">Database to configure</param>
        /// <param name="scenarioNames">Names of the scenarios to apply</param>
        /// <returns>Count of scenarios successfully applied</returns>
        public static int ApplyScenarios(InMemoryGraphDatabase database, params string[] scenarioNames)
        {
            int applied = 0;
            foreach (var scenarioName in scenarioNames)
            {
                if (ApplyScenario(database, scenarioName))
                {
                    applied++;
                }
            }
            return applied;
        }
    }
}
