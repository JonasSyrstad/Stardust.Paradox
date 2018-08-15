using System;
using Stardust.Core;
using Stardust.Nucleus;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Providers.Gremlin;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;

namespace Stardust.Paradox.CosmosDbTest
{
    public class TestBp : IBlueprint
    {
        public void Bind(IConfigurator configuration)
        {
            configuration.AddEntityBinding((type, type1) =>
                {
                    configuration.Bind(type).To(type1).SetTransientScope();
                    
                })
                .Bind<IGremlinLanguageConnector>().ToConstructor(s=>new GremlinNetLanguageConnector("jonas-graphtest.gremlin.cosmosdb.azure.com", "graphTest", "services", "1TKgMc0u6F0MOBQi4jExGm1uAfOMHcXxylcvL55qV7FiCKx5LhTIW0FVXvJ68zdzFnFaS58yPtlxmBLmbDka1A=="));
            GremlinFactory.SetActivatorFactory(()=> new GremlinNetLanguageConnector("jonas-graphtest.gremlin.cosmosdb.azure.com", "graphTest", "services", "1TKgMc0u6F0MOBQi4jExGm1uAfOMHcXxylcvL55qV7FiCKx5LhTIW0FVXvJ68zdzFnFaS58yPtlxmBLmbDka1A=="));
        }

        public Type LoggingType => typeof(LoggingDefaultImplementation);
    }
}