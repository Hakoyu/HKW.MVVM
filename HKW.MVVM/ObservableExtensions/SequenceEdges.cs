namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在源序列前添加一个元素
    /// </summary>
    public static IObservable<TSource> Prepend<TSource>(
        this IObservable<TSource> source,
        TSource value
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.StartWith(value);
    }

    /// <summary>
    /// 在源序列成功完成后添加一个元素
    /// </summary>
    public static IObservable<TSource> Append<TSource>(
        this IObservable<TSource> source,
        TSource value
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Concat(Return(value));
    }
}
