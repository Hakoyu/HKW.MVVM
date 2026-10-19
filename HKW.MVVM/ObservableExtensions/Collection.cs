namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将序列收集到新的列表中
    /// </summary>
    public static IObservable<IList<TSource>> ToList<TSource>(
        this IObservable<TSource> source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(source, new ToListObservable<TSource>(source));
    }

    /// <summary>
    /// 为空序列发出类型默认值
    /// </summary>
    public static IObservable<TSource?> DefaultIfEmpty<TSource>(
        this IObservable<TSource> source
    ) => DefaultIfEmpty(source, default);

    /// <summary>
    /// 为空序列发出指定默认值
    /// </summary>
    public static IObservable<TSource> DefaultIfEmpty<TSource>(
        this IObservable<TSource> source,
        TSource defaultValue
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(source, new DefaultIfEmptyObservable<TSource>(source, defaultValue));
    }
}

internal sealed class ToListObservable<TSource>(IObservable<TSource> source)
    : IObservable<IList<TSource>>
{
    public IDisposable Subscribe(IObserver<IList<TSource>> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new ToListObserver<TSource>(observer);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class ToListObserver<TSource>(IObserver<IList<TSource>> observer)
    : OperatorObserver<TSource, IList<TSource>>(observer)
{
    private readonly List<TSource> _values = [];

    public override void OnNext(TSource value)
    {
        if (!IsStopped)
        {
            _values.Add(value);
        }
    }

    public override void OnCompleted()
    {
        if (!IsStopped)
        {
            Observer.OnNext(_values);
            base.OnCompleted();
        }
    }
}

internal sealed class DefaultIfEmptyObservable<TSource>(
    IObservable<TSource> source,
    TSource defaultValue
) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new DefaultIfEmptyObserver<TSource>(observer, defaultValue);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class DefaultIfEmptyObserver<TSource>(
    IObserver<TSource> observer,
    TSource defaultValue
) : OperatorObserver<TSource, TSource>(observer)
{
    private bool _hasValue;

    public override void OnNext(TSource value)
    {
        if (!IsStopped)
        {
            _hasValue = true;
            Observer.OnNext(value);
        }
    }

    public override void OnCompleted()
    {
        if (!IsStopped)
        {
            if (!_hasValue)
            {
                Observer.OnNext(defaultValue);
            }

            base.OnCompleted();
        }
    }
}
