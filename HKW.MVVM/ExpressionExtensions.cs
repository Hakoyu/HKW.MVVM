using System.Linq.Expressions;

namespace HKW.MVVM;

/// <summary>Provides helpers for expression trees.</summary>
public static class ExpressionExtensions
{
    /// <summary>Gets the selected member name from an expression.</summary>
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