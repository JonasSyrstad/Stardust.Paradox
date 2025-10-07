using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.OData
{
    internal static class OdataSearchParser
    {
        public static GremlinQuery ApplyODataSearch(this GremlinQuery baseGremlinQuery, string searchClause, string[] searchPropertyNames)
        {
            return baseGremlinQuery;
        }
    }
}