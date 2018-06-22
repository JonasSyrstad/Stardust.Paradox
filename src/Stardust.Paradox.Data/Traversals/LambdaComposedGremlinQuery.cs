using System;
using System.Linq;

namespace Stardust.Paradox.Data.Traversals
{
    public class LambdaComposedGremlinQuery : ComposedGremlinQuery
    {
        private readonly Func<GremlinQuery, string>[] _inner;
        private Func<GremlinQuery, GremlinQuery>[] _inner2;

        internal LambdaComposedGremlinQuery(GremlinQuery queryBase, string query, params Func<GremlinQuery,string>[] inner) : base(queryBase, query)
        {
            _inner = inner;
        }

        internal LambdaComposedGremlinQuery(GremlinQuery queryBase, string query, params Func<GremlinQuery, GremlinQuery>[] inner) : base(queryBase, query)
        {
            _inner2 = inner;
        }

        protected internal override string CompileQuery()
        {
            var query = _query.StartsWith(".") ? _query.Remove(0, 1) : _query;
            var baseQ = _queryBase.CompileQuery();
            if ((!string.IsNullOrWhiteSpace(baseQ) && !baseQ.EndsWith("."))) query = $".{query}";
            query = _inner!=null? string.Format(query,
                string.Join(",", _inner.Select(i => i.Invoke(new GremlinQuery(_connector, ""))))):
                string.Format(query,
                    string.Join(",", _inner2.Select(i => i.Invoke(new GremlinQuery(_connector, "")).CompileQuery()))); ;
            return _queryBase.CompileQuery() + query;
        }
    }
}