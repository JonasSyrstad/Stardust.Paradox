using System;
using Stardust.Nucleus;

namespace Stardust.Paradox.Data.Traversals
{
    //public static class Gremlin
    //{
    //    public static IGremlinLanguageConnector Spawn()//:p
    //    {
    //        return GremlinFactory.G;
    //    }
    //}

    public static class GremlinFactory
    {
        private static Func<IGremlinLanguageConnector> _factoryMethod;
        public static IGremlinLanguageConnector G => Activate();

        private static IGremlinLanguageConnector Activate()
        {
            return _factoryMethod?.Invoke() ?? Resolver.CreateScopedResolver().GetService<IGremlinLanguageConnector>();
        }

        public static void SetActivatorFactory(Func<IGremlinLanguageConnector> factoryMetod)
        {
            _factoryMethod = factoryMetod;
        }
    }
}