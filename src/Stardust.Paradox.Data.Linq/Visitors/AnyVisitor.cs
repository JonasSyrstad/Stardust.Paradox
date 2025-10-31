using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Any method calls
    /// </summary>
    public class AnyVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Any";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
      {
        context.IsAnyQuery = true;
            
// Visit source
 VisitSource(node, context);

            // If there's a predicate, add it
       var predicate = ExtractAndTranslatePredicate(node, context);
            if (!string.IsNullOrEmpty(predicate))
       {
        context.GremlinQuery.Append(predicate);
      }

            return node;
        }
    }
}
