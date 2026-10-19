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
        return PreserveLogger(source, new RetryObservable<TSource>(source, retryCount));
    }
}
