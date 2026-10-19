namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 使用默认相等比较器抑制连续的重复值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要比较相邻值的可观察序列</param>
    /// <returns>不含连续重复值的可观察序列</returns>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source
    ) => DistinctUntilChanged(source, EqualityComparer<TSource>.Default);

    /// <summary>
    /// 使用指定的相等比较器抑制连续的重复值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要比较相邻值的可观察序列</param>
    /// <param name="comparer">用于判定相邻值是否相等的比较器</param>
    /// <returns>不含连续重复值的可观察序列</returns>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source,
        IEqualityComparer<TSource> comparer
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);
        return PreserveLogger(
            source,
            new DistinctUntilChangedObservable<TSource>(source, comparer)
        );
    }
}
