namespace HKW.MVVM;

/// <summary>Manages the timer and source subscription used by the throttle operator.</summary>
/// <typeparam name="TSource">The source value type.</typeparam>
internal sealed class ThrottleSubscription<TSource> : IDisposable
{
    private readonly IObserver<TSource> _observer;
    private readonly TimeSpan _dueTime;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private IDisposable? _subscription;
    private ITimer? _timer;
    private long _version;
    private bool _stopped;
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
        _subscription = source.Subscribe(OnNext, OnError, OnCompleted);
    }

    public void Dispose()
    {
        IDisposable? subscription;
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
            subscription = _subscription;
            _subscription = null;
        }

        timer?.Dispose();
        subscription?.Dispose();
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
            _hasValue = false;
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
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
            timer = _timer;
            _timer = null;
            emit = _hasValue;
            value = _lastValue;
            _hasValue = false;
        }

        timer?.Dispose();
        if (emit)
        {
            _observer.OnNext(value!);
        }

        _observer.OnCompleted();
    }
}
