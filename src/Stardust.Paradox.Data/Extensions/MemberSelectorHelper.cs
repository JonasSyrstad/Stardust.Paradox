using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Extensions
{
    internal static class MemberSelectorExtensions
    {
        internal static MemberExpression ExtractMemberExpression(this Expression expression)
        {
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                return ((MemberExpression)expression);
            }

            if (expression.NodeType == ExpressionType.Convert)
            {
                var operand = ((UnaryExpression)expression).Operand;
                return ExtractMemberExpression(operand);
            }

            return null;
        }
    }
}