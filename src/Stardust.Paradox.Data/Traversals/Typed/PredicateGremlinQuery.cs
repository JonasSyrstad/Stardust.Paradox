namespace Stardust.Paradox.Data.Traversals.Typed
{
    public class PredicateGremlinQuery<T> : PredicateGremlinQuery
    {
        internal PredicateGremlinQuery(GremlinQuery<T> query) : base(query)
        {
        }

        public GremlinQuery P => this.P();
    }
}