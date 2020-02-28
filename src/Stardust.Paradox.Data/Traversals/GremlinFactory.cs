using System;

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
        private static IServiceProvider _resolver;
        public static IGremlinLanguageConnector G => Activate();

        private static IGremlinLanguageConnector Activate()
        {
            return _factoryMethod?.Invoke() ??
                   _resolver.GetService(typeof(IGremlinLanguageConnector)) as IGremlinLanguageConnector;
        }

        public static void SetActivatorFactory(Func<IGremlinLanguageConnector> factoryMetod)
        {
            _factoryMethod = factoryMetod;
        }

        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _resolver = serviceProvider;
        }
    }
}