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
    /// error behavior. <b>REFLECTION: NO.</b> The target setter is compiled once per property path and cached.
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
    /// <remarks><b>REFLECTION: NO.</b> The target setter is compiled once per property path and cached.</remarks>
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

    /// <summary>
    /// Creates a two-way binding between source and target properties of the same type.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TTarget">The target object type.</typeparam>
    /// <typeparam name="TValue">The bound property value type.</typeparam>
    /// <param name="target">The target object on which this extension is invoked.</param>
    /// <param name="source">The source object that supplies the initial value.</param>
    /// <param name="sourceProperty">The observed, writable source property path.</param>
    /// <param name="targetProperty">The observed, writable target property path.</param>
    /// <returns>A disposable object that stops updates in both directions.</returns>
    public static IDisposable TwoWayBind<TSource, TTarget, TValue>(
        this TTarget target,
        TSource source,
        Expression<Func<TSource, TValue>> sourceProperty,
        Expression<Func<TTarget, TValue>> targetProperty
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged =>
        TwoWayBind(
            target,
            source,
            sourceProperty,
            targetProperty,
            static value => value,
            static value => value
        );

    /// <summary>
    /// Creates a converted two-way binding between source and target properties.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TTarget">The target object type.</typeparam>
    /// <typeparam name="TSourceValue">The source property value type.</typeparam>
    /// <typeparam name="TTargetValue">The target property value type.</typeparam>
    /// <param name="target">The target object on which this extension is invoked.</param>
    /// <param name="source">The source object that supplies the initial value.</param>
    /// <param name="sourceProperty">The observed, writable source property path.</param>
    /// <param name="targetProperty">The observed, writable target property path.</param>
    /// <param name="sourceToTarget">Converts a source value before assigning it to the target.</param>
    /// <param name="targetToSource">Converts a target value before assigning it to the source.</param>
    /// <returns>A disposable object that stops updates in both directions.</returns>
    public static IDisposable TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TTarget target,
        TSource source,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Func<TSourceValue, TTargetValue> sourceToTarget,
        Func<TTargetValue, TSourceValue> targetToSource
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(sourceToTarget);
        ArgumentNullException.ThrowIfNull(targetToSource);

        var sourceSetter = PropertySetter.Create(sourceProperty);
        var targetSetter = PropertySetter.Create(targetProperty);
        return TwoWayBind(
            target,
            source,
            sourceProperty,
            targetProperty,
            (value, currentTarget) => targetSetter(currentTarget, sourceToTarget(value)),
            (value, currentSource) => sourceSetter(currentSource, targetToSource(value))
        );
    }

    /// <summary>
    /// Creates a two-way binding using caller-provided assignment actions for both directions.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TTarget">The target object type.</typeparam>
    /// <typeparam name="TSourceValue">The observed source value type.</typeparam>
    /// <typeparam name="TTargetValue">The observed target value type.</typeparam>
    /// <param name="target">The target object on which this extension is invoked.</param>
    /// <param name="source">The source object that supplies the initial value.</param>
    /// <param name="sourceProperty">The source property path to observe.</param>
    /// <param name="targetProperty">The target property path to observe.</param>
    /// <param name="assignTarget">Assigns a source value to the target.</param>
    /// <param name="assignSource">Assigns a target value to the source.</param>
    /// <returns>A disposable object that stops updates in both directions.</returns>
    /// <remarks>
    /// The source value initializes the target. The assignment actions are called directly and are not parsed,
    /// compiled, or invoked through reflection. Property observation follows
    /// <see cref="WhenAnyExtensions.WhenAnyValue{TSource,TValue}"/>.
    /// </remarks>
    public static IDisposable TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TTarget target,
        TSource source,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Action<TSourceValue, TTarget> assignTarget,
        Action<TTargetValue, TSource> assignSource
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(assignTarget);
        ArgumentNullException.ThrowIfNull(assignSource);

        var state = new TwoWayBindingState<TSource, TTarget, TSourceValue, TTargetValue>(
            source,
            target,
            assignTarget,
            assignSource
        );
        var subscriptions = new MultipleDisposable { state };

        try
        {
            source
                .WhenAnyValue(sourceProperty)
                .Subscribe(state.UpdateTarget)
                .DisposeWith(subscriptions);
            target
                .WhenAnyValue(targetProperty)
                .Subscribe(state.UpdateSource)
                .DisposeWith(subscriptions);
            return subscriptions;
        }
        catch
        {
            subscriptions.Dispose();
            throw;
        }
    }

    private sealed class TwoWayBindingState<TSource, TTarget, TSourceValue, TTargetValue>(
        TSource source,
        TTarget target,
        Action<TSourceValue, TTarget> assignTarget,
        Action<TTargetValue, TSource> assignSource
    ) : IDisposable
        where TSource : class
        where TTarget : class
    {
        private const int Idle = 0;
        private const int UpdatingTarget = 1;
        private const int UpdatingSource = 2;
        private const int Disposed = 3;

        private int _state;

        public void UpdateTarget(TSourceValue value)
        {
            // Source-to-target has priority: it may replace UpdatingSource, while
            // UpdateSource is only allowed to enter from Idle.
            int state;
            do
            {
                state = Volatile.Read(ref _state);
                if (state is UpdatingTarget or Disposed)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _state, UpdatingTarget, state) != state);

            try
            {
                assignTarget(value, target);
            }
            finally
            {
                Interlocked.CompareExchange(ref _state, Idle, UpdatingTarget);
            }
        }

        public void UpdateSource(TTargetValue value)
        {
            if (Interlocked.CompareExchange(ref _state, UpdatingSource, Idle) != Idle)
            {
                return;
            }

            try
            {
                assignSource(value, source);
            }
            finally
            {
                Interlocked.CompareExchange(ref _state, Idle, UpdatingSource);
            }
        }

        public void Dispose() => Interlocked.Exchange(ref _state, Disposed);
    }

    private static class PropertySetter
    {
        public static Action<TTarget, TValue> Create<TTarget, TValue>(
            Expression<Func<TTarget, TValue>> propertyExpression
        )
        {
            var key = PropertyPathCacheKey<TTarget, TValue>.Create(propertyExpression);
            return PropertySetterCache<TTarget, TValue>.Setters.Get(key);
        }

        private static Action<TTarget, TValue> Compile<TTarget, TValue>(
            PropertyPathCacheKey<TTarget, TValue> key
        )
        {
            var propertyExpression = key.Expression;
            var property = key.FinalProperty;
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

        private static class PropertySetterCache<TTarget, TValue>
        {
            public static readonly MemoizingLRUCache<
                PropertyPathCacheKey<TTarget, TValue>,
                Action<TTarget, TValue>
            > Setters = new(Compile, 64);
        }

        private readonly struct PropertyPathCacheKey<TTarget, TValue>
            : IEquatable<PropertyPathCacheKey<TTarget, TValue>>
        {
            private readonly int _hashCode;

            private PropertyPathCacheKey(
                Expression<Func<TTarget, TValue>> expression,
                PropertyInfo finalProperty,
                int hashCode
            )
            {
                Expression = expression;
                FinalProperty = finalProperty;
                _hashCode = hashCode;
            }

            public Expression<Func<TTarget, TValue>> Expression { get; }

            public PropertyInfo FinalProperty { get; }

            public static PropertyPathCacheKey<TTarget, TValue> Create(
                Expression<Func<TTarget, TValue>> expression
            )
            {
                var finalProperty = GetTargetProperty(expression);
                if (finalProperty.SetMethod is null)
                {
                    throw new ArgumentException(
                        "The final property in the expression must be writable.",
                        nameof(expression)
                    );
                }

                var hash = new HashCode();
                Expression current = GetPropertyBody(expression);
                while (current is MemberExpression member)
                {
                    hash.Add(member.Member);
                    current = member.Expression!;
                }

                return new PropertyPathCacheKey<TTarget, TValue>(
                    expression,
                    finalProperty,
                    hash.ToHashCode()
                );
            }

            public bool Equals(PropertyPathCacheKey<TTarget, TValue> other)
            {
                Expression current = GetPropertyBody(Expression);
                Expression otherCurrent = GetPropertyBody(other.Expression);

                while (
                    current is MemberExpression member
                    && otherCurrent is MemberExpression otherMember
                )
                {
                    if (member.Member != otherMember.Member)
                    {
                        return false;
                    }

                    current = member.Expression!;
                    otherCurrent = otherMember.Expression!;
                }

                return current is ParameterExpression && otherCurrent is ParameterExpression;
            }

            public override bool Equals(object? obj) =>
                obj is PropertyPathCacheKey<TTarget, TValue> other && Equals(other);

            public override int GetHashCode() => _hashCode;
        }
    }
}
