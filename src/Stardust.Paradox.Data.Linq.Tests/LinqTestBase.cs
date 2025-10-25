using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Extensions;
using Stardust.Paradox.Data.InMemory.Factory;
using Stardust.Paradox.Data.Linq.Tests.Scenarios;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Base class for LINQ tests providing common infrastructure and scenario setup
    /// </summary>
    public abstract class LinqTestBase : IDisposable
    {
        protected InMemoryGremlinLanguageConnector Connector { get; private set; } = null!;
        protected InMemoryGraphDatabase Database { get; private set; } = null!;
        protected LinqTestContext Context { get; private set; } = null!;

        protected LinqTestBase()
        {
            InitializeTestEnvironment();
        }

        private void InitializeTestEnvironment()
        {
            // Create in-memory database
            Database = new InMemoryGraphDatabase();
            
            // Apply scenario
            var scenario = new LinqTestScenario();
            scenario.ConfigureScenario(Database);
            
            // Create connector  
            Connector = InMemoryConnectorFactory.Create(Database);
            
            // Initialize GremlinFactory for LINQ queries
            GremlinFactory.SetActivatorFactory(() => Connector);
            
            // Create context
            Context = new LinqTestContext(Connector);
        }

        protected void ResetDatabase()
        {
            Dispose();
            InitializeTestEnvironment();
        }

        public void Dispose()
        {
            Context?.Dispose();
            Connector?.Dispose();
        }
    }
}
