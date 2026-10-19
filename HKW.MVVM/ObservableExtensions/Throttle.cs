namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 仅当源在指定时长内保持静默后,才发出最近的一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要限流的可观察序列</param>
    /// <param name="dueTime">所需的静默时长</param>
    /// <returns>使用 <see cref="TimeProvider.System"/> 的限流可观察序列</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime
    ) => Throttle(source, dueTime, TimeProvider.System);

    /// <summary>
    /// 仅当源在指定时长内保持静默后,才发出最近的一个值,
    /// 并通过选定的调度器派发通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要限流的可观察序列</param>
    /// <param name="dueTime">所需的静默时长</param>
    /// <param name="scheduler">用于限流通知的调度器</param>
    /// <returns>通知使用选定调度器的限流可观察序列</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        IObservableScheduler scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        return Throttle(source, dueTime, TimeProvider.System).ObserveOn(scheduler);
    }

    /// <summary>
    /// 使用提供的时间提供程序,仅当源在指定时长内保持静默后,才发出最近的一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要限流的可观察序列</param>
    /// <param name="dueTime">所需的静默时长</param>
    /// <param name="timeProvider">用于创建计时器的时间提供程序</param>
    /// <returns>限流的可观察序列</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);

        return CreateCore(
            source,
            observer => new ThrottleSubscription<TSource>(source, observer, dueTime, timeProvider)
        );
    }
}
