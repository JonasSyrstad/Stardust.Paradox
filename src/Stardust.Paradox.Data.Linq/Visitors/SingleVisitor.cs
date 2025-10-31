using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Single and SingleOrDefault method calls
    /// </summary>
    public class SingleVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Single"; // Also handles SingleOrDefault through CanVisit override

        public override bool CanVisit(MethodCallExpression node, IVisitorContext context)
        {
            return base.CanVisit(node, context) || node.Method.Name == "SingleOrDefault";
        }

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.IsSingleQuery = true;
            context.UseSingleOrDefault = node.Method.Name == "SingleOrDefault";
      
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