namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在另一序列发出首个值时完成;另一序列的完成被忽略,错误则终止结果序列
    /// </summary>
    public static IObservable<TSource> TakeUntil<TSource, TOther>(
        this IObservable<TSource> source,
        IObservable<TOther> other
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(other);
        return PreserveLogger(source, new TakeUntilObservable<TSource, TOther>(source, other));
    }
}
