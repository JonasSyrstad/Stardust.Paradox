using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.OData
{
    internal static class OdataFilterParser
    {
        public static GremlinQuery ApplyODataFilter(this GremlinQuery baseGremlinQuery, string filter)
        {
            return baseGremlinQuery;
        }
    }
}