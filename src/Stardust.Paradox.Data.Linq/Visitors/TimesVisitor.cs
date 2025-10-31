using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Times() graph traversal step
    /// </summary>
    public class TimesVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Times";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
// Visit source
            context.VisitExpression(node.Arguments[0]);

            // Get the iteration count
            var iterations = (int)((ConstantExpression)node.Arguments[1]).Value;
            
            context.GremlinQuery.Append($".times({iterations})");

            // Element type stays the same
            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length >= 1)
                {
                    context.ElementType = genericArgs[0];
                }
            }

            return node;
        }
    }
}