namespace HKW.MVVM;

internal sealed class WithLatestFromSubscription<TSource, TOther, TResult> : IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly IObserver<TResult> _observer;
    private readonly Func<TSource, TOther, TResult> _selector;
    private readonly MultipleDisposable _subscriptions = new();
    private TOther? _latest;
    private bool _hasLatest;
    private bool _stopped;

    public WithLatestFromSubscription(
        IObserver<TResult> observer,
        Func<TSource, TOther, TResult> selector
    )
    {
        _observer = observer;
        _selector = selector;
    }

    public void Subscribe(IObservable<TSource> source, IObservable<TOther> other)
    {
        SubscribeOther(other);
        if (IsStopped)
        {
            return;
        }

        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try
        {
            slot.Disposable = source.Subscribe(Next, Fail, Complete);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private bool IsStopped
    {
        get
        {
            lock (_gate)
            {
                return _stopped;
            }
        }
    }

    private void SubscribeOther(IObservable<TOther> other)
    {
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try
        {
            slot.Disposable = other.Subscribe(
                value =>
                {
                    lock (_gate)
                    {
                        if (_stopped)
                        {
                            return;
                        }

                        _latest = value;
                        _hasLatest = true;
                    }
                },
                Fail,
                () =>
                {
                    _subscriptions.Remove(slot);
                    slot.Dispose();
                }
            );
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void Next(TSource value)
    {
        Exception? failure = null;
        lock (_gate)
        {
            if (_stopped || !_hasLatest)
            {
                return;
            }

            try
            {
                _observer.OnNext(_selector(value, _latest!));
            }
            catch (Exception exception)
            {
                _stopped = true;
                failure = exception;
            }
        }

        if (failure is not null)
        {
            _observer.OnError(failure);
            _subscriptions.Dispose();
        }
    }

    private void Complete()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
        }

        _observer.OnCompleted();
        _subscriptions.Dispose();
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
        }

        _observer.OnError(error);
        _subscriptions.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _stopped = true;
        }

        _subscriptions.Dispose();
    }
}

internal sealed class ZipSubscription<TFirst, TSecond, TResult> : IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly IObserver<TResult> _observer;
    private readonly Func<TFirst, TSecond, TResult> _selector;
    private readonly Queue<TFirst> _firstValues = [];
    private readonly Queue<TSecond> _secondValues = [];
    private readonly MultipleDisposable _subscriptions = new();
    private bool _firstCompleted;
    private bool _secondCompleted;
    private bool _stopped;

    public ZipSubscription(IObserver<TResult> observer, Func<TFirst, TSecond, TResult> selector)
    {
        _observer = observer;
        _selector = selector;
    }

    public void Subscribe(IObservable<TFirst> first, IObservable<TSecond> second)
    {
        SubscribeFirst(first);
        if (IsStopped)
        {
            return;
        }

        SubscribeSecond(second);
    }

    private bool IsStopped
    {
        get
        {
            lock (_gate)
            {
                return _stopped;
            }
        }
    }

    private void SubscribeFirst(IObservable<TFirst> source)
    {
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try
        {
            slot.Disposable = source.Subscribe(FirstNext, Fail, () => FirstCompleted(slot));
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void SubscribeSecond(IObservable<TSecond> source)
    {
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try
        {
            slot.Disposable = source.Subscribe(SecondNext, Fail, () => SecondCompleted(slot));
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void FirstNext(TFirst value)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _firstValues.Enqueue(value);
        }

        Drain();
    }

    private void SecondNext(TSecond value)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _secondValues.Enqueue(value);
        }

        Drain();
    }

    private void Drain()
    {
        Exception? failure = null;
        var complete = false;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            if (_firstValues.Count > 0 && _secondValues.Count > 0)
            {
                try
                {
                    _observer.OnNext(_selector(_firstValues.Dequeue(), _secondValues.Dequeue()));
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }

            complete =
                failure is not null
                || (_firstCompleted && _firstValues.Count == 0)
                || (_secondCompleted && _secondValues.Count == 0);
            if (complete)
            {
                _stopped = true;
            }
        }

        if (!complete)
        {
            return;
        }

        if (failure is null)
        {
            _observer.OnCompleted();
        }
        else
        {
            _observer.OnError(failure);
        }

        _subscriptions.Dispose();
    }

    private void FirstCompleted(SingleAssignmentDisposable slot)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _firstCompleted = true;
        }

        _subscriptions.Remove(slot);
        slot.Dispose();
        Drain();
    }

    private void SecondCompleted(SingleAssignmentDisposable slot)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _secondCompleted = true;
        }

        _subscriptions.Remove(slot);
        slot.Dispose();
        Drain();
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
        }

        _observer.OnError(error);
        _subscriptions.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _stopped = true;
            _firstValues.Clear();
            _secondValues.Clear();
        }

        _subscriptions.Dispose();
    }
}
