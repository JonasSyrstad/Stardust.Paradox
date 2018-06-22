using System;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data
{
    public static class QueryFuncExt
    {
        public static Func<GremlinContext, GremlinQuery> Extend(this Func<GremlinContext, GremlinQuery> query,
            Func<GremlinQuery, GremlinQuery> queryExtention)
        {
            return context => queryExtention.Invoke(query.Invoke(context));
        }
        public static string GetInfo<T>(Expression<Func<T, object>> action)
        {
            var expression = GetMemberInfo(action);
            string name = expression.Member.Name;
            return name;

        }

        private static MemberExpression GetMemberInfo(Expression method)
        {
            LambdaExpression lambda = method as LambdaExpression;
            if (lambda == null)
                throw new ArgumentNullException("method");

            MemberExpression memberExpr = null;

            if (lambda.Body.NodeType == ExpressionType.Convert)
            {
                memberExpr =
                    ((UnaryExpression)lambda.Body).Operand as MemberExpression;
            }
            else if (lambda.Body.NodeType == ExpressionType.MemberAccess)
            {
                memberExpr = lambda.Body as MemberExpression;
            }

            if (memberExpr == null)
                throw new ArgumentException("method");

            return memberExpr;
        }

        internal static string GetEdgeLabel<T>(Expression<Func<T, object>> action) where T:IVertex
        {
            var expression = GetMemberInfo(action);
            var name = expression.Member.GetCustomAttribute<EdgeLabelAttribute>()?.Label?? expression.Member.GetCustomAttribute<ReverseEdgeLabelAttribute>() ?.ReverseLabel;
            return name;
        }
    }
}