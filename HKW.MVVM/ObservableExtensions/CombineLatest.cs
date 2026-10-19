namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 组合两个源的最新值
    /// </summary>
    public static IObservable<(TFirst First, TSecond Second)> CombineLatest<TFirst, TSecond>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second
    ) =>
        CombineLatest(first, second, static (firstValue, secondValue) => (firstValue, secondValue));

    /// <summary>
    /// 使用结果选择器组合两个源
    /// </summary>
    public static IObservable<TResult> CombineLatest<TFirst, TSecond, TResult>(
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
            new AnonymousObservable<TResult>(observer =>
            {
                var subscription = new CombineLatestSubscription<TResult>(
                    observer,
                    2,
                    values => selector((TFirst)values[0]!, (TSecond)values[1]!)
                );
                subscription.Subscribe(0, first);
                subscription.Subscribe(1, second);
                return subscription;
            })
        );
    }

    /// <summary>
    /// 组合三个源的最新值
    /// </summary>
    public static IObservable<(TFirst First, TSecond Second, TThird Third)> CombineLatest<
        TFirst,
        TSecond,
        TThird
    >(this IObservable<TFirst> first, IObservable<TSecond> second, IObservable<TThird> third) =>
        CombineLatest(first, second, third, static (a, b, c) => (a, b, c));

    /// <summary>
    /// 使用结果选择器组合三个源
    /// </summary>
    public static IObservable<TResult> CombineLatest<TFirst, TSecond, TThird, TResult>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second,
        IObservable<TThird> third,
        Func<TFirst, TSecond, TThird, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(third);
        ArgumentNullException.ThrowIfNull(selector);
        return PreserveLogger(
            first,
            new AnonymousObservable<TResult>(observer =>
            {
                var subscription = new CombineLatestSubscription<TResult>(
                    observer,
                    3,
                    values => selector((TFirst)values[0]!, (TSecond)values[1]!, (TThird)values[2]!)
                );
                subscription.Subscribe(0, first);
                subscription.Subscribe(1, second);
                subscription.Subscribe(2, third);
                return subscription;
            })
        );
    }

    /// <summary>
    /// 组合四个源的最新值
    /// </summary>
    public static IObservable<(
        TFirst First,
        TSecond Second,
        TThird Third,
        TFourth Fourth
    )> CombineLatest<TFirst, TSecond, TThird, TFourth>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second,
        IObservable<TThird> third,
        IObservable<TFourth> fourth
    ) => CombineLatest(first, second, third, fourth, static (a, b, c, d) => (a, b, c, d));

    /// <summary>使用结果选择器组合四个源</summary>
    public static IObservable<TResult> CombineLatest<TFirst, TSecond, TThird, TFourth, TResult>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second,
        IObservable<TThird> third,
        IObservable<TFourth> fourth,
        Func<TFirst, TSecond, TThird, TFourth, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(third);
        ArgumentNullException.ThrowIfNull(fourth);
        ArgumentNullException.ThrowIfNull(selector);
        return PreserveLogger(
            first,
            new AnonymousObservable<TResult>(observer =>
            {
                var subscription = new CombineLatestSubscription<TResult>(
                    observer,
                    4,
                    values =>
                        selector(
                            (TFirst)values[0]!,
                            (TSecond)values[1]!,
                            (TThird)values[2]!,
                            (TFourth)values[3]!
                        )
                );
                subscription.Subscribe(0, first);
                subscription.Subscribe(1, second);
                subscription.Subscribe(2, third);
                subscription.Subscribe(3, fourth);
                return subscription;
            })
        );
    }
}
