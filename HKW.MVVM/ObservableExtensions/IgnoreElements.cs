namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 忽略所有元素, 仅转发终止通知
    /// </summary>
    public static IObservable<TSource> IgnoreElements<TSource>(
        this IObservable<TSource> source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Where(static _ => false);
    }
}
