using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Repeat() graph traversal step
    /// </summary>
    public class RepeatVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Repeat";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Extract the repeat traversal lambda
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
        
            // Extract just the traversal steps from the lambda body
            var nestedSteps = ExtractNestedTraversalSteps(lambda.Body, context);
       
            context.GremlinQuery.Append($".repeat({nestedSteps})");

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

        /// <summary>
        /// Extracts Gremlin steps from a nested traversal lambda expression.
        /// The lambda body represents a chain of method calls starting from the parameter.
        /// We need to extract just the Gremlin steps without the parameter itself.
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
  
            // The generated steps will have leading dots like ".out('friendsWith').has('isActive', eq(true))"
            // Remove ALL leading dots to get "out('friendsWith').has('isActive', eq(true))"
            while (generatedSteps.Length > 0 && generatedSteps[0] == '.')
            {
                generatedSteps = generatedSteps.Substring(1);
            }
      
            return generatedSteps;
        }
    }
}