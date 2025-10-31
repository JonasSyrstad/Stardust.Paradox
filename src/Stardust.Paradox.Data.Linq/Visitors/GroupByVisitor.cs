using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling GroupBy method calls
    /// </summary>
    public class GroupByVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "GroupBy";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // If we're already in client-side projection mode, don't try to translate GroupBy to Gremlin
            // It will be handled by the ChainedOperationsHandler
            if (context.RequiresClientSideProjection)
            {
                // Don't visit children - stop translation here
                // The client-side handler will take care of GroupBy
                return node;
            }

            // Visit source
            VisitSource(node, context);

            // Check again after visiting source - if source triggered client-side projection,
            // don't translate this GroupBy
            if (context.RequiresClientSideProjection)
            {
                return node;
            }

            // Use GroupByTranslator for the GroupBy operation
            var groupByTranslator = new GroupByTranslator(context.GremlinQuery);
            groupByTranslator.TranslateGroupBy(node);

            context.IsGroupByQuery = true;
            return node;
        }
    }
}