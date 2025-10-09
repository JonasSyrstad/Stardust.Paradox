using System;
using System.Collections.Generic;
using System.Text;
using Stardust.Paradox.Data.Annotations.OData;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.OData
{
    public static class ODataExtensions
    {
        public static GremlinQuery ApplyODataQuery(this GremlinQuery baseGremlinGremlinQuery, ODataSearchOptions query)
        {
            if (query == null) return baseGremlinGremlinQuery;
            return baseGremlinGremlinQuery.ApplyODataFilter(query.Filter)
                .ApplyODataSearch(query.Search, query.SearchableProperties)
                .ApplyODataOrdering(query.OrderBy)
                .ApplyODataPaging(query.Take, query.Top, query.Tail);
        }
    }
}
