namespace HKW.MVVM;

internal sealed class ScanObservable<TSource, TAccumulate> : IObservable<TAccumulate>
{
    private readonly IObservable<TSource> _source;
    private readonly Func<TAccumulate, TSource, TAccumulate> _accumulator;
    private readonly TAccumulate? _seed;
    private readonly bool _hasSeed;

    public ScanObservable(
        IObservable<TSource> source,
        Func<TAccumulate, TSource, TAccumulate> accumulator
    )
    {
        _source = source;
        _accumulator = accumulator;
    }

    public ScanObservable(
        IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> accumulator
    )
    {
        _source = source;
        _seed = seed;
        _accumulator = accumulator;
        _hasSeed = true;
    }

    public IDisposable Subscribe(IObserver<TAccumulate> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var subscription = new ScanSubscription<TSource, TAccumulate>(
            observer,
            _accumulator,
            _seed,
            _hasSeed
        );
        subscription.Start(_source);
        return subscription;
    }
}

internal sealed class ScanSubscription<TSource, TAccumulate> : IObserver<TSource>, IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly IObserver<TAccumulate> _observer;
    private readonly Func<TAccumulate, TSource, TAccumulate> _accumulator;
    private readonly SingleAssignmentDisposable _upstream = new();
    private TAccumulate? _value;
    private bool _hasValue;
    private bool _stopped;

    public ScanSubscription(
        IObserver<TAccumulate> observer,
        Func<TAccumulate, TSource, TAccumulate> accumulator,
        TAccumulate? seed,
        bool hasSeed
    )
    {
        _observer = observer;
        _accumulator = accumulator;
        _value = seed;
        _hasValue = hasSeed;
    }

    public void Start(IObservable<TSource> source)
    {
        try
        {
            _upstream.Disposable =
                source.Subscribe(this)
                ?? throw new InvalidOperationException("The source returned a null subscription.");
        }
        catch (Exception error)
        {
            OnError(error);
        }
    }

    public void OnNext(TSource value)
    {
        Exception? failure = null;
        try
        {
            lock (_gate)
            {
                if (_stopped)
                    return;

                try
                {
                    if (_hasValue)
                    {
                        _value = _accumulator(_value!, value);
                    }
                    else
                    {
                        _value = (TAccumulate)(object?)value!;
                        _hasValue = true;
                    }
                }
                catch (Exception error)
                {
                    _stopped = true;
                    failure = error;
                }

                if (failure is null)
                    _observer.OnNext(_value!);
                else
                    _observer.OnError(failure);
            }
        }
        finally
        {
            if (failure is not null)
                _upstream.Dispose();
        }
    }

    public void OnError(Exception error) => Terminate(error);

    public void OnCompleted() => Terminate(null);

    private void Terminate(Exception? error)
    {
        var terminate = false;
        try
        {
            lock (_gate)
            {
                if (_stopped)
                    return;
                _stopped = true;
                terminate = true;
                if (error is null)
                    _observer.OnCompleted();
                else
                    _observer.OnError(error);
            }
        }
        finally
        {
            if (terminate)
                _upstream.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
            _stopped = true;
        _upstream.Dispose();
    }
}

internal sealed class RetryObservable<T>(
    IObservable<T> source,
    int retryCount,
    Func<Exception, bool> exceptionPredicate,
    TimeSpan initialDelay,
    double backoffFactor,
    TimeProvider timeProvider
) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var subscription = new RetrySubscription<T>(
            source,
            observer,
            retryCount,
            exceptionPredicate,
            initialDelay,
            backoffFactor,
            timeProvider
        );
        subscription.Start();
        return subscription;
    }
}

internal sealed class RetrySubscription<T> : IObserver<T>, IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly IObservable<T> _source;
    private readonly IObserver<T> _observer;
    private readonly Func<Exception, bool> _exceptionPredicate;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffFactor;
    private readonly TimeProvider _timeProvider;
    private readonly SerialDisposable _upstream = new();
    private readonly SerialDisposable _timer = new();
    private int _remaining;
    private int _retryIndex;
    private int _pending;
    private int _draining;
    private bool _stopped;

    public RetrySubscription(
        IObservable<T> source,
        IObserver<T> observer,
        int retryCount,
        Func<Exception, bool> exceptionPredicate,
        TimeSpan initialDelay,
        double backoffFactor,
        TimeProvider timeProvider
    )
    {
        _source = source;
        _observer = observer;
        _remaining = retryCount;
        _exceptionPredicate = exceptionPredicate;
        _initialDelay = initialDelay;
        _backoffFactor = backoffFactor;
        _timeProvider = timeProvider;
    }

    public void Start() => RequestSubscription();

    private void RequestSubscription()
    {
        Interlocked.Increment(ref _pending);
        if (Interlocked.Increment(ref _draining) != 1)
            return;

        do
        {
            while (Interlocked.Exchange(ref _pending, 0) != 0)
            {
                lock (_gate)
                {
                    if (_stopped)
                        break;
                }

                var slot = new SingleAssignmentDisposable();
                _upstream.Disposable = slot;
                try
                {
                    slot.Disposable =
                        _source.Subscribe(this)
                        ?? throw new InvalidOperationException(
                            "The source returned a null subscription."
                        );
                }
                catch (Exception error)
                {
                    OnError(error);
                }
            }
        } while (Interlocked.Decrement(ref _draining) != 0);
    }

    public void OnNext(T value)
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _observer.OnNext(value);
        }
    }

    public void OnError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        lock (_gate)
        {
            if (_stopped)
                return;
        }

        bool accepted;
        try
        {
            accepted = _exceptionPredicate(error);
        }
        catch (Exception predicateError)
        {
            Terminate(predicateError);
            return;
        }

        TimeSpan delay = TimeSpan.Zero;
        var terminate = false;
        lock (_gate)
        {
            if (_stopped)
                return;
            if (!accepted || _remaining == 0)
            {
                _stopped = true;
                terminate = true;
                _observer.OnError(error);
            }
            else
            {
                _remaining--;
                try
                {
                    delay = ComputeDelay(_retryIndex++);
                }
                catch (Exception delayError)
                {
                    _stopped = true;
                    terminate = true;
                    _observer.OnError(delayError);
                }
            }
        }

        if (terminate)
        {
            DisposeResources();
            return;
        }

        if (delay == TimeSpan.Zero)
        {
            RequestSubscription();
            return;
        }

        try
        {
            var timer = _timeProvider.CreateTimer(
                static state => ((RetrySubscription<T>)state!).TimerElapsed(),
                this,
                delay,
                Timeout.InfiniteTimeSpan
            );
            _timer.Disposable = timer;
        }
        catch (Exception timerError)
        {
            Terminate(timerError);
        }
    }

    private TimeSpan ComputeDelay(int retryIndex)
    {
        if (_initialDelay == TimeSpan.Zero || retryIndex == 0)
            return _initialDelay;

        var ticks = _initialDelay.Ticks * Math.Pow(_backoffFactor, retryIndex);
        if (double.IsFinite(ticks) is false || ticks > TimeSpan.MaxValue.Ticks)
            throw new ArgumentOutOfRangeException(
                nameof(_backoffFactor),
                "The retry delay exceeds TimeSpan.MaxValue."
            );
        return TimeSpan.FromTicks((long)ticks);
    }

    private void TimerElapsed()
    {
        lock (_gate)
        {
            if (_stopped)
                return;
        }
        RequestSubscription();
    }

    public void OnCompleted()
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _stopped = true;
            _observer.OnCompleted();
        }
        DisposeResources();
    }

    private void Terminate(Exception error)
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _stopped = true;
            _observer.OnError(error);
        }
        DisposeResources();
    }

    private void DisposeResources()
    {
        _timer.Dispose();
        _upstream.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
            _stopped = true;
        DisposeResources();
    }
}

internal sealed class FinallyObservable<T>(IObservable<T> source, Action finallyAction)
    : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var subscription = new FinallySubscription<T>(observer, finallyAction);
        subscription.Start(source);
        return subscription;
    }
}

internal sealed class FinallySubscription<T> : IObserver<T>, IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly IObserver<T> _observer;
    private readonly Action _finallyAction;
    private readonly SingleAssignmentDisposable _upstream = new();
    private bool _stopped;
    private int _finalized;

    public FinallySubscription(IObserver<T> observer, Action finallyAction)
    {
        _observer = observer;
        _finallyAction = finallyAction;
    }

    public void Start(IObservable<T> source)
    {
        try
        {
            _upstream.Disposable =
                source.Subscribe(this)
                ?? throw new InvalidOperationException("The source returned a null subscription.");
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                if (_stopped)
                    throw;
            }
            Finish(error);
        }
    }

    public void OnNext(T value)
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _observer.OnNext(value);
        }
    }

    public void OnError(Exception error) => Finish(error);

    public void OnCompleted() => Finish(null);

    private void Finish(Exception? error)
    {
        var terminate = false;
        try
        {
            lock (_gate)
            {
                if (_stopped)
                    return;
                _stopped = true;
                terminate = true;
                if (error is null)
                    _observer.OnCompleted();
                else
                    _observer.OnError(error);
            }
        }
        finally
        {
            if (terminate)
            {
                try
                {
                    _upstream.Dispose();
                }
                finally
                {
                    InvokeFinally();
                }
            }
        }
    }

    public void Dispose()
    {
        var dispose = false;
        lock (_gate)
        {
            if (!_stopped)
            {
                _stopped = true;
                dispose = true;
            }
        }
        if (!dispose)
            return;
        try
        {
            _upstream.Dispose();
        }
        finally
        {
            InvokeFinally();
        }
    }

    private void InvokeFinally()
    {
        if (Interlocked.Exchange(ref _finalized, 1) == 0)
            _finallyAction();
    }
}

internal sealed class TakeUntilObservable<TSource, TOther>(
    IObservable<TSource> source,
    IObservable<TOther> other
) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var subscription = new TakeUntilSubscription<TSource, TOther>(observer);
        subscription.Start(source, other);
        return subscription;
    }
}

internal sealed class TakeUntilSubscription<TSource, TOther> : IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly IObserver<TSource> _observer;
    private readonly SingleAssignmentDisposable _sourceSubscription = new();
    private readonly SingleAssignmentDisposable _otherSubscription = new();
    private bool _stopped;

    public TakeUntilSubscription(IObserver<TSource> observer) => _observer = observer;

    public void Start(IObservable<TSource> source, IObservable<TOther> other)
    {
        try
        {
            _otherSubscription.Disposable =
                other.Subscribe(new OtherObserver(this))
                ?? throw new InvalidOperationException(
                    "The other source returned a null subscription."
                );
        }
        catch (Exception error)
        {
            Error(error);
            return;
        }

        lock (_gate)
        {
            if (_stopped)
                return;
        }

        try
        {
            _sourceSubscription.Disposable =
                source.Subscribe(new SourceObserver(this))
                ?? throw new InvalidOperationException("The source returned a null subscription.");
        }
        catch (Exception error)
        {
            Error(error);
        }
    }

    private void Next(TSource value)
    {
        lock (_gate)
        {
            if (!_stopped)
                _observer.OnNext(value);
        }
    }

    private void Error(Exception error) => Terminate(error);

    private void Complete() => Terminate(null);

    private void Terminate(Exception? error)
    {
        var terminate = false;
        try
        {
            lock (_gate)
            {
                if (_stopped)
                    return;
                _stopped = true;
                terminate = true;
                if (error is null)
                    _observer.OnCompleted();
                else
                    _observer.OnError(error);
            }
        }
        finally
        {
            if (terminate)
                DisposeResources();
        }
    }

    private void OtherCompleted() => _otherSubscription.Dispose();

    public void Dispose()
    {
        lock (_gate)
            _stopped = true;
        DisposeResources();
    }

    private void DisposeResources()
    {
        _sourceSubscription.Dispose();
        _otherSubscription.Dispose();
    }

    private sealed class SourceObserver(TakeUntilSubscription<TSource, TOther> owner)
        : IObserver<TSource>
    {
        public void OnNext(TSource value) => owner.Next(value);

        public void OnError(Exception error) => owner.Error(error);

        public void OnCompleted() => owner.Complete();
    }

    private sealed class OtherObserver(TakeUntilSubscription<TSource, TOther> owner)
        : IObserver<TOther>
    {
        public void OnNext(TOther value) => owner.Complete();

        public void OnError(Exception error) => owner.Error(error);

        public void OnCompleted() => owner.OtherCompleted();
    }
}
