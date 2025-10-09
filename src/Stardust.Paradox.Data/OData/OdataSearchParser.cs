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
            
            // For simplicity, use a where clause that checks if any property contains the search term
            // This is a simplified implementation - a full implementation would need more complex logic
            var result = baseGremlinQuery;
            
            // Apply OR condition across all searchable properties  
            if (searchPropertyNames.Length > 0)
            {
                result = result.Where(p =>
                {
                    // Build OR chain for multiple properties
                    var orPredicates = searchPropertyNames.Select<string, Func<PredicateGremlinQuery, GremlinQuery>>(
                        propName => q => q.Values(propName).Is(v => v.Containing(searchTerm))
                    ).ToArray();
                    return p.Or(orPredicates);
                });
            }
            
            return result;
        }
    }
}