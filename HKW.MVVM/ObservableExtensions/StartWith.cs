namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在可观察序列前插入一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要置于其前的可观察序列</param>
    /// <param name="value">在订阅源之前发出的值</param>
    /// <returns>以 <paramref name="value"/> 开头的可观察序列</returns>
    public static IObservable<TSource> StartWith<TSource>(
        this IObservable<TSource> source,
        TSource value
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return CreateCore(
            source,
            observer =>
            {
                observer.OnNext(value);
                return source.Subscribe(observer);
            }
        );
    }
}
