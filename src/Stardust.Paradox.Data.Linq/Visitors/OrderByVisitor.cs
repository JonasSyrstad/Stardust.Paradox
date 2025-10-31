using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling OrderBy and OrderByDescending method calls
    /// </summary>
    public class OrderByVisitor : ExpressionVisitorBase
    {
 public override string MethodName => "OrderBy"; // Also handles OrderByDescending through CanVisit override

        public override bool CanVisit(MethodCallExpression node, IVisitorContext context)
        {
      return base.CanVisit(node, context) || node.Method.Name == "OrderByDescending";
        }

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
  // If we're already in client-side projection mode, don't translate OrderBy to Gremlin
         // It will be handled by the ChainedOperationsHandler
       if (context.RequiresClientSideProjection)
      {
     // Just visit the source and return - the OrderBy will be applied client-side
      VisitSource(node, context);
        return node;
       }

            // Visit source
          VisitSource(node, context);

     // Extract lambda
        var propertyName = GetPropertyNameFromSelector(node, context);
       
   var direction = node.Method.Name == "OrderByDescending" ? "decr" : "incr";
  context.GremlinQuery.Append($".order().by('{context.ToCamelCase(propertyName)}', {direction})");

   return node;
      }
    }
}
