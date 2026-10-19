using System.ComponentModel;
using System.Linq.Expressions;

#pragma warning disable S2436

namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在类型相同的属性之间创建双向绑定
    /// </summary>
    public static BindingResult TwoWayBind<TSource, TTarget, TValue>(
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
            new TwoWayBindingOptions<TValue, TValue>()
        );

    /// <summary>
    /// 使用选项在类型相同的属性之间创建双向绑定
    /// </summary>
    public static BindingResult TwoWayBind<TSource, TTarget, TValue>(
        this TSource source,
        TTarget target,
        Expression<Func<TSource, TValue>> sourceProperty,
        Expression<Func<TTarget, TValue>> targetProperty,
        TwoWayBindingOptions<TValue, TValue> options
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged =>
        TwoWayBind(
            source,
            target,
            sourceProperty,
            targetProperty,
            static value => value,
            static value => value,
            options
        );

    /// <summary>
    /// 在属性之间创建经过转换的双向绑定
    /// </summary>
    public static BindingResult TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TSource source,
        TTarget target,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Func<TSourceValue, TTargetValue> sourceToTarget,
        Func<TTargetValue, TSourceValue> targetToSource
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged =>
        TwoWayBind(
            source,
            target,
            sourceProperty,
            targetProperty,
            sourceToTarget,
            targetToSource,
            new TwoWayBindingOptions<TSourceValue, TTargetValue>()
        );

    /// <summary>
    /// 使用选项在属性之间创建经过转换的双向绑定
    /// </summary>
    public static BindingResult TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TSource source,
        TTarget target,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Func<TSourceValue, TTargetValue> sourceToTarget,
        Func<TTargetValue, TSourceValue> targetToSource,
        TwoWayBindingOptions<TSourceValue, TTargetValue> options
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged
    {
        ValidateTwoWayArguments(source, target, sourceProperty, targetProperty, options);
        ArgumentNullException.ThrowIfNull(sourceToTarget);
        ArgumentNullException.ThrowIfNull(targetToSource);
        var sourceSetter = PropertySetter.Create(sourceProperty);
        var targetSetter = PropertySetter.Create(targetProperty);
        return TwoWayBindCore(
            source,
            target,
            sourceProperty,
            targetProperty,
            value =>
                targetSetter(
                    target,
                    value is null && options.TargetNullValue.HasValue
                        ? options.TargetNullValue.Value
                        : sourceToTarget(value)
                ),
            value =>
                sourceSetter(
                    source,
                    value is null && options.SourceNullValue.HasValue
                        ? options.SourceNullValue.Value
                        : targetToSource(value)
                ),
            options
        );
    }

    /// <summary>
    /// 使用调用方提供的赋值操作创建双向绑定
    /// </summary>
    public static BindingResult TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TSource source,
        TTarget target,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Action<TSourceValue, TTarget> assignTarget,
        Action<TTargetValue, TSource> assignSource
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged =>
        TwoWayBind(
            source,
            target,
            sourceProperty,
            targetProperty,
            assignTarget,
            assignSource,
            new TwoWayBindingOptions<TSourceValue, TTargetValue>()
        );

    /// <summary>
    /// 使用选项和调用方提供的赋值操作创建双向绑定
    /// </summary>
    public static BindingResult TwoWayBind<TSource, TTarget, TSourceValue, TTargetValue>(
        this TSource source,
        TTarget target,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Action<TSourceValue, TTarget> assignTarget,
        Action<TTargetValue, TSource> assignSource,
        TwoWayBindingOptions<TSourceValue, TTargetValue> options
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(assignTarget);
        ArgumentNullException.ThrowIfNull(assignSource);
        return TwoWayBindCore(
            source,
            target,
            sourceProperty,
            targetProperty,
            value => assignTarget(value, target),
            value => assignSource(value, source),
            options
        );
    }

    private static BindingResult TwoWayBindCore<TSource, TTarget, TSourceValue, TTargetValue>(
        TSource source,
        TTarget target,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Action<TSourceValue> assignTarget,
        Action<TTargetValue> assignSource,
        TwoWayBindingOptions<TSourceValue, TTargetValue> options
    )
        where TSource : class, INotifyPropertyChanged
        where TTarget : class, INotifyPropertyChanged
    {
        ValidateTwoWayArguments(source, target, sourceProperty, targetProperty, options);

        var result = new BindingResult();
        var state = new TwoWayBindingState<TSourceValue, TTargetValue>(
            result,
            options.Scheduler,
            assignTarget,
            assignSource
        );
        var subscriptions = new MultipleDisposable();

        try
        {
            var sourceOptions = new WhenAnyValueOptions<TSourceValue>
            {
                Comparer = options.SourceComparer,
                SkipInitial = true,
                PathUnavailableFallback = options.SourceFallbackValue.HasValue
                    ? () => options.SourceFallbackValue.Value
                    : null,
            };
            var targetOptions = new WhenAnyValueOptions<TTargetValue>
            {
                Comparer = options.TargetComparer,
                SkipInitial = true,
                PathUnavailableFallback = options.TargetFallbackValue.HasValue
                    ? () => options.TargetFallbackValue.Value
                    : null,
            };

            source
                .WhenAnyValue(sourceProperty, sourceOptions)
                .Subscribe(state.UpdateTarget, result.Fail)
                .DisposeWith(subscriptions);
            target
                .WhenAnyValue(targetProperty, targetOptions)
                .Subscribe(state.UpdateSource, result.Fail)
                .DisposeWith(subscriptions);

            var sourceGetter = sourceProperty.Compile();
            var targetGetter = targetProperty.Compile();
            switch (options.InitialSyncDirection)
            {
                case BindingInitialSyncDirection.SourceToTarget:
                    state.UpdateTarget(sourceGetter(source));
                    break;
                case BindingInitialSyncDirection.TargetToSource:
                    state.UpdateSource(targetGetter(target));
                    break;
                case BindingInitialSyncDirection.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(options.InitialSyncDirection));
            }

            result.SetSubscription(subscriptions);
        }
        catch (Exception exception)
        {
            subscriptions.Dispose();
            result.Fail(exception);
        }

        return result;
    }

    private static void ValidateTwoWayArguments<TSource, TTarget, TSourceValue, TTargetValue>(
        TSource source,
        TTarget target,
        Expression<Func<TSource, TSourceValue>> sourceProperty,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        TwoWayBindingOptions<TSourceValue, TTargetValue> options
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SourceComparer);
        ArgumentNullException.ThrowIfNull(options.TargetComparer);
        ArgumentNullException.ThrowIfNull(options.Scheduler);
    }

    private sealed class TwoWayBindingState<TSourceValue, TTargetValue>(
        BindingResult result,
        IObservableScheduler scheduler,
        Action<TSourceValue> assignTarget,
        Action<TTargetValue> assignSource
    )
    {
        private const int Idle = 0;
        private const int UpdatingTarget = 1;
        private const int UpdatingSource = 2;
        private int _state;

        public void UpdateTarget(TSourceValue value) =>
            Schedule(value, UpdatingTarget, assignTarget, true);

        public void UpdateSource(TTargetValue value) =>
            Schedule(value, UpdatingSource, assignSource, false);

        private void Schedule<TValue>(
            TValue value,
            int direction,
            Action<TValue> assignment,
            bool sourceWins
        )
        {
            if (result.IsStopped)
            {
                return;
            }

            try
            {
                scheduler.Schedule(() =>
                {
                    if (result.IsStopped)
                    {
                        return;
                    }

                    int previous;
                    if (sourceWins)
                    {
                        do
                        {
                            previous = Volatile.Read(ref _state);
                            if (previous == UpdatingTarget)
                            {
                                return;
                            }
                        } while (
                            Interlocked.CompareExchange(ref _state, direction, previous) != previous
                        );
                    }
                    else if (Interlocked.CompareExchange(ref _state, direction, Idle) != Idle)
                    {
                        return;
                    }

                    try
                    {
                        assignment(value);
                    }
                    catch (Exception exception)
                    {
                        result.Fail(exception);
                    }
                    finally
                    {
                        Interlocked.CompareExchange(ref _state, Idle, direction);
                    }
                });
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }
        }
    }
}
