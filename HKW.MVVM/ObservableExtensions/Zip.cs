namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 按索引配对两个可观察序列的值
    /// </summary>
    /// <typeparam name="TFirst">第一个序列的值类型</typeparam>
    /// <typeparam name="TSecond">第二个序列的值类型</typeparam>
    /// <param name="first">第一个可观察序列</param>
    /// <param name="second">第二个可观察序列</param>
    /// <returns>按到达顺序配对的元组序列</returns>
    public static IObservable<(TFirst First, TSecond Second)> Zip<TFirst, TSecond>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second
    ) => Zip(first, second, static (firstValue, secondValue) => (firstValue, secondValue));

    /// <summary>
    /// 使用结果选择器按索引配对两个可观察序列的值
    /// </summary>
    /// <typeparam name="TFirst">第一个序列的值类型</typeparam>
    /// <typeparam name="TSecond">第二个序列的值类型</typeparam>
    /// <typeparam name="TResult">结果值类型</typeparam>
    /// <param name="first">第一个可观察序列</param>
    /// <param name="second">第二个可观察序列</param>
    /// <param name="selector">根据一对值生成结果的函数</param>
    /// <returns>选择器生成的可观察序列</returns>
    public static IObservable<TResult> Zip<TFirst, TSecond, TResult>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second,
        Func<TFirst, TSecond, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(selector);

        return PreserveLogger(
            first,
            CreateCore<TResult>(observer =>
            {
                var subscription = new ZipSubscription<TFirst, TSecond, TResult>(
                    observer,
                    selector
                );
                subscription.Subscribe(first, second);
                return subscription;
            })
        );
    }
}
