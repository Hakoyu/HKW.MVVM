namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 按顺序串联两个可观察序列
    /// </summary>
    public static IObservable<T> Concat<T>(this IObservable<T> first, IObservable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return PreserveLogger(first, ConcatCore(new[] { first, second }));
    }

    /// <summary>
    /// 按顺序串联一组可观察序列
    /// </summary>
    public static IObservable<T> Concat<T>(this IEnumerable<IObservable<T>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return ConcatCore(sources);
    }

    private static IObservable<T> ConcatCore<T>(IEnumerable<IObservable<T>> sources) =>
        new AnonymousObservable<T>(observer =>
        {
            var subscription = new ConcatSubscription<T>(observer, sources);
            subscription.Run();
            return subscription;
        });
}
