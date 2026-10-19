namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 跳过序列最后指定数量的元素
    /// </summary>
    public static IObservable<TSource> SkipLast<TSource>(
        this IObservable<TSource> source,
        int count
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return PreserveLogger(source, new SkipLastObservable<TSource>(source, count));
    }
}

internal sealed class SkipLastObservable<TSource>(IObservable<TSource> source, int count)
    : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new SkipLastObserver<TSource>(observer, count);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class SkipLastObserver<TSource>(IObserver<TSource> observer, int count)
    : OperatorObserver<TSource, TSource>(observer)
{
    private readonly Queue<TSource> _values = new(count);

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        if (count == 0)
        {
            Observer.OnNext(value);
            return;
        }

        _values.Enqueue(value);
        if (_values.Count > count)
        {
            Observer.OnNext(_values.Dequeue());
        }
    }
}
