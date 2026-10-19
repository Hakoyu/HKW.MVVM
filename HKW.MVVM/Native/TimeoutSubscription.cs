namespace HKW.MVVM;

internal sealed class TimeoutSubscription<TSource> : IDisposable
{
    private readonly IObserver<TSource> _observer;
    private readonly TimeSpan _dueTime;
    private readonly IObservable<TSource>? _fallback;
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly SingleAssignmentDisposable _sourceSubscription = new();
    private readonly SingleAssignmentDisposable _fallbackSubscription = new();
    private ITimer? _timer;
    private bool _sourceActive = true;
    private bool _fallbackActive;
    private bool _stopped;

    public TimeoutSubscription(
        IObservable<TSource> source,
        IObserver<TSource> observer,
        TimeSpan dueTime,
        IObservable<TSource>? fallback,
        TimeProvider timeProvider
    )
    {
        _observer = observer;
        _dueTime = dueTime;
        _fallback = fallback;
        try
        {
            _timer = timeProvider.CreateTimer(
                static state => ((TimeoutSubscription<TSource>)state!).OnTimeout(),
                this,
                dueTime,
                System.Threading.Timeout.InfiniteTimeSpan
            );
            _sourceSubscription.Disposable = source.Subscribe(
                OnSourceNext,
                OnSourceError,
                OnSourceCompleted
            );
        }
        catch (Exception error)
        {
            bool notify;
            lock (_gate)
                notify = !_stopped;
            Dispose();
            if (notify)
                _observer.OnError(error);
        }
    }

    public void Dispose()
    {
        ITimer? timer;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _sourceActive = false;
            _fallbackActive = false;
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
        _sourceSubscription.Dispose();
        _fallbackSubscription.Dispose();
    }

    private void OnSourceNext(TSource value)
    {
        lock (_gate)
        {
            if (_stopped || !_sourceActive)
            {
                return;
            }

            _timer!.Change(_dueTime, System.Threading.Timeout.InfiniteTimeSpan);
            _observer.OnNext(value);
        }
    }

    private void OnSourceError(Exception error) => TerminateSource(error);

    private void OnSourceCompleted() => TerminateSource(null);

    private void TerminateSource(Exception? error)
    {
        ITimer? timer;
        lock (_gate)
        {
            if (_stopped || !_sourceActive)
            {
                return;
            }

            _stopped = true;
            _sourceActive = false;
            timer = _timer;
            _timer = null;
            if (error is null)
            {
                _observer.OnCompleted();
            }
            else
            {
                _observer.OnError(error);
            }
        }

        timer?.Dispose();
        _sourceSubscription.Dispose();
    }

    private void OnTimeout()
    {
        ITimer? timer;
        lock (_gate)
        {
            if (_stopped || !_sourceActive)
            {
                return;
            }

            _sourceActive = false;
            timer = _timer;
            _timer = null;
            if (_fallback is null)
            {
                _stopped = true;
                _observer.OnError(new TimeoutException());
            }
            else
            {
                _fallbackActive = true;
            }
        }

        timer?.Dispose();
        _sourceSubscription.Dispose();
        if (_fallback is null)
        {
            return;
        }

        try
        {
            _fallbackSubscription.Disposable = _fallback.Subscribe(
                OnFallbackNext,
                OnFallbackError,
                OnFallbackCompleted
            );
        }
        catch (Exception error)
        {
            OnFallbackError(error);
        }
    }

    private void OnFallbackNext(TSource value)
    {
        lock (_gate)
        {
            if (!_stopped && _fallbackActive)
            {
                _observer.OnNext(value);
            }
        }
    }

    private void OnFallbackError(Exception error) => TerminateFallback(error);

    private void OnFallbackCompleted() => TerminateFallback(null);

    private void TerminateFallback(Exception? error)
    {
        lock (_gate)
        {
            if (_stopped || !_fallbackActive)
            {
                return;
            }

            _stopped = true;
            _fallbackActive = false;
            if (error is null)
            {
                _observer.OnCompleted();
            }
            else
            {
                _observer.OnError(error);
            }
        }

        _fallbackSubscription.Dispose();
    }
}
