using System.Linq.Expressions;

namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 使用调用方提供的赋值操作绑定可观察序列产生的值
    /// </summary>
    public static BindingResult BindTo<TValue, TTarget>(
        this IObservable<TValue> source,
        TTarget target,
        Action<TValue, TTarget> assignment
    ) => BindTo(source, target, assignment, new BindingOptions<TValue>());

    /// <summary>
    /// 使用选项和调用方提供的赋值操作绑定可观察序列产生的值
    /// </summary>
    public static BindingResult BindTo<TValue, TTarget>(
        this IObservable<TValue> source,
        TTarget target,
        Action<TValue, TTarget> assignment,
        BindingOptions<TValue> options
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(assignment);
        Validate(options);
        return BindCore(source, static value => value, value => assignment(value, target), options);
    }

    /// <summary>
    /// 将可观察序列产生的值绑定到可写的目标属性
    /// </summary>
    public static BindingResult BindTo<TTarget, TValue>(
        this IObservable<TValue> source,
        TTarget target,
        Expression<Func<TTarget, TValue>> targetProperty
    )
        where TTarget : class => BindTo(source, target, targetProperty, new BindingOptions<TValue>());

    /// <summary>
    /// 使用选项将可观察序列产生的值绑定到可写的目标属性
    /// </summary>
    public static BindingResult BindTo<TTarget, TValue>(
        this IObservable<TValue> source,
        TTarget target,
        Expression<Func<TTarget, TValue>> targetProperty,
        BindingOptions<TValue> options
    )
        where TTarget : class => BindTo(source, target, targetProperty, static value => value, options);

    /// <summary>
    /// 将转换后的源值绑定到可写的目标属性
    /// </summary>
    public static BindingResult BindTo<TSourceValue, TTarget, TTargetValue>(
        this IObservable<TSourceValue> source,
        TTarget target,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Func<TSourceValue, TTargetValue> converter
    )
        where TTarget : class =>
        BindTo(source, target, targetProperty, converter, new BindingOptions<TTargetValue>());

    /// <summary>
    /// 使用选项将转换后的源值绑定到可写的目标属性
    /// </summary>
    public static BindingResult BindTo<TSourceValue, TTarget, TTargetValue>(
        this IObservable<TSourceValue> source,
        TTarget target,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Func<TSourceValue, TTargetValue> converter,
        BindingOptions<TTargetValue> options
    )
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(converter);
        Validate(options);

        var setter = PropertySetter.Create(targetProperty);
        return BindCore(source, converter, value => setter(target, value), options);
    }

    private static BindingResult BindCore<TSourceValue, TTargetValue>(
        IObservable<TSourceValue> source,
        Func<TSourceValue, TTargetValue> converter,
        Action<TTargetValue> assignment,
        BindingOptions<TTargetValue> options
    )
    {
        var result = new BindingResult();
        var queue = new SerialActionQueue(options.Scheduler.Schedule);
        var hasValue = false;
        var lastValue = default(TTargetValue)!;

        void Assign(TTargetValue value)
        {
            if (result.IsStopped)
            {
                return;
            }

            try
            {
                if (hasValue && options.Comparer.Equals(lastValue, value))
                {
                    return;
                }

                assignment(value);
                lastValue = value;
                hasValue = true;
            }
            catch (Exception exception)
            {
                if (options.FallbackValue.HasValue)
                {
                    try
                    {
                        assignment(options.FallbackValue.Value);
                    }
                    catch
                    {
                        // The original failure is the binding's diagnostic.
                    }
                }

                result.Fail(exception);
                queue.Dispose();
            }
        }

        try
        {
            var subscription = source.Subscribe(
                value =>
                {
                    try
                    {
                        queue.Enqueue(() =>
                        {
                            try
                            {
                                var targetValue = value is null && options.TargetNullValue.HasValue
                                    ? options.TargetNullValue.Value
                                    : converter(value);
                                Assign(targetValue);
                            }
                            catch (Exception exception)
                            {
                                if (options.FallbackValue.HasValue)
                                {
                                    try
                                    {
                                        assignment(options.FallbackValue.Value);
                                    }
                                    catch
                                    {
                                        // The original conversion failure is the binding's diagnostic.
                                    }
                                }
                                result.Fail(exception);
                            }
                        });
                    }
                    catch (Exception exception)
                    {
                        result.Fail(exception);
                        queue.Dispose();
                    }
                },
                exception =>
                {
                    try
                    {
                        queue.Enqueue(() =>
                        {
                            if (options.FallbackValue.HasValue)
                            {
                                try
                                {
                                    assignment(options.FallbackValue.Value);
                                }
                                catch
                                {
                                    // The source failure remains the binding's diagnostic.
                                }
                            }
                            result.Fail(exception);
                        });
                    }
                    catch (Exception schedulingException)
                    {
                        result.Fail(schedulingException);
                        queue.Dispose();
                    }
                }
            );
            result.SetSubscription(new MultipleDisposable { subscription, queue });
        }
        catch (Exception exception)
        {
            result.Fail(exception);
            queue.Dispose();
        }

        return result;
    }

    private static void Validate<TValue>(BindingOptions<TValue> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Comparer);
        ArgumentNullException.ThrowIfNull(options.Scheduler);
    }
}
