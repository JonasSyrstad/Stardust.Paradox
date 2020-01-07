namespace Stardust.Paradox.Data.Traversals.Typed
{
    public class GremlinQuery<T>:GremlinQuery
    {
        internal GremlinQuery(IGremlinLanguageConnector connector, string query) : base(connector, query)
        {
        }

        internal GremlinQuery(GremlinQuery baseQuery, string query) : base(baseQuery, query)
        {
        }

        internal GremlinQuery(IGremlinLanguageConnector connector) : base(connector)
        {
        }

        internal GremlinQuery(IGremlinLanguageConnector connector, string query, string id) : base(connector, query, id)
        {
        }

        public GremlinQuery(GremlinQuery baseQuery) : base(baseQuery, null)
        {
        }

       
    }
}