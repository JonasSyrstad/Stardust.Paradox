using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for As() label assignment step
    /// </summary>
    public class AsVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "As";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Get the label from the constant string argument
            var label = (string)((ConstantExpression)node.Arguments[1]).Value;
          
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty");
          
            context.GremlinQuery.Append($".as('{label}')");

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
    }
}