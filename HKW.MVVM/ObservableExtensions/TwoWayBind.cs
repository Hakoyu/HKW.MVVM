using System.ComponentModel;
using System.Linq.Expressions;

#pragma warning disable S2436

namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在类型相同的源属性和目标属性之间创建双向绑定
    /// </summary>
    /// <typeparam name="TSource">源对象的类型</typeparam>
    /// <typeparam name="TTarget">目标对象的类型</typeparam>
    /// <typeparam name="TValue">所绑定属性的值类型</typeparam>
    /// <param name="target">调用此扩展方法的目标对象</param>
    /// <param name="source">提供初始值的源对象</param>
    /// <param name="sourceProperty">被观察的,可写的源属性路径</param>
    /// <param name="targetProperty">被观察的,可写的目标属性路径</param>
    /// <returns>可停止双向更新的可释放对象</returns>
    public static IDisposable TwoWayBind<TSource, TTarget, TValue>(
        this TSource source,
        TTarget target,
        Expression<Func<TSource, TValue>> sourceProperty,
        Expression<Func<TTarget, TValue>> targetProperty
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged =>
        TwoWayBind(
            source,
            target,
            sourceProperty,
            targetProperty,
            static value => value,
            static value => value
        );

    /// <summary>
    /// 在源属性和目标属性之间创建经过转换的双向绑定
    /// </summary>
    /// <typeparam name="TSource">源对象的类型</typeparam>
    /// <typeparam name="TTarget">目标对象的类型</typeparam>
    /// <typeparam name="TSourceValue">源属性的值类型</typeparam>
    /// <typeparam name="TTargetValue">目标属性的值类型</typeparam>
    /// <param name="target">调用此扩展方法的目标对象</param>
    /// <param name="source">提供初始值的源对象</param>
    /// <param name="sourceProperty">被观察的,可写的源属性路径</param>
    /// <param name="targetProperty">被观察的,可写的目标属性路径</param>
    /// <param name="sourceToTarget">在将源值赋给目标之前对其进行转换</param>
    /// <param name="targetToSource">在将目标值赋给源之前对其进行转换</param>
    /// <returns>可停止双向更新的可释放对象</returns>
    public static IDisposable TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TSource source,
        TTarget target,
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
            source,
            target,
            sourceProperty,
            targetProperty,
            (value, currentTarget) => targetSetter(currentTarget, sourceToTarget(value)),
            (value, currentSource) => sourceSetter(currentSource, targetToSource(value))
        );
    }

    /// <summary>
    /// 使用调用方为两个方向提供的赋值操作创建双向绑定
    /// </summary>
    /// <typeparam name="TSource">源对象的类型</typeparam>
    /// <typeparam name="TTarget">目标对象的类型</typeparam>
    /// <typeparam name="TSourceValue">被观察的源值类型</typeparam>
    /// <typeparam name="TTargetValue">被观察的目标值类型</typeparam>
    /// <param name="target">调用此扩展方法的目标对象</param>
    /// <param name="source">提供初始值的源对象</param>
    /// <param name="sourceProperty">要观察的源属性路径</param>
    /// <param name="targetProperty">要观察的目标属性路径</param>
    /// <param name="assignTarget">将源值赋给目标</param>
    /// <param name="assignSource">将目标值赋给源</param>
    /// <returns>可停止双向更新的可释放对象</returns>
    /// <remarks>
    /// 反射: 否
    /// </remarks>
    public static IDisposable TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TSource source,
        TTarget target,
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
            // UpdateSource 只允许从 Idle 状态进入
            int state;
            do
            {
                state = Volatile.Read(ref _state);
                if (state is UpdatingTarget or Disposed)
                {
                    return;
                }
            } while (Interlocked.CompareExchange(ref _state, UpdatingTarget, state) != state);

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
}
