using System;
using Stardust.Core;
using Stardust.Nucleus;
using Stardust.Paradox.Data;
using Stardust.Particles;

namespace Stardust.Paradox.CosmosDbTest
{
    public class TestBp : IBlueprint
    {
        public void Bind(IConfigurator configuration)
        {
            configuration.Bind<IGremlinLanguageConnector>().To<Class1>().SetTransientScope();
        }

        public Type LoggingType => typeof(LoggingDefaultImplementation);
    }
}