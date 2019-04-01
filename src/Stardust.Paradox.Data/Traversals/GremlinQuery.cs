using Stardust.Paradox.Data.Annotations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Traversals
{
	public class GremlinQuery
	{
		public override string ToString()
		{
			return CompileQuery();
		}

		protected internal readonly IGremlinLanguageConnector _connector;

		private readonly GremlinQuery _queryBase;
		public string _query;
		protected Dictionary<string, object> _parameters;

		public virtual bool IsUpdatableGremlinQuery => false;

		internal GremlinQuery(IGremlinLanguageConnector connector, string query)
		{
			_connector = connector;
			_query = query;
		}

		internal GremlinQuery(GremlinQuery baseQuery, string query)
		{
			_connector = baseQuery._connector;
			_queryBase = baseQuery;
			_query = query;
		}

		internal GremlinQuery(IGremlinLanguageConnector connector)
		{
			_query = "g";
		}

		internal GremlinQuery(IGremlinLanguageConnector connector, string query, string id)
		{
			_connector = connector;
			string v;
			lock (Parameters)
			{
				v = $"__p{Parameters.Count}";
				Parameters.Add(v, id);
			}

			_query = string.Format(query, v);
		}

		protected internal virtual string CompileQuery()
		{
			return _query;
		}

		public async Task<IEnumerable<dynamic>> ExecuteAsync()
		{
			return await _connector.ExecuteAsync(CompileQuery(), Parameters);
		}

		public virtual Dictionary<string, object> Parameters
		{
			get
			{
				if (_queryBase != null)
					return _queryBase.Parameters;
				return _parameters ?? (_parameters = new Dictionary<string, object>());
			}
		}

		internal virtual object ComposeParameter(object value)
		{
			if (_queryBase != null) return _queryBase.ComposeParameter(value);
			if (_connector == null || !_connector.CanParameterizeQueries)
			{
				if (value is string s)
				{
					return $"'{s.EscapeGremlinString()}'";
				}
				if (value is DateTime dt)
					return dt.Ticks.ToString(CultureInfo.InvariantCulture);
				return value?.ToString().ToLower();
			}
			string v;
			if (value is long || value is decimal)
			{
				if ((decimal)value >= int.MaxValue)
					return value;
			}
			lock (Parameters)
			{

				v = $"__p{Parameters.Count}";
				Parameters.Add(v, value);
			}
			return v;
		}
	}
}
