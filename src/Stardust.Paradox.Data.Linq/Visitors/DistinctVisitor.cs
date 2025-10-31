using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Distinct method calls
    /// </summary>
    public class DistinctVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Distinct";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.IsDistinctQuery = true;
    
            // Visit source
            VisitSource(node, context);

            return node;
        }
    }
}