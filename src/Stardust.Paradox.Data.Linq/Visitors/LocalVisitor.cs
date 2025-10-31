using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Local() graph traversal step
    /// </summary>
    public class LocalVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Local";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Extract the local traversal lambda
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
      
            // Extract just the traversal steps from the lambda body
            var nestedSteps = ExtractNestedTraversalSteps(lambda.Body, context);
    
            context.GremlinQuery.Append($".local({nestedSteps})");

            // Update element type to TResult
            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    context.ElementType = genericArgs[1]; // TResult
                }
            }

            return node;
        }

        /// <summary>
        /// Extracts Gremlin steps from a nested traversal lambda expression.
        /// </summary>
        private string ExtractNestedTraversalSteps(Expression expression, IVisitorContext context)
        {
            // Save current query position
            var savedLength = context.GremlinQuery.Length;

            // Visit the expression to generate Gremlin
            context.VisitExpression(expression);
          
            // Extract the generated Gremlin steps
            var generatedSteps = context.GremlinQuery.ToString().Substring(savedLength);
      
            // Remove the generated steps from the main query
            context.GremlinQuery.Length = savedLength;
            
            // Remove only the FIRST leading dot to get clean steps
            // Keep dots for chained methods like .match(...), .local(...), etc.
            if (generatedSteps.Length > 0 && generatedSteps[0] == '.')
            {
          generatedSteps = generatedSteps.Substring(1);
            }
     
            return generatedSteps;
        }
    }
}