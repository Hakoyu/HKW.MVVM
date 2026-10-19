namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将每个源值投影为新形式
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <typeparam name="TResult">投影后的值类型</typeparam>
    /// <param name="source">要转换的可观察序列</param>
    /// <param name="selector">应用于每个值的投影</param>
    /// <returns>包含投影值的可观察序列</returns>
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return PreserveLogger(source, new SelectObservable<TSource, TResult>(source, selector));
    }
}
