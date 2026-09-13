using System.Linq.Expressions;

namespace HKW.MVVM;

/// <summary>
/// 提供表达式树的辅助方法.
/// </summary>
internal static class ExpressionExtensions
{
    /// <summary>
/// 从表达式树中获取所选的成员名称.
/// </summary>
    public static string GetPropertyName(this LambdaExpression expression)
    {
        if (expression.Body is MemberExpression member)
        {
            return member.Member.Name;
        }
        else if (
            expression.Body is UnaryExpression unary
            && unary.Operand is MemberExpression unaryMember
        )
        {
            return unaryMember.Member.Name;
        }
        else
            throw new ArgumentException($"Not supported expression \"{expression}\"");
    }
}
