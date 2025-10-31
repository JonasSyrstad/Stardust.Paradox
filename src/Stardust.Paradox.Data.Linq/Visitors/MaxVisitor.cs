using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Max method calls
    /// </summary>
    public class MaxVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Max";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.IsMaxQuery = true;
       
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