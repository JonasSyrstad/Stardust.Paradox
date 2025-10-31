using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stardust.Paradox.Data.Infrastructure
{
    /// <summary>
   /// Provides optimized string building utilities to reduce allocations and improve performance.
    /// </summary>
    internal static class StringBuilderOptimizer
    {
    /// <summary>
     /// Builds a concatenated string from an enumerable with an optional prefix.
    /// Optimized for scenarios like building Gremlin update statements.
        /// </summary>
   public static string BuildConcatenatedString<T>(string prefix, IEnumerable<T> items, System.Func<T, string> selector)
        {
       var itemsList = items as IList<T> ?? items.ToList();
            if (itemsList.Count == 0)
                return prefix ?? string.Empty;

      // Estimate capacity: prefix + (average item length * count)
    // Assuming average update statement is ~50 chars
            var estimatedCapacity = (prefix?.Length ?? 0) + (itemsList.Count * 50);
 var sb = new StringBuilder(estimatedCapacity);

   if (prefix != null)
            {
     sb.Append(prefix);
     }

       foreach (var item in itemsList)
       {
         sb.Append(selector(item));
   }

      return sb.ToString();
 }

        /// <summary>
      /// Builds a Gremlin update statement from update chain efficiently.
        /// </summary>
        public static string BuildUpdateStatement(string vertexSelector, IEnumerable<string> updateStatements)
 {
            var statementsList = updateStatements as IList<string> ?? updateStatements.ToList();
 if (statementsList.Count == 0)
   return vertexSelector;

    // Estimate capacity: selector + (average update * count)
     var estimatedCapacity = vertexSelector.Length + (statementsList.Count * 50);
    var sb = new StringBuilder(estimatedCapacity);
    sb.Append(vertexSelector);

            foreach (var statement in statementsList)
      {
    sb.Append(statement);
     }

      return sb.ToString();
        }

        /// <summary>
   /// Builds a Gremlin update statement from a dictionary of updates.
        /// </summary>
     public static string BuildUpdateStatement<TKey, TValue>(
   string vertexSelector, 
    IDictionary<TKey, TValue> updateChain, 
       System.Func<TValue, string> selector)
   {
     if (updateChain.Count == 0)
         return vertexSelector;

            var estimatedCapacity = vertexSelector.Length + (updateChain.Count * 50);
   var sb = new StringBuilder(estimatedCapacity);
    sb.Append(vertexSelector);

    foreach (var update in updateChain.Values)
   {
 sb.Append(selector(update));
            }

    return sb.ToString();
        }

   /// <summary>
        /// Joins strings with a separator efficiently using StringBuilder.
        /// </summary>
      public static string JoinStrings(IEnumerable<string> strings, string separator = "")
        {
 var stringsList = strings as IList<string> ?? strings.ToList();
       if (stringsList.Count == 0)
  return string.Empty;

            if (stringsList.Count == 1)
                return stringsList[0];

            var averageLength = stringsList[0]?.Length ?? 20;
   var estimatedCapacity = (averageLength + separator.Length) * stringsList.Count;
            var sb = new StringBuilder(estimatedCapacity);

        bool first = true;
            foreach (var str in stringsList)
     {
      if (!first && separator.Length > 0)
     {
    sb.Append(separator);
      }
      sb.Append(str);
       first = false;
       }

      return sb.ToString();
        }

        /// <summary>
        /// Creates a cache key string efficiently from Type and property name.
        /// Note: Consider using ValueTuple<Type, string> instead for better performance.
        /// </summary>
        public static string CreateCacheKey(System.Type type, string propertyName)
        {
     // This is for backward compatibility only
          // NEW CODE SHOULD USE: (type, propertyName) tuple
      var sb = new StringBuilder(type.FullName.Length + propertyName.Length + 1);
       sb.Append(type.FullName);
      sb.Append('.');
       sb.Append(propertyName);
       return sb.ToString();
  }
    }
}
