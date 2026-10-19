namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在初次订阅失败后最多重新订阅指定次数
    /// </summary>
    /// <param name="source">要重新订阅的可观察序列</param>
    /// <param name="retryCount">初次尝试之外允许的额外订阅次数</param>
    public static IObservable<TSource> Retry<TSource>(
        this IObservable<TSource> source,
        int retryCount
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
        return Retry(source, retryCount, static _ => true, TimeSpan.Zero, 1d, TimeProvider.System);
    }

    /// <summary>
    /// 对谓词接受的失败进行重试, 不延迟
    /// </summary>
    public static IObservable<TSource> Retry<TSource>(
        this IObservable<TSource> source,
        int retryCount,
        Func<Exception, bool> exceptionPredicate
    ) => Retry(source, retryCount, exceptionPredicate, TimeSpan.Zero, 1d, TimeProvider.System);

    /// <summary>
    /// 在固定延迟后重试失败
    /// </summary>
    public static IObservable<TSource> Retry<TSource>(
        this IObservable<TSource> source,
        int retryCount,
        TimeSpan delay,
        TimeProvider? timeProvider = null
    ) =>
        Retry(source, retryCount, static _ => true, delay, 1d, timeProvider ?? TimeProvider.System);

    /// <summary>
    /// 在固定延迟后重试谓词接受的失败
    /// </summary>
    public static IObservable<TSource> Retry<TSource>(
        this IObservable<TSource> source,
        int retryCount,
        Func<Exception, bool> exceptionPredicate,
        TimeSpan delay,
        TimeProvider? timeProvider = null
    ) =>
        Retry(
            source,
            retryCount,
            exceptionPredicate,
            delay,
            1d,
            timeProvider ?? TimeProvider.System
        );

    /// <summary>
    /// 使用指数退避重试失败
    /// </summary>
    public static IObservable<TSource> Retry<TSource>(
        this IObservable<TSource> source,
        int retryCount,
        TimeSpan initialDelay,
        double backoffFactor,
        TimeProvider? timeProvider = null
    ) =>
        Retry(
            source,
            retryCount,
            static _ => true,
            initialDelay,
            backoffFactor,
            timeProvider ?? TimeProvider.System
        );

    /// <summary>
    /// 使用指数退避重试谓词接受的失败
    /// </summary>
    public static IObservable<TSource> Retry<TSource>(
        this IObservable<TSource> source,
        int retryCount,
        Func<Exception, bool> exceptionPredicate,
        TimeSpan initialDelay,
        double backoffFactor,
        TimeProvider? timeProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(exceptionPredicate);
        timeProvider ??= TimeProvider.System;
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
        if (initialDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDelay));
        }

        if (double.IsFinite(backoffFactor) is false || backoffFactor < 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(backoffFactor));
        }

        return PreserveLogger(
            source,
            new RetryObservable<TSource>(
                source,
                retryCount,
                exceptionPredicate,
                initialDelay,
                backoffFactor,
                timeProvider
            )
        );
    }
}
