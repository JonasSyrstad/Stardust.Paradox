using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling ThenBy and ThenByDescending method calls
    /// </summary>
    public class ThenByVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "ThenBy"; // Also handles ThenByDescending through CanVisit override

        public override bool CanVisit(MethodCallExpression node, IVisitorContext context)
        {
            return base.CanVisit(node, context) || node.Method.Name == "ThenByDescending";
        }

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // If we're already in client-side projection mode, don't translate ThenBy to Gremlin
            // It will be handled by the ChainedOperationsHandler
            if (context.RequiresClientSideProjection)
            {
                // Just visit the source and return - the ThenBy will be applied client-side
                VisitSource(node, context);
                return node;
            }

            // Visit source
            VisitSource(node, context);

            // Extract lambda
            var propertyName = GetPropertyNameFromSelector(node, context);
          
            var direction = node.Method.Name == "ThenByDescending" ? "decr" : "incr";
            context.GremlinQuery.Append($".by('{context.ToCamelCase(propertyName)}', {direction})");

            return node;
        }
    }
}