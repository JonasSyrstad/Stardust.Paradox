using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Select method calls (projection)
    /// </summary>
    public class SelectVisitor : ExpressionVisitorBase
    {
        /// <inheritdoc />
        public override string MethodName => "Select";

        /// <inheritdoc />
        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Check if source is GroupBy - if so, delegate to GroupByTranslator
            if (node.Arguments[0] is MethodCallExpression sourceCall &&
                sourceCall.Method.Name == "GroupBy" &&
                (sourceCall.Method.DeclaringType == typeof(Queryable) ||
                 sourceCall.Method.DeclaringType == typeof(Enumerable)))
            {
                // Visit the GroupBy source first (this will also visit what's before GroupBy)
                context.VisitExpression(sourceCall.Arguments[0]);

                // If the source triggered client-side projection, don't translate GroupBy or this Select
                // They will be handled by ChainedOperationsHandler
                if (context.RequiresClientSideProjection)
                {
                    return node;
                }

                // Use GroupByTranslator to handle the complex pattern
                var groupByTranslator = new GroupByTranslator(context.GremlinQuery);
                groupByTranslator.TranslateGroupByWithSelect(sourceCall, node);

                context.IsGroupByQuery = true;
                return node;
            }

            // Visit source
            VisitSource(node, context);

            // If visiting source triggered client-side projection, we're done
            // This Select and any subsequent operations will be handled client-side
            if (context.RequiresClientSideProjection)
            {
                return node;
            }

            // Extract lambda
            var lambda = (LambdaExpression)context.StripQuotes(node.Arguments[1]);

            // Check if the projection is an edge traversal (OutE/InE with optional Cast)
            if (IsEdgeTraversalProjection(lambda.Body))
            {
                // Handle edge traversal manually
                var edgeMethod = GetEdgeTraversalMethod(lambda.Body);
                if (edgeMethod != null)
                {
                    // Get the edge label from the OutE/InE method
                    if (edgeMethod.Arguments.Count > 1)
                    {
                        var edgeLambda = (LambdaExpression)context.StripQuotes(edgeMethod.Arguments[1]);
                        var edgeLabel = GetEdgeLabelFromExpression(edgeLambda.Body, context);

                        if (!string.IsNullOrEmpty(edgeLabel))
                        {
                            if (edgeMethod.Method.Name == "OutE")
                            {
                                context.GremlinQuery.Append($".outE('{edgeLabel}')");
                            }
                            else if (edgeMethod.Method.Name == "InE")
                            {
                                context.GremlinQuery.Append($".inE('{edgeLabel}')");
                            }
                        }
                    }
                    else
                    {
                        if (edgeMethod.Method.Name == "OutE")
                        {
                            context.GremlinQuery.Append(".outE()");
                        }
                        else if (edgeMethod.Method.Name == "InE")
                        {
                            context.GremlinQuery.Append(".inE()");
                        }
                    }
                }

                // Update element type based on the edge traversal
                if (lambda.Body is MethodCallExpression castMethodCall &&
                    castMethodCall.Method.Name == "Cast" &&
                    castMethodCall.Method.IsGenericMethod)
                {
                    // Cast<T>() - element type is T
                    context.ElementType = castMethodCall.Method.GetGenericArguments()[0];
                }
                else if (edgeMethod.Method.IsGenericMethod)
                {
                    // OutE<TSource, TTarget, TEdge>() - element type is TEdge
                    var genericArgs = edgeMethod.Method.GetGenericArguments();
                    if (genericArgs.Length >= 3)
                    {
                        context.ElementType = genericArgs[2]; // TEdge
                    }
                }

                return node;
            }

            // Check if this requires client-side evaluation
            if (ProjectionExpressionEvaluator.RequiresClientSideEvaluation(lambda))
            {
                // Mark for client-side projection
                context.RequiresClientSideProjection = true;
                context.ClientSideProjection = lambda;
                context.ElementType = lambda.ReturnType;

                // Extract required properties for server-side retrieval
                var requiredProperties = ProjectionExpressionEvaluator.ExtractRequiredProperties(lambda);
                var propertyQuery = ProjectionExpressionEvaluator.GetPropertyRetrievalQuery(requiredProperties);
                context.GremlinQuery.Append(propertyQuery);

                return node;
            }

            // Build values() or valueMap() step for server-side projection
            if (lambda.Body is MemberExpression memberExpr)
            {
                var propertyName = context.ToCamelCase(memberExpr.Member.Name);
                context.GremlinQuery.Append($".values('{propertyName}')");

                // For single property selection, the element type becomes the property type
                context.ElementType = memberExpr.Type;
            }
            else if (lambda.Body is NewExpression newExpr)
            {
                // Anonymous type projection
                var properties = newExpr.Arguments
                    .OfType<MemberExpression>()
                    .Select(m => $"'{context.ToCamelCase(m.Member.Name)}'")
                    .ToArray();

                if (properties.Length > 0)
                {
                    context.GremlinQuery.Append($".valueMap({string.Join(",", properties)})");

                    // For anonymous type projection, the element type becomes the anonymous type
                    context.ElementType = newExpr.Type;
                }
            }

            return node;
        }

        /// <summary>
        /// Checks if the projection expression is an edge traversal (OutE/InE with optional Cast)
        /// </summary>
        private bool IsEdgeTraversalProjection(Expression expr)
        {
            if (expr is MethodCallExpression methodCall)
            {
                // Check for Cast<T>() wrapping OutE/InE
                if (methodCall.Method.Name == "Cast" && methodCall.Object != null)
                {
                    return IsEdgeTraversalProjection(methodCall.Object);
                }

                // Check for OutE or InE directly
                return methodCall.Method.Name == "OutE" || methodCall.Method.Name == "InE";
            }

            return false;
        }

        /// <summary>
        /// Gets the OutE or InE method call from the expression (unwrapping Cast if present)
        /// </summary>
        private MethodCallExpression GetEdgeTraversalMethod(Expression expr)
        {
            if (expr is MethodCallExpression methodCall)
            {
                // If it's Cast, get the wrapped method
                if (methodCall.Method.Name == "Cast" && methodCall.Object != null)
                {
                    return GetEdgeTraversalMethod(methodCall.Object);
                }

                // If it's OutE or InE, return it
                if (methodCall.Method.Name == "OutE" || methodCall.Method.Name == "InE")
                {
                    return methodCall;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts edge label from a lambda expression body
        /// </summary>
        private string GetEdgeLabelFromExpression(Expression expression, IVisitorContext context)
        {
            // Extract property name from p => p.Companies
            if (expression is MemberExpression memberExpr)
            {
                var propertyInfo = memberExpr.Member as PropertyInfo;
                if (propertyInfo != null)
                {
                    // Check for attributes on the property
                    var edgeLabelAttr = propertyInfo.GetCustomAttribute<EdgeLabelAttribute>();
                    if (edgeLabelAttr != null)
                        return edgeLabelAttr.Label;

                    var reverseLabelAttr = propertyInfo.GetCustomAttribute<ReverseEdgeLabelAttribute>();
                    if (reverseLabelAttr != null)
                        return reverseLabelAttr.ReverseLabel;
                }

                // Fallback: Convert property name to camelCase for edge label
                return context.ToCamelCase(memberExpr.Member.Name);
            }

            return null;
        }
    }
}
