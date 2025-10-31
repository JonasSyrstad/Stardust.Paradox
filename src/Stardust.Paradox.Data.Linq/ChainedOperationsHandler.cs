using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Handles chained LINQ operations that require client-side evaluation after an initial projection
    /// For example: Select().OrderBy(), Select().GroupBy().Select(), Select().SelectMany()
    /// </summary>
    internal class ChainedOperationsHandler
    {
        /// <summary>
        /// Checks if the expression contains operations that need client-side handling after projection
        /// </summary>
        public static bool RequiresChainedClientSideEvaluation(Expression expression)
        {
            // Walk the expression tree to find if there's a Select followed by other operations
            var visitor = new ChainedOperationsVisitor();
            visitor.Visit(expression);
            return visitor.HasChainedOperations;
        }

        /// <summary>
        /// Applies chained operations client-side to projected data
        /// </summary>
        public static object ApplyChainedOperations(object projectedData, Expression expression, Type resultType)
        {
            if (projectedData == null)
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(resultType));

            // Build a queryable from the projected data
            var enumerable = projectedData as IEnumerable;
            if (enumerable == null)
                return projectedData;

            // Extract the element type from projected data
            Type elementType = null;
            var enumerableType = projectedData.GetType();
            if (enumerableType.IsGenericType)
            {
                var genericArgs = enumerableType.GetGenericArguments();
                if (genericArgs.Length > 0)
                    elementType = genericArgs[0];
            }

            if (elementType == null)
                return projectedData;

            // Extract post-projection operations in execution order
            var extractor = new PostProjectionOperationsExtractor();
            extractor.Visit(expression);

            if (extractor.Operations.Count == 0)
                return projectedData;

            // Convert to queryable
            var asQueryableMethod = typeof(Queryable).GetMethods()
             .First(m => m.Name == "AsQueryable" && m.IsGenericMethod && m.GetParameters().Length == 1);
            var queryable = asQueryableMethod.MakeGenericMethod(elementType).Invoke(null, new[] { projectedData });

            // Apply each operation in order
            object currentResult = queryable;
            Type currentElementType = elementType;

            foreach (var operation in extractor.Operations)
            {
                currentResult = operation.Apply(currentResult, currentElementType);

                // Update element type if the operation changed it (e.g., GroupBy or Select)
                if (operation is GroupByOperation groupByOp)
                {
                    currentElementType = groupByOp.GetResultElementType(currentElementType);
                }
                else if (operation is SelectAfterGroupByOperation selectAfterGroupByOp)
                {
                    currentElementType = selectAfterGroupByOp.GetResultElementType();
                }
            }

            // Convert result to expected type
            if (resultType.IsGenericType)
            {
                var genericDef = resultType.GetGenericTypeDefinition();
                if (genericDef == typeof(List<>) || genericDef == typeof(IEnumerable<>))
                {
                    // Convert IQueryable to List
                    var toListMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == "ToList" && m.GetParameters().Length == 1)
                      .MakeGenericMethod(currentElementType);
                    return toListMethod.Invoke(null, new[] { currentResult });
                }
            }

            return currentResult;
        }

        private class ChainedOperationsVisitor : ExpressionVisitor
        {
            public bool HasChainedOperations { get; private set; }
            private bool _foundSelect;

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable))
                {
                    if (node.Method.Name == "Select" || node.Method.Name == "SelectMany")
                    {
                        _foundSelect = true;
                    }
                    else if (_foundSelect)
                    {
                        // Operations after Select that need client-side handling
                        if (node.Method.Name == "OrderBy" || node.Method.Name == "OrderByDescending" ||
                       node.Method.Name == "ThenBy" || node.Method.Name == "ThenByDescending" ||
                     node.Method.Name == "GroupBy")
                        {
                            HasChainedOperations = true;
                        }
                    }
                }

                return base.VisitMethodCall(node);
            }
        }

        private class PostProjectionOperationsExtractor : ExpressionVisitor
        {
            public List<IPostProjectionOperation> Operations { get; } = new List<IPostProjectionOperation>();
            private bool _foundSelect;
            private bool _inGroupByChain;

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                // Visit children first (bottom-up traversal)
                var result = base.VisitMethodCall(node);

                if (node.Method.DeclaringType != typeof(Queryable) && node.Method.DeclaringType != typeof(Enumerable))
                    return result;

                if (node.Method.Name == "Select" || node.Method.Name == "SelectMany")
                {
                    if (!_foundSelect)
                    {
                        // This is the first Select - the projection point
                        _foundSelect = true;
                    }
                    else if (_inGroupByChain)
                    {
                        // This is a Select after GroupBy
                        var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
                        Operations.Add(new SelectAfterGroupByOperation(lambda));
                        _inGroupByChain = false;
                    }
                }
                else if (_foundSelect)
                {
                    // Capture operations after the first Select
                    switch (node.Method.Name)
                    {
                        case "OrderBy":
                        case "OrderByDescending":
                        case "ThenBy":
                        case "ThenByDescending":
                            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
                            bool descending = node.Method.Name.Contains("Descending");
                            bool isThenBy = node.Method.Name.StartsWith("ThenBy");
                            Operations.Add(new OrderByOperation(lambda, descending, isThenBy));
                            break;

                        case "GroupBy":
                            var groupLambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
                            Operations.Add(new GroupByOperation(groupLambda));
                            _inGroupByChain = true;
                            break;
                    }
                }

                return result;
            }

            private static Expression StripQuotes(Expression expression)
            {
                while (expression.NodeType == ExpressionType.Quote)
                {
                    expression = ((UnaryExpression)expression).Operand;
                }
                return expression;
            }
        }

        private interface IPostProjectionOperation
        {
            object Apply(object queryable, Type elementType);
        }

        private class OrderByOperation : IPostProjectionOperation
        {
            private readonly LambdaExpression _keySelector;
            private readonly bool _descending;
            private readonly bool _isThenBy;

            public OrderByOperation(LambdaExpression keySelector, bool descending, bool isThenBy)
            {
                _keySelector = keySelector;
                _descending = descending;
                _isThenBy = isThenBy;
            }

            public object Apply(object queryable, Type elementType)
            {
                var methodName = _isThenBy
       ? (_descending ? "ThenByDescending" : "ThenBy")
         : (_descending ? "OrderByDescending" : "OrderBy");
                var keyType = _keySelector.ReturnType;

                var orderByMethod = typeof(Queryable).GetMethods()
             .First(m => m.Name == methodName && m.GetParameters().Length == 2)
              .MakeGenericMethod(elementType, keyType);

                return orderByMethod.Invoke(null, new[] { queryable, _keySelector });
            }
        }

        private class GroupByOperation : IPostProjectionOperation
        {
            private readonly LambdaExpression _keySelector;

            public GroupByOperation(LambdaExpression keySelector)
            {
                _keySelector = keySelector;
            }

            public object Apply(object queryable, Type elementType)
            {
                var keyType = _keySelector.ReturnType;

                var groupByMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == "GroupBy" && m.GetParameters().Length == 2)
             .MakeGenericMethod(elementType, keyType);

                return groupByMethod.Invoke(null, new[] { queryable, _keySelector });
            }

            public Type GetResultElementType(Type sourceElementType)
            {
                var keyType = _keySelector.ReturnType;
                return typeof(IGrouping<,>).MakeGenericType(keyType, sourceElementType);
            }
        }

        private class SelectAfterGroupByOperation : IPostProjectionOperation
        {
            private readonly LambdaExpression _selector;

            public SelectAfterGroupByOperation(LambdaExpression selector)
            {
                _selector = selector;
            }

            public object Apply(object queryable, Type elementType)
            {
                var resultType = _selector.ReturnType;

                var selectMethod = typeof(Queryable).GetMethods()
          .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
        .MakeGenericMethod(elementType, resultType);

                return selectMethod.Invoke(null, new[] { queryable, _selector });
            }

            public Type GetResultElementType()
            {
                return _selector.ReturnType;
            }
        }
    }
}
