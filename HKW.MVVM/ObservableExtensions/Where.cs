namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 使用谓词筛选可观察序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要筛选的可观察序列</param>
    /// <param name="predicate">用于确定是否发出某个值的函数</param>
    /// <returns>仅包含谓词接受的值的可观察序列</returns>
    public static IObservable<TSource> Where<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return PreserveLogger(source, new WhereObservable<TSource>(source, predicate));
    }
}
