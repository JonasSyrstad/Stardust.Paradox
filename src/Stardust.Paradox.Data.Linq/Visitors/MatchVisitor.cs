using System;
using System.Linq;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Match() pattern matching step
    /// </summary>
    public class MatchVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Match";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Extract patterns from the NewArrayInit expression
            var arrayInit = (NewArrayExpression)node.Arguments[1];
            var patterns = arrayInit.Expressions
                .Cast<UnaryExpression>()
                .Select(u => (LambdaExpression)u.Operand)
                .ToArray();

            if (patterns.Length == 0)
                throw new ArgumentException("At least one pattern is required for match()");

            // Generate Gremlin for each pattern
            var patternStrings = new System.Collections.Generic.List<string>();
            foreach (var pattern in patterns)
            {
                var patternSteps = ExtractNestedTraversalSteps(pattern.Body, context);
                patternStrings.Add(patternSteps);
            }

            context.GremlinQuery.Append($".match({string.Join(", ", patternStrings)})");

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
       
            // Remove ALL leading dots to get clean steps
            while (generatedSteps.Length > 0 && generatedSteps[0] == '.')
            {
                generatedSteps = generatedSteps.Substring(1);
            }
    
            return generatedSteps;
        }
    }
}