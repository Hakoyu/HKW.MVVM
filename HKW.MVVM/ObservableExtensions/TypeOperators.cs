namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 仅发出可赋给指定类型的元素
    /// </summary>
    public static IObservable<TResult> OfType<TResult>(this IObservable<object?> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source
            .Where(static value => value is TResult)
            .Select(static value => (TResult)value!);
    }

    /// <summary>
    /// 将每个元素强制转换为指定类型
    /// </summary>
    public static IObservable<TResult> Cast<TResult>(this IObservable<object?> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Select(static value => (TResult)value!);
    }
}
