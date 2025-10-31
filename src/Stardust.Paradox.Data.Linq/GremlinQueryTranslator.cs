using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Linq.Visitors;
using Stardust.Paradox.Data.Linq.Infrastructure;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Translates LINQ expression trees to Gremlin query strings using a plugin-based visitor architecture
    /// with query plan caching for improved performance
    /// </summary>
    internal class GremlinQueryTranslator : ExpressionVisitor, IVisitorContext
    {
        // Query translation cache using expression hash as key
        private static readonly ConcurrentDictionary<int, CachedQueryPlan> _queryCache =
 new ConcurrentDictionary<int, CachedQueryPlan>(
    concurrencyLevel: Environment.ProcessorCount * 2,
       capacity: 256);

        private readonly string _label;
        private readonly StringBuilder _gremlinQuery;
        private readonly Stack<string> _contextStack;
        private Type _elementType;
        private readonly Dictionary<string, object> _parameters;
        private bool _isCountQuery;
        private bool _isLongCountQuery;
        private bool _isAnyQuery;
        private bool _isFirstQuery;
        private bool _isSingleQuery;
        private bool _useFirstOrDefault;
        private bool _useSingleOrDefault;
        private bool _isDistinctQuery;
        private bool _isAllQuery;
        private bool _isSumQuery;
        private bool _isAverageQuery;
        private bool _isMinQuery;
        private bool _isMaxQuery;
        private bool _isGroupByQuery;
        private bool _requiresClientSideProjection;
        private LambdaExpression _clientSideProjection;

        // IVisitorContext implementation - expose private fields as properties
        string IVisitorContext.Label => _label;
        StringBuilder IVisitorContext.GremlinQuery => _gremlinQuery;

        Type IVisitorContext.ElementType
        {
            get => _elementType;
            set => _elementType = value;
        }

        Dictionary<string, object> IVisitorContext.Parameters => _parameters;

        bool IVisitorContext.IsCountQuery
        {
            get => _isCountQuery;
            set => _isCountQuery = value;
        }

        bool IVisitorContext.IsLongCountQuery
        {
            get => _isLongCountQuery;
            set => _isLongCountQuery = value;
        }

        bool IVisitorContext.IsAnyQuery
        {
            get => _isAnyQuery;
            set => _isAnyQuery = value;
        }

        bool IVisitorContext.IsFirstQuery
        {
            get => _isFirstQuery;
            set => _isFirstQuery = value;
        }

        bool IVisitorContext.UseFirstOrDefault
        {
            get => _useFirstOrDefault;
            set => _useFirstOrDefault = value;
        }

        bool IVisitorContext.IsSingleQuery
        {
            get => _isSingleQuery;
            set => _isSingleQuery = value;
        }

        bool IVisitorContext.UseSingleOrDefault
        {
            get => _useSingleOrDefault;
            set => _useSingleOrDefault = value;
        }

        bool IVisitorContext.IsDistinctQuery
        {
            get => _isDistinctQuery;
            set => _isDistinctQuery = value;
        }

        bool IVisitorContext.IsAllQuery
        {
            get => _isAllQuery;
            set => _isAllQuery = value;
        }

        bool IVisitorContext.IsSumQuery
        {
            get => _isSumQuery;
            set => _isSumQuery = value;
        }

        bool IVisitorContext.IsAverageQuery
        {
            get => _isAverageQuery;
            set => _isAverageQuery = value;
        }

        bool IVisitorContext.IsMinQuery
        {
            get => _isMinQuery;
            set => _isMinQuery = value;
        }

        bool IVisitorContext.IsMaxQuery
        {
            get => _isMaxQuery;
            set => _isMaxQuery = value;
        }

        bool IVisitorContext.IsGroupByQuery
        {
            get => _isGroupByQuery;
            set => _isGroupByQuery = value;
        }

        bool IVisitorContext.RequiresClientSideProjection
        {
            get => _requiresClientSideProjection;
            set => _requiresClientSideProjection = value;
        }

        LambdaExpression IVisitorContext.ClientSideProjection
        {
            get => _clientSideProjection;
            set => _clientSideProjection = value;
        }

        // Public properties for external access
        public Type ElementType => _elementType;
        public bool IsGroupByQuery => _isGroupByQuery;
        public bool IsSingleQuery => _isSingleQuery;
        public bool UseSingleOrDefault => _useSingleOrDefault;
        public bool IsFirstQuery => _isFirstQuery;
        public bool UseFirstOrDefault => _useFirstOrDefault;
        public bool IsAllQuery => _isAllQuery;
        public bool RequiresClientSideProjection => _requiresClientSideProjection;
        public LambdaExpression ClientSideProjection => _clientSideProjection;
        public Dictionary<string, object> Parameters => _parameters;

        public GremlinQueryTranslator(string label)
        {
            _label = label;
            _gremlinQuery = new StringBuilder();
            _contextStack = new Stack<string>();
            _parameters = new Dictionary<string, object>();
        }

        public string Translate(Expression expression)
        {
            _gremlinQuery.Clear();
            _gremlinQuery.Append($"g.V().hasLabel('{_label}')");

            Visit(expression);

            // Add terminal steps
            if (_isCountQuery || _isLongCountQuery)
            {
                _gremlinQuery.Append(".count()");
            }
            else if (_isAnyQuery)
            {
                _gremlinQuery.Append(".limit(1).count()");
            }
            else if (_isFirstQuery)
            {
                _gremlinQuery.Append(".limit(1)");
            }
            else if (_isSingleQuery)
            {
                _gremlinQuery.Append(".limit(2)"); // Get 2 to verify single
            }
            else if (_isDistinctQuery)
            {
                _gremlinQuery.Append(".dedup()");
            }
            else if (_isAllQuery)
            {
                // All query already has .count() added in VisitAll
                // No additional terminal step needed
            }
            else if (_isSumQuery)
            {
                _gremlinQuery.Append(".sum()");
            }
            else if (_isAverageQuery)
            {
                _gremlinQuery.Append(".mean()");
            }
            else if (_isMinQuery)
            {
                _gremlinQuery.Append(".min()");
            }
            else if (_isMaxQuery)
            {
                _gremlinQuery.Append(".max()");
            }

            return _gremlinQuery.ToString();
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Try to find a visitor for this method call
            var visitor = VisitorRegistry.Instance.FindVisitor(node, this);

            if (visitor != null)
            {
                return visitor.Visit(node, this);
            }

            // If no visitor found, fall back to base behavior
            return base.VisitMethodCall(node);
        }

        // IVisitorContext helper methods
        string IVisitorContext.ToCamelCase(string name) => ToCamelCase(name);
        string IVisitorContext.TranslatePredicate(Expression expression, string parameterName) => TranslatePredicate(expression, parameterName);
        string IVisitorContext.GetPropertyName(Expression expression) => GetPropertyName(expression);
        object IVisitorContext.GetValue(Expression expression) => GetValue(expression);
        string IVisitorContext.FormatValueWithParameterization(object value) => FormatValueWithParameterization(value);
        Expression IVisitorContext.StripQuotes(Expression expression) => StripQuotes(expression);
        Expression IVisitorContext.VisitExpression(Expression expression) => Visit(expression);

        // Edge label resolution methods
        string IVisitorContext.GetInEdgeLabel(Type entityType, MemberInfo member) => EdgeLabelResolver.GetInEdgeLabel(entityType, member);
        string IVisitorContext.GetOutEdgeLabel(Type entityType, MemberInfo member) => EdgeLabelResolver.GetOutEdgeLabel(entityType, member);
        string IVisitorContext.GetAnyEdgeLabel(Type entityType, MemberInfo member) => EdgeLabelResolver.GetAnyEdgeLabel(entityType, member);

        // Keep existing helper methods (used by context and legacy code)
        internal string TranslatePredicate(Expression expression, string parameterName)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    // Handle constant boolean values (e.g., Where(p => true) or Where(p => false))
                    if (expression is ConstantExpression constantExpr && constantExpr.Type == typeof(bool))
                    {
                        var boolValue = (bool)constantExpr.Value;
                        if (!boolValue)
                        {
                            // false: Filter out everything with a condition that never matches
                            // Use an impossible condition
                            return ".has('__impossible__', '__never__')";
                        }
                        // true: Don't add any filter (matches everything)
                        return string.Empty;
                    }
                    return string.Empty;
                case ExpressionType.Equal:
                    return TranslateEquality((BinaryExpression)expression, "eq");
                case ExpressionType.NotEqual:
                    return TranslateEquality((BinaryExpression)expression, "neq");
                case ExpressionType.GreaterThan:
                    return TranslateComparison((BinaryExpression)expression, "gt");
                case ExpressionType.GreaterThanOrEqual:
                    return TranslateComparison((BinaryExpression)expression, "gte");
                case ExpressionType.LessThan:
                    return TranslateComparison((BinaryExpression)expression, "lt");
                case ExpressionType.LessThanOrEqual:
                    return TranslateComparison((BinaryExpression)expression, "lte");
                case ExpressionType.AndAlso:
                    return TranslateBinaryLogical((BinaryExpression)expression, parameterName);
                case ExpressionType.OrElse:
                    return TranslateBinaryLogical((BinaryExpression)expression, parameterName);
                case ExpressionType.Not:
                    return TranslateNot((UnaryExpression)expression, parameterName);
                case ExpressionType.Call:
                    return TranslateMethodCall((MethodCallExpression)expression);
                case ExpressionType.MemberAccess:
                    // Handle direct boolean member access (e.g., p => p.IsActive)
                    if (expression is MemberExpression memberExpr && memberExpr.Type == typeof(bool))
                    {
                        var propertyName = memberExpr.Member.Name;
                        return $".has('{ToCamelCase(propertyName)}', eq(true))";
                    }
                    return string.Empty;
                default:
                    return string.Empty;
            }
        }

        private string TranslateEquality(BinaryExpression binary, string op)
        {
            var propertyName = GetPropertyName(binary.Left);
            var value = GetValue(binary.Right);

            if (propertyName != null)
            {
                // Handle null comparisons specially
                if (value == null)
                {
                    // p.Name == null should check if property does not exist
                    // p.Name != null should check if property does exist
                    if (op == "eq")
                    {
                        // For equality with null, check if property does not exist
                        return $".hasNot('{ToCamelCase(propertyName)}')";
                    }
                    else if (op == "neq")
                    {
                        // For inequality with null, filter to elements that have this property
                        return $".has('{ToCamelCase(propertyName)}')";
                    }
                }
                else
                {
                    // Normal value comparison - use parameterization
                    return $".has('{ToCamelCase(propertyName)}', {op}({FormatValueWithParameterization(value)}))";
                }
            }

            return string.Empty;
        }

        private string TranslateComparison(BinaryExpression binary, string op)
        {
            var propertyName = GetPropertyName(binary.Left);
            var value = GetValue(binary.Right);

            if (propertyName != null && value != null)
            {
                return $".has('{ToCamelCase(propertyName)}', {op}({FormatValueWithParameterization(value)}))";
            }

            return string.Empty;
        }

        private string FormatValueWithParameterization(object value)
        {
            if (value == null)
                return "null";

            // Create a parameter name
            var paramName = $"__p{_parameters.Count}";
            _parameters.Add(paramName, value);
            return paramName;
        }

        private string TranslateBinaryLogical(BinaryExpression binary, string parameterName)
        {
            // For AndAlso, just concatenate the predicates
            if (binary.NodeType == ExpressionType.AndAlso)
            {
                var left = TranslatePredicate(binary.Left, parameterName);
                var right = TranslatePredicate(binary.Right, parameterName);
                return left + right;
            }
            // For OrElse, collect all OR conditions and flatten them
            else if (binary.NodeType == ExpressionType.OrElse)
            {
                // Collect all OR conditions into a flat list
                var conditions = new List<string>();
                CollectOrConditions(binary, conditions);

                if (conditions.Count > 0)
                {
                    // Create a single flat or() statement with all conditions (NOT wrapped in .where())
                    return $".or({string.Join(", ", conditions)})";
                }
                // Fallback: shouldn't reach here
                return string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Recursively collects all OR conditions from a binary expression tree into a flat list
        /// </summary>
        private void CollectOrConditions(Expression expression, List<string> conditions)
        {
            if (expression is BinaryExpression binaryExpr && binaryExpr.NodeType == ExpressionType.OrElse)
            {
                // Recursively process left side
                CollectOrConditions(binaryExpr.Left, conditions);
                // Recursively process right side
                CollectOrConditions(binaryExpr.Right, conditions);
            }
            else
            {
                // This is a leaf condition (not an OR expression)
                // Translate the predicate directly and extract just the condition part
                var predicate = TranslatePredicate(expression, null);
                if (!string.IsNullOrEmpty(predicate))
                {
                    // Remove leading dot if present
                    if (predicate.StartsWith("."))
                    {
                        predicate = predicate.Substring(1);
                    }
                    conditions.Add(predicate);
                }
            }
        }

        private string ExtractConditionForOr(Expression expression)
        {
            // DEPRECATED: This method is no longer used as we now handle condition extraction in CollectOrConditions
            // For non-OR expressions, translate the predicate normally
            var predicate = TranslatePredicate(expression, null);

            if (string.IsNullOrEmpty(predicate))
                return string.Empty;

            // Remove leading dot and any .where() wrapper
            if (predicate.StartsWith(".where(or(") && predicate.EndsWith("))"))
            {
                // This shouldn't happen with the new CollectOrConditions, but handle it anyway
                predicate = predicate.Substring(".where(or(".Length, predicate.Length - ".where(or(".Length - 2);
            }
            else if (predicate.StartsWith("."))
            {
                // Remove the leading dot
                predicate = predicate.Substring(1);
            }

            return predicate;
        }

        private string TranslateNot(UnaryExpression unary, string parameterName)
        {
            // Handle simple negation of boolean properties
            if (unary.Operand is MemberExpression memberExpr)
            {
                var propertyName = memberExpr.Member.Name;
                return $".has('{ToCamelCase(propertyName)}', eq(false))";
            }

            var inner = TranslatePredicate(unary.Operand, parameterName);
            // Would need .not() step wrapping - simplified for now
            return inner;
        }

        private string TranslateMethodCall(MethodCallExpression call)
        {
            if (call.Method.DeclaringType == typeof(string))
            {
                switch (call.Method.Name)
                {
                    case "Contains":
                        {
                            var propertyName = GetPropertyName(call.Object);
                            var value = GetValue(call.Arguments[0]);
                            return $".has('{ToCamelCase(propertyName)}', containing({FormatValueWithParameterization(value)}))";
                        }
                    case "StartsWith":
                        {
                            var propertyName = GetPropertyName(call.Object);
                            var value = GetValue(call.Arguments[0]);
                            return $".has('{ToCamelCase(propertyName)}', startingWith({FormatValueWithParameterization(value)}))";
                        }
                    case "EndsWith":
                        {
                            var propertyName = GetPropertyName(call.Object);
                            var value = GetValue(call.Arguments[0]);
                            return $".has('{ToCamelCase(propertyName)}', endingWith({FormatValueWithParameterization(value)}))";
                        }
                }
            }

            return string.Empty;
        }

        internal string GetPropertyName(Expression expression)
        {
            if (expression is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            else if (expression is UnaryExpression unaryExpr)
            {
                return GetPropertyName(unaryExpr.Operand);
            }

            return null;
        }

        internal object GetValue(Expression expression)
        {
            if (expression is ConstantExpression constantExpr)
            {
                return constantExpr.Value;
            }
            else if (expression is MemberExpression memberExpr)
            {
                // Evaluate the member expression
                var objectMember = Expression.Convert(memberExpr, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                var getter = getterLambda.Compile();
                return getter();
            }
            else if (expression is UnaryExpression unaryExpr)
            {
                return GetValue(unaryExpr.Operand);
            }
            else if (expression is BinaryExpression binaryExpr)
            {
                // Handle binary expressions like baseAge + offset
                try
                {
                    // Compile and evaluate the binary expression
                    var lambda = Expression.Lambda<Func<object>>(
                             Expression.Convert(binaryExpr, typeof(object)));
                    var compiled = lambda.Compile();
                    return compiled();
                }
                catch
                {
                    // If compilation fails, return null
                    return null;
                }
            }

            return null;
        }

        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length == 0)
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static Expression StripQuotes(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Quote)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Check if this is a GraphQueryable using cached type operations
            if (node.Value != null && ReflectionCache.IsGenericType(node.Value.GetType()))
            {
                var nodeType = node.Value.GetType();
                if (nodeType.GetGenericTypeDefinition() == typeof(GraphQueryable<>))
                {
                    // Extract element type using cached generic arguments
                    var genericArgs = ReflectionCache.GetGenericArguments(node.Type);
                    if (genericArgs.Length > 0)
                    {
                        _elementType = genericArgs[0];
                    }
                }
            }

            return node;
        }
    }
}
