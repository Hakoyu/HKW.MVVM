namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>并发合并两个可观察序列</summary>
    public static IObservable<T> Merge<T>(this IObservable<T> first, IObservable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return PreserveLogger(first, MergeCore(new[] { first, second }));
    }

    /// <summary>并发合并一组可观察序列</summary>
    public static IObservable<T> Merge<T>(this IEnumerable<IObservable<T>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return MergeCore(sources);
    }

    private static IObservable<T> MergeCore<T>(IEnumerable<IObservable<T>> sources) =>
        new AnonymousObservable<T>(observer =>
        {
            var subscription = new MergeSubscription<T>(observer);
            subscription.Run(sources);
            return subscription;
        });
}
