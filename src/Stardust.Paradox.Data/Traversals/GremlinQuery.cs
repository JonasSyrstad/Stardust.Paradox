using System.Collections.Generic;
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

        public string _query;

        public virtual bool IsUpdatableGremlinQuery => false;

        internal GremlinQuery(IGremlinLanguageConnector connector, string query)
        {
            _connector = connector;
            _query = query;
        }

        internal GremlinQuery(IGremlinLanguageConnector connector)
        {
            _query = "g";
        }

        protected internal virtual string CompileQuery()
        {
            return _query;
        }

        public async Task<IEnumerable<dynamic>> ExecuteAsync()
        {
            return await _connector.ExecuteAsync(CompileQuery());
        }
    }
}
