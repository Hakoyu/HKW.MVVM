namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 无限重复源序列, 直到订阅被释放或源出错
    /// </summary>
    public static IObservable<TSource> Repeat<TSource>(this IObservable<TSource> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(
            source,
            ConcatCore(RepeatForever(source), yieldOnSynchronousCompletion: true)
        );
    }

    /// <summary>
    /// 重复源序列指定次数
    /// </summary>
    public static IObservable<TSource> Repeat<TSource>(
        this IObservable<TSource> source,
        int repeatCount
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(repeatCount);
        return PreserveLogger(source, ConcatCore(Enumerable.Repeat(source, repeatCount)));
    }

    private static IEnumerable<IObservable<TSource>> RepeatForever<TSource>(
        IObservable<TSource> source
    )
    {
        while (true)
        {
            yield return source;
        }
    }
}
