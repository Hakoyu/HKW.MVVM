using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace HKW.MVVM;

/// <summary>Provides one-way and two-way property binding helpers.</summary>
public static class BindingExtensions
{
    /// <summary>Binds values produced by an observable sequence using a caller-provided assignment action.</summary>
    /// <typeparam name="TValue">The value type produced by the source.</typeparam>
    /// <typeparam name="TTarget">The target object type.</typeparam>
    /// <param name="source">The observable sequence that supplies values.</param>
    /// <param name="target">The target object passed to <paramref name="assignment"/>.</param>
    /// <param name="assignment">The action that receives each source value and the target object.</param>
    /// <returns>A disposable object that stops the binding.</returns>
    /// <remarks>
    /// This overload does not parse or compile an expression and does not use reflection. It also permits
    /// assignments that cannot be represented by a property expression, such as dependency-property setters.
    /// </remarks>
    public static IDisposable BindTo<TValue, TTarget>(
        this IObservable<TValue> source,
        TTarget target,
        Action<TValue, TTarget> assignment
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(assignment);
        return source.Subscribe(value => assignment(value, target));
    }

    /// <summary>Binds values produced by an observable sequence to a writable target property.</summary>
    /// <typeparam name="TTarget">The target object type.</typeparam>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="source">The observable sequence that supplies values.</param>
    /// <param name="target">The object whose property receives values.</param>
    /// <param name="targetProperty">A writable property path rooted at <paramref name="target"/>.</param>
    /// <returns>A disposable object that stops the binding.</returns>
    /// <remarks>
    /// Every source value is assigned synchronously. Source errors use the standard
    /// <see cref="NativeObservableSubscriptionExtensions.Subscribe{T}(IObservable{T}, Action{T})"/>
    /// error behavior. <b>REFLECTION: NO.</b> The target setter is compiled once from the expression.
    /// </remarks>
    public static IDisposable BindTo<TTarget, TValue>(
        this IObservable<TValue> source,
        TTarget target,
        Expression<Func<TTarget, TValue>> targetProperty
    )
        where TTarget : class => BindTo(source, target, targetProperty, static value => value);

    /// <summary>Binds converted values produced by an observable sequence to a writable target property.</summary>
    /// <typeparam name="TSourceValue">The value type produced by the source.</typeparam>
    /// <typeparam name="TTarget">The target object type.</typeparam>
    /// <typeparam name="TTargetValue">The target property value type.</typeparam>
    /// <param name="source">The observable sequence that supplies values.</param>
    /// <param name="target">The object whose property receives values.</param>
    /// <param name="targetProperty">A writable property path rooted at <paramref name="target"/>.</param>
    /// <param name="converter">The function that converts source values to target values.</param>
    /// <returns>A disposable object that stops the binding.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The target setter is compiled once from the expression.</remarks>
    public static IDisposable BindTo<TSourceValue, TTarget, TTargetValue>(
        this IObservable<TSourceValue> source,
        TTarget target,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Func<TSourceValue, TTargetValue> converter
    )
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(converter);

        var setter = PropertySetter.Create(targetProperty);
        return source.Subscribe(value => setter(target, converter(value)));
    }

    private static class PropertySetter
    {
        public static Action<TTarget, TValue> Create<TTarget, TValue>(
            Expression<Func<TTarget, TValue>> propertyExpression
        )
        {
            var property = GetTargetProperty(propertyExpression);
            if (property.SetMethod is null)
            {
                throw new ArgumentException(
                    "The final property in the expression must be writable.",
                    nameof(propertyExpression)
                );
            }

            var value = Expression.Parameter(typeof(TValue), "value");
            Expression assignedValue = value;
            if (assignedValue.Type != property.PropertyType)
            {
                assignedValue = Expression.Convert(assignedValue, property.PropertyType);
            }

            try
            {
                return Expression
                    .Lambda<Action<TTarget, TValue>>(
                        Expression.Assign(GetPropertyBody(propertyExpression), assignedValue),
                        propertyExpression.Parameters[0],
                        value
                    )
                    .Compile();
            }
            catch (Exception exception)
                when (exception is ArgumentException or InvalidOperationException)
            {
                throw new ArgumentException(
                    "The expression must identify an assignable property path.",
                    nameof(propertyExpression),
                    exception
                );
            }
        }

        private static PropertyInfo GetTargetProperty(LambdaExpression expression)
        {
            Expression current = GetPropertyBody(expression);
            PropertyInfo? finalProperty = null;

            while (current is MemberExpression member)
            {
                if (member.Member is not PropertyInfo property || property.GetMethod is null)
                {
                    throw new ArgumentException(
                        "The expression must contain readable properties only.",
                        nameof(expression)
                    );
                }

                finalProperty ??= property;
                current = member.Expression!;
            }

            if (current != expression.Parameters[0] || finalProperty is null)
            {
                throw new ArgumentException(
                    "The expression must be a property path rooted at its parameter.",
                    nameof(expression)
                );
            }

            return finalProperty;
        }

        private static Expression GetPropertyBody(LambdaExpression expression) =>
            expression.Body
                is UnaryExpression
                {
                    NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
                } conversion
                ? conversion.Operand
                : expression.Body;
    }
}
