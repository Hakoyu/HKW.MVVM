using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace HKW.MVVM;

/// <summary>
/// 提供单向和双向属性绑定辅助方法.
/// </summary>
public static class BindingExtensions
{
    /// <summary>
/// 使用调用方提供的赋值操作绑定可观察序列产生的值.
/// </summary>
    /// <typeparam name="TValue">源产生的值类型.</typeparam>
    /// <typeparam name="TTarget">目标对象的类型.</typeparam>
    /// <param name="source">提供值的可观察序列.</param>
    /// <param name="target">传递给 <paramref name="assignment"/> 的目标对象.</param>
    /// <param name="assignment">接收每个源值和目标对象的操作.</param>
    /// <returns>可停止绑定的可释放对象.</returns>
    /// <remarks>
    /// 此重载不解析或编译表达式,也不使用反射.它同样允许
    /// 属性表达式无法表示的赋值,例如依赖属性 setter.
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

    /// <summary>
/// 将可观察序列产生的值绑定到可写的目标属性.
/// </summary>
    /// <typeparam name="TTarget">目标对象的类型.</typeparam>
    /// <typeparam name="TValue">源和目标的值的类型.</typeparam>
    /// <param name="source">提供值的可观察序列.</param>
    /// <param name="target">接收值的属性所属对象.</param>
    /// <param name="targetProperty">以 <paramref name="target"/> 为根的、可写的属性路径.</param>
    /// <returns>可停止绑定的可释放对象.</returns>
    /// <remarks>
    /// 每个源值都会同步赋值.源错误采用
    /// <see cref="NativeObservableSubscriptionExtensions.Subscribe{T}(IObservable{T}, Action{T})"/>
    /// 的标准错误行为.<b>反射:否.</b>目标 setter 会针对每个属性路径编译一次并缓存.
    /// </remarks>
    public static IDisposable BindTo<TTarget, TValue>(
        this IObservable<TValue> source,
        TTarget target,
        Expression<Func<TTarget, TValue>> targetProperty
    )
        where TTarget : class => BindTo(source, target, targetProperty, static value => value);

    /// <summary>
/// 将可观察序列产生的、经过转换的值绑定到可写的目标属性.
/// </summary>
    /// <typeparam name="TSourceValue">源产生的值类型.</typeparam>
    /// <typeparam name="TTarget">目标对象的类型.</typeparam>
    /// <typeparam name="TTargetValue">目标属性的值类型.</typeparam>
    /// <param name="source">提供值的可观察序列.</param>
    /// <param name="target">接收值的属性所属对象.</param>
    /// <param name="targetProperty">以 <paramref name="target"/> 为根的、可写的属性路径.</param>
    /// <param name="converter">将源值转换为目标值的函数.</param>
    /// <returns>可停止绑定的可释放对象.</returns>
    /// <remarks><b>反射:否.</b>目标 setter 会针对每个属性路径编译一次并缓存.</remarks>
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
    /// 在类型相同的源属性和目标属性之间创建双向绑定.
    /// </summary>
    /// <typeparam name="TSource">源对象的类型.</typeparam>
    /// <typeparam name="TTarget">目标对象的类型.</typeparam>
    /// <typeparam name="TValue">所绑定属性的值类型.</typeparam>
    /// <param name="target">调用此扩展方法的目标对象.</param>
    /// <param name="source">提供初始值的源对象.</param>
    /// <param name="sourceProperty">被观察的、可写的源属性路径.</param>
    /// <param name="targetProperty">被观察的、可写的目标属性路径.</param>
    /// <returns>可停止双向更新的可释放对象.</returns>
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
    /// 在源属性和目标属性之间创建经过转换的双向绑定.
    /// </summary>
    /// <typeparam name="TSource">源对象的类型.</typeparam>
    /// <typeparam name="TTarget">目标对象的类型.</typeparam>
    /// <typeparam name="TSourceValue">源属性的值类型.</typeparam>
    /// <typeparam name="TTargetValue">目标属性的值类型.</typeparam>
    /// <param name="target">调用此扩展方法的目标对象.</param>
    /// <param name="source">提供初始值的源对象.</param>
    /// <param name="sourceProperty">被观察的、可写的源属性路径.</param>
    /// <param name="targetProperty">被观察的、可写的目标属性路径.</param>
    /// <param name="sourceToTarget">在将源值赋给目标之前对其进行转换.</param>
    /// <param name="targetToSource">在将目标值赋给源之前对其进行转换.</param>
    /// <returns>可停止双向更新的可释放对象.</returns>
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
    /// 使用调用方为两个方向提供的赋值操作创建双向绑定.
    /// </summary>
    /// <typeparam name="TSource">源对象的类型.</typeparam>
    /// <typeparam name="TTarget">目标对象的类型.</typeparam>
    /// <typeparam name="TSourceValue">被观察的源值类型.</typeparam>
    /// <typeparam name="TTargetValue">被观察的目标值类型.</typeparam>
    /// <param name="target">调用此扩展方法的目标对象.</param>
    /// <param name="source">提供初始值的源对象.</param>
    /// <param name="sourceProperty">要观察的源属性路径.</param>
    /// <param name="targetProperty">要观察的目标属性路径.</param>
    /// <param name="assignTarget">将源值赋给目标.</param>
    /// <param name="assignSource">将目标值赋给源.</param>
    /// <returns>可停止双向更新的可释放对象.</returns>
    /// <remarks>
    /// 源值用于初始化目标.赋值操作会被直接调用,不经过解析、编译
    /// 或反射调用.属性观察遵循
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
            // 源到目标具有更高优先级:它可以替换 UpdatingSource,而
            // UpdateSource 只允许从 Idle 状态进入.
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
