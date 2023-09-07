using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Traversals
{
	public class LambdaComposedGremlinQuery : ComposedGremlinQuery
	{
		private readonly Func<GremlinQuery, string>[] _inner;
		private Func<GremlinQuery, GremlinQuery>[] _inner2;

		internal LambdaComposedGremlinQuery(GremlinQuery queryBase, string query, params Func<GremlinQuery, string>[] inner) : base(queryBase, query)
		{
			_inner = inner;
		}

		internal LambdaComposedGremlinQuery(GremlinQuery queryBase, string query, params Func<GremlinQuery, GremlinQuery>[] inner) : base(queryBase, query)
		{
			_inner2 = inner;
		}

		protected internal override string CompileQuery()
        {
            var baseQ = _queryBase.CompileQuery();

            var query = _query.StartsWith(".") ? _query.Remove(0, 1) : _query;
			if ((!string.IsNullOrWhiteSpace(baseQ) && !baseQ.EndsWith("."))) query = $".{query}";
			if(_inner2!=null)
            {
                string.Format(query,
                    string.Join(",", _inner2.Select(i1 => i1.Invoke(new GremlinQuery(_queryBase, "")).CompileQuery())));
                return _queryBase.CompileQuery() + query;
            }

            if (_inner == null) throw new IndexOutOfRangeException("No query functions found");
			//query = _inner != null ? string.Format(query,
			//	string.Join(",", _inner.Select(i => i.Invoke(new GremlinQuery(_queryBase, ""))))) :
			//	string.Format(query,
			//		string.Join(",", _inner2.Select(i => i.Invoke(new GremlinQuery(_queryBase, "")).CompileQuery())));
			var q = new string[_inner.Length];
            var i = 0;
            foreach (var func in _inner)
            {
                q[i] = func(_queryBase);
                i++;
            }
            var formated = string.Format(query, q);
            return baseQ + formated;
		}

		public override Dictionary<string, object> Parameters
		{
			get
			{
				if (_queryBase != null)
					return _queryBase.Parameters;
				return _parameters ?? (_parameters = new Dictionary<string, object>());
			}
		}
	}
}