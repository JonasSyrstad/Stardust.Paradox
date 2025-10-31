using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// IQueryable implementation for graph entities
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
internal class GraphQueryable<T> : IOrderedQueryable<T>
 {
     public GraphQueryable(GremlinQueryProvider provider)
        {
      Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Expression = Expression.Constant(this);
    }

        public GraphQueryable(GremlinQueryProvider provider, Expression expression)
        {
     Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
 }

    public Type ElementType => typeof(T);

        public Expression Expression { get; }

        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
   {
       var result = Provider.Execute<IEnumerable<T>>(Expression);
            return result.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
          return GetEnumerator();
        }

        /// <summary>
        /// Translates the LINQ expression to a Gremlin query string for testing/debugging purposes
   /// </summary>
        /// <returns>The generated Gremlin query string</returns>
        public string ToGremlinQuery()
        {
        if (Provider is GremlinQueryProvider gremlinProvider)
            {
       var label = GetVertexLabel();
        var translator = new GremlinQueryTranslator(label);
                return translator.Translate(Expression);
 }

        throw new InvalidOperationException("Provider is not a GremlinQueryProvider");
      }

        private string GetVertexLabel()
{
        // Try to get label from VertexLabel attribute
     var labelAttr = typeof(T).GetCustomAttributes(typeof(VertexLabelAttribute), false)
           .FirstOrDefault() as VertexLabelAttribute;

            if (labelAttr != null)
           return labelAttr.Label;

       // Fallback to type name in camelCase
            var typeName = typeof(T).Name;

            // Remove 'I' prefix if present (interface naming convention)
       if (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]))
  {
      typeName = typeName.Substring(1);
    }

    // Convert to camelCase
    return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        }

        /// <summary>
        /// Override ToString to provide Gremlin query for debugging
        /// </summary>
        /// <returns>The generated Gremlin query string, or the type name if translation fails</returns>
        public override string ToString()
      {
        try
     {
         return ToGremlinQuery();
    }
            catch (Exception)
     {
     // If translation fails, return base implementation
             return base.ToString();
            }
        }
  }
}
