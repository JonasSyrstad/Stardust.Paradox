namespace Stardust.Paradox.Data.Traversals
{
    public class PredicateGremlinQuery:GremlinQuery
    {
        internal PredicateGremlinQuery(IGremlinLanguageConnector connector) : base(connector, "")
        {
           
        }

        public GremlinQuery P => this.P();
    }
}