using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Average method calls
    /// </summary>
    public class AverageVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Average";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.IsAverageQuery = true;
  
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