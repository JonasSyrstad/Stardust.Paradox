using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Where method calls
    /// </summary>
 public class WhereVisitor : ExpressionVisitorBase
    {
        /// <inheritdoc />
        public override string MethodName => "Where";

  /// <inheritdoc />
        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
        // Visit source
            VisitSource(node, context);

            // Extract lambda and build has() predicate
   var predicate = ExtractAndTranslatePredicate(node, context);
   
         if (!string.IsNullOrEmpty(predicate))
            {
  context.GremlinQuery.Append(predicate);
            }

       return node;
        }
    }
}
