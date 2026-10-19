namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将每个元素与其前一个元素配对
    /// </summary>
    public static IObservable<(TSource Previous, TSource Current)> Pairwise<TSource>(
        this IObservable<TSource> source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(source, new PairwiseObservable<TSource>(source));
    }
}

internal sealed class PairwiseObservable<TSource>(IObservable<TSource> source)
    : IObservable<(TSource Previous, TSource Current)>
{
    public IDisposable Subscribe(IObserver<(TSource Previous, TSource Current)> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new PairwiseObserver<TSource>(observer);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class PairwiseObserver<TSource>(
    IObserver<(TSource Previous, TSource Current)> observer
) : OperatorObserver<TSource, (TSource Previous, TSource Current)>(observer)
{
    private bool _hasPrevious;
    private TSource? _previous;

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        if (_hasPrevious)
        {
            Observer.OnNext((_previous!, value));
        }

        _previous = value;
        _hasPrevious = true;
    }
}
