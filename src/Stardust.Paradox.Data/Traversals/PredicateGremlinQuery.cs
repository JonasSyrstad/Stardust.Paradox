namespace Stardust.Paradox.Data.Traversals
{
    public class PredicateGremlinQuery : GremlinQuery
    {
        private GremlinQuery _queryBase;

        internal PredicateGremlinQuery(GremlinQuery query) : base(query, "")
        {
            _queryBase = query;
        }

        public GremlinQuery P => this.P();
    }
}