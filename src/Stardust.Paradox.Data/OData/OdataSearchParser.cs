using System;
using System.Linq;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.OData
{
    internal static class OdataSearchParser
    {
        public static GremlinQuery ApplyODataSearch(this GremlinQuery baseGremlinQuery, string searchClause, string[] searchPropertyNames)
        {
            if (string.IsNullOrWhiteSpace(searchClause)) return baseGremlinQuery;
            if (searchPropertyNames == null || searchPropertyNames.Length == 0) return baseGremlinQuery;

            // Remove quotes from search clause
            var searchTerm = searchClause.Trim().Trim('"', '\'');
            
            // Apply OR condition across all searchable properties
            // Use Has with Containing predicate for each property
            if (searchPropertyNames.Length > 0)
            {
                var orPredicates = searchPropertyNames.Select<string, Func<PredicateGremlinQuery, GremlinQuery>>(
                    propName => q => q.Has(propName, v => v.Containing(searchTerm))
                ).ToArray();
          
                return baseGremlinQuery.Or(orPredicates);
            }
            
            return baseGremlinQuery;
        }
    }
}