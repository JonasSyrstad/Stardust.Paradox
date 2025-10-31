using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Count and LongCount method calls
    /// </summary>
    public class CountVisitor : ExpressionVisitorBase
    {
     public override string MethodName => "Count"; // Also handles LongCount through CanVisit override

        public override bool CanVisit(MethodCallExpression node, IVisitorContext context)
{
return base.CanVisit(node, context) || node.Method.Name == "LongCount";
 }

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
      if (node.Method.Name == "LongCount")
         context.IsLongCountQuery = true;
   else
         context.IsCountQuery = true;

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
