using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Base class for graph traversal visitors
    /// </summary>
    public abstract class GraphTraversalVisitorBase : IExpressionVisitor
    {
        private static readonly ConcurrentDictionary<string, string> _camelCaseCache =
            new ConcurrentDictionary<string, string>(
                concurrencyLevel: Environment.ProcessorCount,
                capacity: 128);

        public abstract string MethodName { get; }

        public virtual int Priority => 100;

        public virtual bool CanVisit(MethodCallExpression node, IVisitorContext context)
        {
            // Check if it's from GraphTraversalExtensions or GraphSetLinqExtensions
            return (node.Method.DeclaringType == typeof(GraphTraversalExtensions) ||
                   node.Method.DeclaringType == typeof(GraphSetLinqExtensions)) &&
                   node.Method.Name == MethodName;
        }

        public abstract Expression Visit(MethodCallExpression node, IVisitorContext context);

        protected string GetEdgeLabelFromExpression(Expression expression)
        {
            // Extract property name from p => p.Companies
            if (expression is MemberExpression memberExpr)
            {
                var propertyInfo = memberExpr.Member as PropertyInfo;
                if (propertyInfo != null)
                {
                    // Get the entity type from the member expression
                    // For "p => p.Skills", memberExpr.Expression is the parameter "p"
                    // We need to get the actual type of the parameter
                    Type entityType = null;

                    if (memberExpr.Expression is ParameterExpression paramExpr)
                    {
                        // Get the parameter's type (e.g., IPerson)
                        entityType = paramExpr.Type;
                    }
                    else if (memberExpr.Expression != null)
                    {
                        // Fallback to the expression's type
                        entityType = memberExpr.Expression.Type;
                    }

                    if (entityType != null)
                    {
                        // Try to get edge label from fluent configuration or attributes
                        var edgeLabel = EdgeLabelResolver.GetAnyEdgeLabel(entityType, propertyInfo);
                        if (!string.IsNullOrEmpty(edgeLabel))
                            return edgeLabel;
                    }
                }

                // Fallback: Convert property name to camelCase for edge label
                var fallback = ToCamelCase(memberExpr.Member.Name);
                return fallback;
            }

            return null;
        }

        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return _camelCaseCache.GetOrAdd(name, n =>
                char.ToLowerInvariant(n[0]) + n.Substring(1));
        }

        protected Expression StripQuotes(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Quote)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression;
        }
    }
}
