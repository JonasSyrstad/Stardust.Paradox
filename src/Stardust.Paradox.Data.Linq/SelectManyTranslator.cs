using System;
using System.Linq.Expressions;
using System.Text;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Handles translation of SelectMany operations to Gremlin queries
    /// SelectMany flattens nested collections by projecting each element to a collection
    /// and then flattening the results into a single sequence
    /// </summary>
    internal class SelectManyTranslator
    {
      private readonly StringBuilder _gremlinQuery;

 public SelectManyTranslator(StringBuilder gremlinQuery)
 {
            _gremlinQuery = gremlinQuery ?? throw new ArgumentNullException(nameof(gremlinQuery));
        }

        /// <summary>
     /// Translates SelectMany operation
        /// For most cases, SelectMany requires client-side evaluation since it involves
        /// creating collections and flattening them
   /// </summary>
        public bool RequiresClientSideEvaluation(MethodCallExpression node)
    {
      // SelectMany almost always requires client-side evaluation
       // because it involves:
      // 1. Creating collections for each element
         // 2. Flattening those collections
            // 3. Often involves complex projections
     
     // Check if this is a simple property access that returns a collection
    var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
     
            // If the selector is just accessing a collection property (like p => p.Tags),
          // we might be able to handle it server-side with unfold()
        // Otherwise, it requires client-side evaluation
            
        if (lambda.Body is MemberExpression memberExpr)
            {
     // Simple collection property access - might be handleable server-side
         // But for now, we'll say it requires client-side evaluation
          // since we'd need to ensure the property is actually a collection
  return true;
  }
      
   // Complex expressions definitely need client-side evaluation
            return true;
        }

   /// <summary>
     /// For SelectMany, we typically need to retrieve all properties and do client-side flattening
        /// </summary>
        public void AppendPropertyRetrieval()
        {
   // Use elementMap() to get all properties for client-side processing
            _gremlinQuery.Append(".elementMap()");
  }

        private static Expression StripQuotes(Expression expression)
  {
            while (expression.NodeType == ExpressionType.Quote)
        {
       expression = ((UnaryExpression)expression).Operand;
      }
            return expression;
        }
    }
}
