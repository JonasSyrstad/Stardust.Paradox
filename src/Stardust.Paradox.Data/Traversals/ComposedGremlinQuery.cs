namespace Stardust.Paradox.Data.Traversals
{
    public class ComposedGremlinQuery : GremlinQuery
    {
        protected readonly GremlinQuery _queryBase;

        internal ComposedGremlinQuery(GremlinQuery queryBase, string query) : base(queryBase._connector, query)
        {
            _queryBase = queryBase;
        }

        protected internal override string CompileQuery()
        {
            var query = _query.StartsWith(".") ? _query.Remove(0, 1) : _query;
            var baseQ = _queryBase.CompileQuery();
            if ((!string.IsNullOrWhiteSpace(baseQ) && !baseQ.EndsWith("."))) query = $".{query}";
            return baseQ + query;
        }

        public override bool IsUpdatableGremlinQuery => _queryBase.IsUpdatableGremlinQuery;
    }
}