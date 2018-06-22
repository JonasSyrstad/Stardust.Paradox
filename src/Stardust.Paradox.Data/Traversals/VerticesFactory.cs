using System.Globalization;

namespace Stardust.Paradox.Data.Traversals
{
    public static class VerticesFactory
    {
        public static GremlinQuery V(this IGremlinLanguageConnector connector)
        {
            return new GremlinQuery(connector, "g.V()");
        }

        public static GremlinQuery WithSackV(this IGremlinLanguageConnector connector, float sack)
        {
            return new GremlinQuery(connector, $"g.withSack({sack.ToString(CultureInfo.InvariantCulture)}f).V()");
        }

        public static UpdatableGremlinQuery V(this IGremlinLanguageConnector connector, string id)
        {
            return new UpdatableGremlinQuery(connector, $"g.V('{id}')");
        }

        public static UpdatableGremlinQuery V(this IGremlinLanguageConnector connector, int index)
        {
            return new UpdatableGremlinQuery(connector, $"g.V({index})");
        }
    }
}