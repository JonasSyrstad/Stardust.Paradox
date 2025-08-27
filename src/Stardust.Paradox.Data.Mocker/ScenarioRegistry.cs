using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stardust.Paradox.Data.Mocker.Scenarios;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Registry for managing scenario providers
    /// </summary>
    public static class ScenarioRegistry
    {
        private static readonly Dictionary<string, IScenarioProvider> _scenarios = new Dictionary<string, IScenarioProvider>(StringComparer.OrdinalIgnoreCase);
        private static bool _builtInScenariosRegistered = false;

        static ScenarioRegistry()
        {
            RegisterBuiltInScenarios();
        }

        /// <summary>
        /// Register a scenario provider
        /// </summary>
        /// <param name="scenario">The scenario to register</param>
        public static void Register(IScenarioProvider scenario)
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
        public static void Register(params IScenarioProvider[] scenarios)
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
        public static IScenarioProvider GetScenario(string scenarioName)
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
        public static IReadOnlyDictionary<string, IScenarioProvider> GetAllScenarios()
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

            Register(
                new SocialNetworkScenario(),
                new OrganizationScenario(),
                new ECommerceScenario(),
                new UserManagementScenario()
            );

            _builtInScenariosRegistered = true;
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
                    .Where(t => typeof(IScenarioProvider).IsAssignableFrom(t) && 
                               !t.IsInterface && 
                               !t.IsAbstract &&
                               t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in scenarioTypes)
                {
                    try
                    {
                        var scenario = (IScenarioProvider)Activator.CreateInstance(type);
                        Register(scenario);
                    }
                    catch (Exception ex)
                    {
                        // Log warning about failed scenario registration
                        System.Diagnostics.Debug.WriteLine($"Failed to register scenario type {type.FullName}: {ex.Message}");
                    }
                }
            }
        }
    }
}