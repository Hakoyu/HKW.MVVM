namespace HKW.MVVM;

internal sealed class BufferSubscription<TSource> : IDisposable
{
    private readonly IObserver<IList<TSource>> _observer;
    private readonly TimeSpan _timeSpan;
    private readonly int _count;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly SingleAssignmentDisposable _subscription = new();
    private readonly bool _useTimer;
    private List<TSource> _buffer = [];
    private ITimer? _timer;
    private bool _stopped;
    private bool _disposed;

    public BufferSubscription(
        IObservable<TSource> source,
        IObserver<IList<TSource>> observer,
        TimeSpan timeSpan,
        int count,
        bool useTimer,
        TimeProvider timeProvider
    )
    {
        _observer = observer;
        _timeSpan = timeSpan;
        _count = count;
        _useTimer = useTimer;
        _timeProvider = timeProvider;

        try
        {
            if (_useTimer)
            {
                _timer = _timeProvider.CreateTimer(
                    static state => ((BufferSubscription<TSource>)state!).CloseTimedWindow(),
                    this,
                    _timeSpan,
                    Timeout.InfiniteTimeSpan
                );
            }

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
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopped = true;
            _buffer = [];
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

            _buffer.Add(value);
            if (_buffer.Count < _count)
            {
                return;
            }

            var buffer = TakeBuffer();
            RestartTimer();
            _observer.OnNext(buffer);
        }
    }

    private void CloseTimedWindow()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            var buffer = TakeBuffer();
            RestartTimer();
            _observer.OnNext(buffer);
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
            _disposed = true;
            _buffer = [];
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
        _subscription.Dispose();
        _observer.OnError(error);
    }

    private void OnCompleted()
    {
        IList<TSource>? buffer;
        ITimer? timer;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _disposed = true;
            buffer = _buffer.Count == 0 ? null : TakeBuffer();
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
        _subscription.Dispose();
        if (buffer is not null)
        {
            _observer.OnNext(buffer);
        }

        _observer.OnCompleted();
    }

    private List<TSource> TakeBuffer()
    {
        var buffer = _buffer;
        _buffer = [];
        return buffer;
    }

    private void RestartTimer()
    {
        if (_useTimer)
        {
            _timer!.Change(_timeSpan, Timeout.InfiniteTimeSpan);
        }
    }
}
