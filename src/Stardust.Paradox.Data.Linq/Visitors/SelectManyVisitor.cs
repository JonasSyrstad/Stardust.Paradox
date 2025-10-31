using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling SelectMany method calls
    /// </summary>
    public class SelectManyVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "SelectMany";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // SelectMany flattens collections - requires client-side evaluation
            // because Gremlin doesn't have a direct equivalent
  
            // Visit source
            VisitSource(node, context);

            // Extract lambda
            var lambda = (LambdaExpression)context.StripQuotes(node.Arguments[1]);
   
            // Mark for client-side projection
            context.RequiresClientSideProjection = true;
            context.ClientSideProjection = lambda;
      
            // For SelectMany, the element type is the element inside the collection, not the collection itself
            // The lambda returns IEnumerable<T>, so we need to extract T
            if (lambda.ReturnType.IsGenericType)
            {
                var genericArgs = lambda.ReturnType.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    context.ElementType = genericArgs[0]; // Extract T from IEnumerable<T>
                }
                else
                {
                    context.ElementType = lambda.ReturnType;
                }
            }
            else
            {
                context.ElementType = lambda.ReturnType;
            }
            
            // Extract required properties for server-side retrieval
            var requiredProperties = ProjectionExpressionEvaluator.ExtractRequiredProperties(lambda);
            var propertyQuery = ProjectionExpressionEvaluator.GetPropertyRetrievalQuery(requiredProperties);
            context.GremlinQuery.Append(propertyQuery);
      
            return node;
        }
    }
}