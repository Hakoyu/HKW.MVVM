namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 当源保持静默的时间超过 <paramref name="dueTime"/> 时失败
    /// </summary>
    public static IObservable<TSource> Timeout<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime
    ) => Timeout(source, dueTime, TimeProvider.System);

    /// <summary>
    /// 使用提供的时间提供程序, 在源保持静默时失败
    /// </summary>
    public static IObservable<TSource> Timeout<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        TimeProvider timeProvider
    )
    {
        ValidateTimeArguments(source, dueTime, timeProvider);
        return CreateCore(
            source,
            observer => new TimeoutSubscription<TSource>(
                source,
                observer,
                dueTime,
                null,
                timeProvider
            )
        );
    }

    /// <summary>
    /// 当源保持静默时切换到 <paramref name="fallback"/>
    /// </summary>
    public static IObservable<TSource> Timeout<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        IObservable<TSource> fallback
    ) => Timeout(source, dueTime, fallback, TimeProvider.System);

    /// <summary>
    /// 使用提供的时间提供程序, 当源保持静默时切换到 <paramref name="fallback"/>
    /// </summary>
    public static IObservable<TSource> Timeout<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        IObservable<TSource> fallback,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ValidateTimeArguments(source, dueTime, timeProvider);
        return CreateCore(
            source,
            observer => new TimeoutSubscription<TSource>(
                source,
                observer,
                dueTime,
                fallback,
                timeProvider
            )
        );
    }
}
