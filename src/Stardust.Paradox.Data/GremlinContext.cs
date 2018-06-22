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

        public GremlinQuery V()=>new GremlinQuery(_connector,"g.V()");

        public GremlinQuery V(string id) => new GremlinQuery(_connector,$"g.V('{id}')");
    }
}