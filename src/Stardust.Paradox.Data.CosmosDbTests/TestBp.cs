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
            configuration.AddEntityBinding((type, type1) =>
                {
                    configuration.Bind(type).To(type1).SetTransientScope();
                    
                })
                .Bind<IGremlinLanguageConnector>().To<Class1>().SetTransientScope();
        }

        public Type LoggingType => typeof(LoggingDefaultImplementation);
    }
}