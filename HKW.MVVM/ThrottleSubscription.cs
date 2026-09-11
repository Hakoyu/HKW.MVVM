namespace HKW.MVVM;

/// <summary>Manages the timer and source subscription used by the throttle operator.</summary>
/// <typeparam name="TSource">The source value type.</typeparam>
internal sealed class ThrottleSubscription<TSource> : IDisposable
{
    private readonly IObserver<TSource> _observer;
    private readonly TimeSpan _dueTime;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly SingleAssignmentDisposable _subscription = new();
    private ITimer? _timer;
    private long _version;
    private bool _stopped;
    private bool _disposed;
    private bool _hasValue;
    private TSource? _lastValue;

    public ThrottleSubscription(
        IObservable<TSource> source,
        IObserver<TSource> observer,
        TimeSpan dueTime,
        TimeProvider timeProvider
    )
    {
        _observer = observer;
        _dueTime = dueTime;
        _timeProvider = timeProvider;
        try
        {
            _subscription.Disposable = source.Subscribe(OnNext, OnError, OnCompleted);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        ITimer? timer;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
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
            if (_stopped)
            {
                return;
            }

            _hasValue = true;
            _lastValue = value;
            var version = ++_version;
            _timer?.Dispose();
            _timer = _timeProvider.CreateTimer(
                static state =>
                {
                    var work = ((ThrottleSubscription<TSource> Subscription, long Version))state!;
                    work.Subscription.Emit(work.Version);
                },
                (this, version),
                _dueTime,
                Timeout.InfiniteTimeSpan
            );
        }
    }

    private void Emit(long expectedVersion)
    {
        TSource? value;
        lock (_gate)
        {
            if (_stopped || _hasValue is false || _version != expectedVersion)
            {
                return;
            }

            _hasValue = false;
            value = _lastValue;
        }

        _observer.OnNext(value!);
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
            _disposed = true;
            _hasValue = false;
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
        _subscription.Dispose();
        _observer.OnError(error);
    }

    private void OnCompleted()
    {
        TSource? value;
        bool emit;
        ITimer? timer;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _disposed = true;
            timer = _timer;
            _timer = null;
            emit = _hasValue;
            value = _lastValue;
            _hasValue = false;
        }

        timer?.Dispose();
        _subscription.Dispose();
        if (emit)
        {
            _observer.OnNext(value!);
        }

        _observer.OnCompleted();
    }
}
