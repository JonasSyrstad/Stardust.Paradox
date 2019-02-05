using System.Globalization;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Internals;

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

		/// <summary>
		/// Does escaping of id value
		/// </summary>
		/// <param name="connector"></param>
		/// <param name="id"></param>
		/// <returns></returns>
        public static UpdatableGremlinQuery V(this IGremlinLanguageConnector connector, string id)
        {
	        
			return new UpdatableGremlinQuery(connector, $"g.V('{ id.EscapeGremlinString()}')");
        }

        public static UpdatableGremlinQuery V(this IGremlinLanguageConnector connector, int index)
        {
			
            return new UpdatableGremlinQuery(connector, $"g.V({index})");
        }
    }
}