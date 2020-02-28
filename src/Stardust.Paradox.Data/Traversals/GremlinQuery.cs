using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Traversals
{
    public class GremlinQuery
    {
        protected internal readonly IGremlinLanguageConnector _connector;

        private readonly GremlinQuery _queryBase;
        protected Dictionary<string, object> _parameters;

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

        public string _query { get; protected internal set; }

        public virtual bool IsUpdatableGremlinQuery => false;

        public virtual Dictionary<string, object> Parameters
        {
            get
            {
                if (_queryBase != null)
                    return _queryBase.Parameters;
                return _parameters ?? (_parameters = new Dictionary<string, object>());
            }
        }

        public override string ToString()
        {
            return CompileQuery();
        }

        protected internal virtual string CompileQuery()
        {
            if (_query == null)
                return _queryBase.CompileQuery();
            return _query;
        }

        public async Task<IEnumerable<dynamic>> ExecuteAsync()
        {
            return await _connector.ExecuteAsync(CompileQuery(), Parameters);
        }

        internal virtual object ComposeParameter(object value)
        {
            if (_queryBase != null) return _queryBase.ComposeParameter(value);
            if (_connector == null || !_connector.CanParameterizeQueries)
            {
                if (value is string s) return $"'{s.EscapeGremlinString()}'";

                if (value is EpochDateTime epoch)
                    return epoch.Epoch.ToString(CultureInfo.InvariantCulture);
                if (value is DateTime dt)
                    return dt.Ticks.ToString(CultureInfo.InvariantCulture);
                return value?.ToString().ToLower();
            }

            string v;
            if (value is decimal o)
                if (o >= int.MaxValue)
                    return o;
            if (value is long l)
                if (l >= int.MaxValue)
                    return l;
            lock (Parameters)
            {
                v = $"__p{Parameters.Count}";
                if (value is EpochDateTime epoch)
                    Parameters.Add(v, epoch.Epoch);
                else Parameters.Add(v, value);
            }

            return v;
        }
    }
}