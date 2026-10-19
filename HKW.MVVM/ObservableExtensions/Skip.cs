namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 跳过指定数量的源值,然后发出其余值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要跳过其值的可观察序列</param>
    /// <param name="count">要跳过的起始值数量</param>
    /// <returns>包含跳过前缀之后剩余值的可观察序列</returns>
    public static IObservable<TSource> Skip<TSource>(this IObservable<TSource> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return CreateCore(
            source,
            observer =>
            {
                var remaining = count;
                return source.Subscribe(
                    value =>
                    {
                        if (remaining > 0)
                        {
                            remaining--;
                        }
                        else
                        {
                            observer.OnNext(value);
                        }
                    },
                    observer.OnError,
                    observer.OnCompleted
                );
            }
        );
    }
}
