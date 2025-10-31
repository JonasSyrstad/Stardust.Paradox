using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Skip method calls
    /// </summary>
    public class SkipVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Skip";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            VisitSource(node, context);

            // Extract count and validate
            var count = (int)((ConstantExpression)node.Arguments[1]).Value;
    
            if (count < 0)
            {
                throw new System.ArgumentException("Skip count cannot be negative.", nameof(count));
            }
            
            context.GremlinQuery.Append($".skip({count})");

            return node;
        }
    }
}