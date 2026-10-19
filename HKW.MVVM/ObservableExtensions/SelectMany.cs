namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将源的每个值投影为可观察序列，并发合并结果
    /// </summary>
    public static IObservable<TResult> SelectMany<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, IObservable<TResult>> selector
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return PreserveLogger(
            source,
            new AnonymousObservable<TResult>(observer =>
            {
                var subscription = new SelectManySubscription<TSource, TResult>(observer, selector);
                subscription.Subscribe(source);
                return subscription;
            })
        );
    }
}
