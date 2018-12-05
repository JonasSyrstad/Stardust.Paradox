using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data
{
    public class GremlinContext
    {
        internal readonly IGremlinLanguageConnector _connector;

        internal GremlinContext(IGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        public static bool ParallelSaveExecution { get; set; }

        public GremlinQuery V()=>new GremlinQuery(_connector,"g.V()");

        public GremlinQuery V(string id) => new GremlinQuery(_connector,$"g.V('{id}')");

        public GremlinQuery E() => new GremlinQuery(_connector, "g.E()");

        public GremlinQuery E(string id) => new GremlinQuery(_connector, $"g.E('{id}')");
    }
}