namespace HKW.MVVM;

internal abstract class OperatorObserver<TSource, TResult>(IObserver<TResult> observer)
    : IObserver<TSource>,
        IDisposable
{
    private static readonly IDisposable DisposedSubscription = new DisposedDisposable();
    private IDisposable? _subscription;

    protected IObserver<TResult> Observer { get; } = observer;

    protected bool IsStopped =>
        ReferenceEquals(Volatile.Read(ref _subscription), DisposedSubscription);

    public abstract void OnNext(TSource value);

    public void OnError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var subscription = Interlocked.Exchange(ref _subscription, DisposedSubscription);
        if (ReferenceEquals(subscription, DisposedSubscription))
        {
            return;
        }

        try
        {
            Observer.OnError(error);
        }
        finally
        {
            subscription?.Dispose();
        }
    }

    public void OnCompleted()
    {
        var subscription = Interlocked.Exchange(ref _subscription, DisposedSubscription);
        if (ReferenceEquals(subscription, DisposedSubscription))
        {
            return;
        }

        try
        {
            Observer.OnCompleted();
        }
        finally
        {
            subscription?.Dispose();
        }
    }

    public void Dispose() => DisposeSubscription();

    internal void SetSubscription(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var current = Interlocked.CompareExchange(ref _subscription, subscription, null);
        if (ReferenceEquals(current, DisposedSubscription))
        {
            subscription.Dispose();
            return;
        }

        if (current is not null)
        {
            subscription.Dispose();
            throw new InvalidOperationException("The upstream subscription has already been assigned.");
        }
    }

    protected void Fail(Exception error) => OnError(error);

    private void DisposeSubscription()
    {
        var subscription = Interlocked.Exchange(ref _subscription, DisposedSubscription);
        if (subscription is not null && ReferenceEquals(subscription, DisposedSubscription) is false)
        {
            subscription.Dispose();
        }
    }

    private sealed class DisposedDisposable : IDisposable
    {
        public void Dispose() { }
    }
}

internal sealed class SelectObservable<TSource, TResult>(
    IObservable<TSource> source,
    Func<TSource, TResult> selector
) : IObservable<TResult>
{
    public IDisposable Subscribe(IObserver<TResult> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new SelectObserver<TSource, TResult>(observer, selector);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class SelectObserver<TSource, TResult>(
    IObserver<TResult> observer,
    Func<TSource, TResult> selector
) : OperatorObserver<TSource, TResult>(observer)
{
    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        TResult result;
        try
        {
            result = selector(value);
        }
        catch (Exception error)
        {
            Fail(error);
            return;
        }

        Observer.OnNext(result);
    }
}

internal sealed class WhereObservable<TSource>(
    IObservable<TSource> source,
    Func<TSource, bool> predicate
) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new WhereObserver<TSource>(observer, predicate);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class WhereObserver<TSource>(IObserver<TSource> observer, Func<TSource, bool> predicate)
    : OperatorObserver<TSource, TSource>(observer)
{
    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        bool shouldEmit;
        try
        {
            shouldEmit = predicate(value);
        }
        catch (Exception error)
        {
            Fail(error);
            return;
        }

        if (shouldEmit)
        {
            Observer.OnNext(value);
        }
    }
}

internal sealed class DistinctUntilChangedObservable<TSource>(
    IObservable<TSource> source,
    IEqualityComparer<TSource> comparer
) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new DistinctUntilChangedObserver<TSource>(observer, comparer);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class DistinctUntilChangedObserver<TSource>(
    IObserver<TSource> observer,
    IEqualityComparer<TSource> comparer
) : OperatorObserver<TSource, TSource>(observer)
{
    private bool _hasValue;
    private TSource? _lastValue;

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        bool equals;
        try
        {
            equals = _hasValue && comparer.Equals(_lastValue!, value);
        }
        catch (Exception error)
        {
            Fail(error);
            return;
        }

        if (equals)
        {
            return;
        }

        _hasValue = true;
        _lastValue = value;
        Observer.OnNext(value);
    }
}