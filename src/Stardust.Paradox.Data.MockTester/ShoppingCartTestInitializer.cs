using Stardust.Paradox.Data.MockTester.Scenarios;
using Stardust.Paradox.Data.Mocker;
using System.Runtime.CompilerServices;
using System;

[assembly: InternalsVisibleTo("Stardust.Paradox.Data.MockTester")]

namespace Stardust.Paradox.Data.MockTester
{
    /// <summary>
    /// Initialization class for shopping cart test scenarios
    /// </summary>
    public static class ShoppingCartTestInitializer
    {
        private static readonly object _lock = new object();
        private static volatile bool _initialized = false;

        /// <summary>
        /// Register all shopping cart scenarios
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // Register shopping cart scenarios
                    ScenarioRegistry.Register(
                        new EmptyCartScenario(),
                        new ActiveCartScenario(),
                        new AbandonedCartScenario(),
                        new CustomerWithOrdersScenario(),
                        new MultipleCustomersScenario()
                    );

                    _initialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize shopping cart scenarios: {ex.Message}");
                    // Don't rethrow - allow tests to continue even if scenarios fail to register
                }
            }
        }

        /// <summary>
        /// Check if scenarios are registered
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Force re-initialization (useful for testing)
        /// </summary>
        public static void ForceReInitialize()
        {
            lock (_lock)
            {
                _initialized = false;
                Initialize();
            }
        }
    }
}