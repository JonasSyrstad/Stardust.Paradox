using System;
using System.Linq.Expressions;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Extension methods for GraphTraversal
    /// </summary>
    public static class GraphTraversalLambdaExtensions
    {
        public static string ToGremlinQuery<T>(this GraphTraversal<T> traversal) where T : IGraphEntity
        {
            return traversal.ToGremlinQuery();
        }

        // Lambda-based traversal methods
        public static GraphTraversal<T> OutE<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var propertyName = ExtractPropertyName(edgeProperty);
            newTraversal.AddStep($"outE('{propertyName}')");
            return newTraversal;
        }

        public static GraphTraversal<T> InE<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var propertyName = ExtractPropertyName(edgeProperty);
            newTraversal.AddStep($"inE('{propertyName}')");
            return newTraversal;
        }

        public static GraphTraversal<T> Out<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var propertyName = ExtractPropertyName(edgeProperty);
            newTraversal.AddStep($"out('{propertyName}')");
            return newTraversal;
        }

        public static GraphTraversal<T> In<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var propertyName = ExtractPropertyName(edgeProperty);
            newTraversal.AddStep($"in('{propertyName}')");
            return newTraversal;
        }

        private static string ExtractPropertyName<T>(Expression<Func<T, object>> expression)
        {
            if (expression.Body is MemberExpression memberExpr)
            {
                return ToCamelCase(memberExpr.Member.Name);
            }
            else if (expression.Body is UnaryExpression unaryExpr && unaryExpr.Operand is MemberExpression operandMemberExpr)
            {
                return ToCamelCase(operandMemberExpr.Member.Name);
            }

            throw new ArgumentException("Expression must be a property accessor", nameof(expression));
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length == 0)
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}