namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在源完成时发出最后指定数量的元素
    /// </summary>
    public static IObservable<TSource> TakeLast<TSource>(
        this IObservable<TSource> source,
        int count
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return PreserveLogger(source, new TakeLastObservable<TSource>(source, count));
    }
}

internal sealed class TakeLastObservable<TSource>(IObservable<TSource> source, int count)
    : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new TakeLastObserver<TSource>(observer, count);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class TakeLastObserver<TSource>(IObserver<TSource> observer, int count)
    : OperatorObserver<TSource, TSource>(observer)
{
    private readonly Queue<TSource> _values = new(count);

    public override void OnNext(TSource value)
    {
        if (IsStopped || count == 0)
        {
            return;
        }

        if (_values.Count == count)
        {
            _values.Dequeue();
        }

        _values.Enqueue(value);
    }

    public override void OnCompleted()
    {
        if (IsStopped)
        {
            return;
        }

        while (_values.TryDequeue(out var value))
        {
            Observer.OnNext(value);
        }

        base.OnCompleted();
    }
}
