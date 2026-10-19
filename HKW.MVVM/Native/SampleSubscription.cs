namespace HKW.MVVM;

internal sealed class SampleSubscription<TSource> : IDisposable
{
    private readonly IObserver<TSource> _observer;
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly SingleAssignmentDisposable _subscription = new();
    private ITimer? _timer;
    private bool _hasValue;
    private TSource? _value;
    private bool _sourceCompleted;
    private bool _stopped;

    public SampleSubscription(
        IObservable<TSource> source,
        IObserver<TSource> observer,
        TimeSpan interval,
        TimeProvider timeProvider
    )
    {
        _observer = observer;
        try
        {
            _timer = timeProvider.CreateTimer(
                static state => ((SampleSubscription<TSource>)state!).Tick(),
                this,
                interval,
                interval
            );
            _subscription.Disposable = source.Subscribe(OnNext, OnError, OnCompleted);
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
            _hasValue = false;
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
        _subscription.Dispose();
    }

    private void OnNext(TSource value)
    {
        lock (_gate)
        {
            if (_stopped || _sourceCompleted)
            {
                return;
            }

            _hasValue = true;
            _value = value;
        }
    }

    private void OnError(Exception error)
    {
        ITimer? timer;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _hasValue = false;
            timer = _timer;
            _timer = null;
            _observer.OnError(error);
        }

        timer?.Dispose();
        _subscription.Dispose();
    }

    private void OnCompleted()
    {
        lock (_gate)
        {
            if (_stopped || _sourceCompleted)
            {
                return;
            }

            _sourceCompleted = true;
        }

        _subscription.Dispose();
    }

    private void Tick()
    {
        ITimer? timer = null;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            if (_hasValue)
            {
                var value = _value;
                _hasValue = false;
                _observer.OnNext(value!);
            }

            if (_sourceCompleted)
            {
                _stopped = true;
                timer = _timer;
                _timer = null;
                _observer.OnCompleted();
            }
        }

        timer?.Dispose();
    }
}
