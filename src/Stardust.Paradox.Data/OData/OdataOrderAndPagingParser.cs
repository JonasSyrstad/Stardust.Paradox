using System;
using Stardust.Paradox.Data.Annotations.OData;
using Stardust.Paradox.Data.Traversals;
using OrderingTypes = Stardust.Paradox.Data.Traversals.OrderingTypes;

namespace Stardust.Paradox.Data.OData
{
    internal static class OdataOrderAndPagingParser
    {
        public static GremlinQuery ApplyODataOrdering(this GremlinQuery baseGremlinQuery, OrderingOptions ordering)
        {
            if (ordering == null || string.IsNullOrWhiteSpace(ordering.PropertyName))
                return baseGremlinQuery;

            var orderType = ordering.Ordering == OrderingType.Ascending
                ? OrderingTypes.Incr 
                : OrderingTypes.Decr;

            return baseGremlinQuery.Order().By(ordering.PropertyName, orderType);
        }

        public static GremlinQuery ApplyODataPaging(this GremlinQuery baseGremlinQuery, int? take, int? top, int? tail)
        {
            if (top != null && tail != null) 
                throw new ArgumentException("You cannot use both top and tail in the same query");

            // Apply $top or $take (they are equivalent)
            if (top.HasValue)
            {
                return baseGremlinQuery.Limit(top.Value);
            }

            if (take.HasValue)
            {
                return baseGremlinQuery.Limit(take.Value);
            }

            // Apply $tail (last N elements)
            if (tail.HasValue)
            {
                return baseGremlinQuery.Tail(tail.Value);
            }

            return baseGremlinQuery;
        }
    }
}