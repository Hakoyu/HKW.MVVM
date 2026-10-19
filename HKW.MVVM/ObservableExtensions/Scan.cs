namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 使用第一个源值作为初始累积值,并发出每个累积结果
    /// </summary>
    public static IObservable<TSource> Scan<TSource>(
        this IObservable<TSource> source,
        Func<TSource, TSource, TSource> accumulator
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accumulator);
        return PreserveLogger(source, new ScanObservable<TSource, TSource>(source, accumulator));
    }

    /// <summary>
    /// 从指定种子开始累积源值,并发出每个累积结果
    /// </summary>
    public static IObservable<TAccumulate> Scan<TSource, TAccumulate>(
        this IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> accumulator
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accumulator);
        return PreserveLogger(
            source,
            new ScanObservable<TSource, TAccumulate>(source, seed, accumulator)
        );
    }
}
