namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将源序列的每个值与另一个序列最近发出的值组合
    /// </summary>
    /// <typeparam name="TSource">源序列的值类型</typeparam>
    /// <typeparam name="TOther">另一个序列的值类型</typeparam>
    /// <param name="source">触发结果的源序列</param>
    /// <param name="other">提供最近值的序列</param>
    /// <returns>包含源值和另一个序列最近值的可观察序列</returns>
    /// <remarks>在 <paramref name="other"/> 发出首个值之前,源序列的值会被忽略</remarks>
    public static IObservable<(TSource Source, TOther Latest)> WithLatestFrom<TSource, TOther>(
        this IObservable<TSource> source,
        IObservable<TOther> other
    ) => WithLatestFrom(source, other, static (sourceValue, latestValue) => (sourceValue, latestValue));

    /// <summary>
    /// 使用结果选择器将源序列的每个值与另一个序列最近发出的值组合
    /// </summary>
    /// <typeparam name="TSource">源序列的值类型</typeparam>
    /// <typeparam name="TOther">另一个序列的值类型</typeparam>
    /// <typeparam name="TResult">结果值类型</typeparam>
    /// <param name="source">触发结果的源序列</param>
    /// <param name="other">提供最近值的序列</param>
    /// <param name="selector">根据源值和最近值生成结果的函数</param>
    /// <returns>选择器生成的可观察序列</returns>
    /// <remarks>在 <paramref name="other"/> 发出首个值之前,源序列的值会被忽略</remarks>
    public static IObservable<TResult> WithLatestFrom<TSource, TOther, TResult>(
        this IObservable<TSource> source,
        IObservable<TOther> other,
        Func<TSource, TOther, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(selector);

        return PreserveLogger(
            source,
            CreateCore<TResult>(observer =>
            {
                var subscription = new WithLatestFromSubscription<TSource, TOther, TResult>(
                    observer,
                    selector
                );
                subscription.Subscribe(source, other);
                return subscription;
            })
        );
    }
}
