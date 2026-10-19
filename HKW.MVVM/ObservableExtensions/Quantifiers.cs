namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 确定序列是否包含任何元素
    /// </summary>
    public static IObservable<bool> Any<TSource>(this IObservable<TSource> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Take(1).Select(static _ => true).DefaultIfEmpty(false);
    }

    /// <summary>
    /// 确定序列是否包含满足条件的元素
    /// </summary>
    public static IObservable<bool> Any<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).Any();
    }

    /// <summary>
    /// 确定序列的所有元素是否都满足条件
    /// </summary>
    public static IObservable<bool> All<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Any(value => !predicate(value)).Select(static any => !any);
    }

    /// <summary>
    /// 使用默认相等比较器确定序列是否包含指定值
    /// </summary>
    public static IObservable<bool> Contains<TSource>(
        this IObservable<TSource> source,
        TSource value
    ) => Contains(source, value, EqualityComparer<TSource>.Default);

    /// <summary>
    /// 使用指定比较器确定序列是否包含指定值
    /// </summary>
    public static IObservable<bool> Contains<TSource>(
        this IObservable<TSource> source,
        TSource value,
        IEqualityComparer<TSource> comparer
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);
        return source.Any(item => comparer.Equals(item, value));
    }
}
