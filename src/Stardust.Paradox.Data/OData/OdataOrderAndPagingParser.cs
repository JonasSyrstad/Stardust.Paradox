using System;
using Stardust.Paradox.Data.Annotations.OData;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.OData
{
    internal static class OdataOrderAndPagingParser
    {
        public static GremlinQuery ApplyODataOrdering(this GremlinQuery baseGremlinQuery, OrderingOptions ordering)
        {
            return baseGremlinQuery;
        }

        public static GremlinQuery ApplyODataPaging(this GremlinQuery baseGremlinQuery, int? take, int? top, int? tail)
        {
            if(top!=null&&tail!=null) throw new ArgumentException("You cannot use both top and tail in the same query");
            return baseGremlinQuery;
        }
    }
}