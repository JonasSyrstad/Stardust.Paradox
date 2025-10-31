using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling First and FirstOrDefault method calls
    /// </summary>
    public class FirstVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "First"; // Also handles FirstOrDefault through CanVisit override

        public override bool CanVisit(MethodCallExpression node, IVisitorContext context)
        {
            return base.CanVisit(node, context) || node.Method.Name == "FirstOrDefault";
        }

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.IsFirstQuery = true;
            context.UseFirstOrDefault = node.Method.Name == "FirstOrDefault";
         
            // Visit source
            VisitSource(node, context);

            // If there's a predicate, add it
            var predicate = ExtractAndTranslatePredicate(node, context);
            if (!string.IsNullOrEmpty(predicate))
            {
                context.GremlinQuery.Append(predicate);
            }

            return node;
        }
    }
}