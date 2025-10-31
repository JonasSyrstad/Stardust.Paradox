using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Min method calls
    /// </summary>
    public class MinVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Min";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.IsMinQuery = true;

            // Visit source
            VisitSource(node, context);

            // If there's a lambda selector, extract property
            var propertyName = GetPropertyNameFromSelector(node, context);
            if (!string.IsNullOrEmpty(propertyName))
            {
                context.GremlinQuery.Append($".values('{context.ToCamelCase(propertyName)}')");
            }

            return node;
        }
    }
}