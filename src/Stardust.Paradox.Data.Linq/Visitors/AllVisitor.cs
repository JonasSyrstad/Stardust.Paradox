using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling All method calls
    /// </summary>
    public class AllVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "All";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.IsAllQuery = true;
      
            // Visit source
            VisitSource(node, context);

// For All, we need special handling:
            // LINQ All(predicate) returns true if ALL elements satisfy the predicate
            // In Gremlin, we need to check if count(filtered) == count(total)
         
            // Add predicate if present - this filters to matching items
            var predicate = ExtractAndTranslatePredicate(node, context);
            if (!string.IsNullOrEmpty(predicate))
            {
                context.GremlinQuery.Append(predicate);
            }
       
            // Add count to count the filtered results
            // The provider will compare this to the total count
            context.GremlinQuery.Append(".count()");

            return node;
        }
    }
}