namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 使用默认比较器发出不同的元素
    /// </summary>
    public static IObservable<TSource> Distinct<TSource>(this IObservable<TSource> source) =>
        Distinct(source, EqualityComparer<TSource>.Default);

    /// <summary>
    /// 使用指定比较器发出不同的元素
    /// </summary>
    public static IObservable<TSource> Distinct<TSource>(
        this IObservable<TSource> source,
        IEqualityComparer<TSource> comparer
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);
        return PreserveLogger(source, new DistinctObservable<TSource>(source, comparer));
    }
}

internal sealed class DistinctObservable<TSource>(
    IObservable<TSource> source,
    IEqualityComparer<TSource> comparer
) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new DistinctObserver<TSource>(observer, comparer);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class DistinctObserver<TSource>(
    IObserver<TSource> observer,
    IEqualityComparer<TSource> comparer
) : OperatorObserver<TSource, TSource>(observer)
{
    private readonly HashSet<TSource> _seen = new(comparer);

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        bool added;
        try
        {
            added = _seen.Add(value);
        }
        catch (Exception error)
        {
            Fail(error);
            return;
        }

        if (added)
        {
            Observer.OnNext(value);
        }
    }
}
