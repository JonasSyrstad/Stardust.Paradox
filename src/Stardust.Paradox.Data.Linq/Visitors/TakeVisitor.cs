using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Take method calls
    /// </summary>
    public class TakeVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Take";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            VisitSource(node, context);

            // Extract count and validate
            var count = (int)((ConstantExpression)node.Arguments[1]).Value;
       
            if (count < 0)
            {
                throw new System.ArgumentException("Take count cannot be negative.", nameof(count));
            }
            
            context.GremlinQuery.Append($".limit({count})");

            return node;
        }
    }
}