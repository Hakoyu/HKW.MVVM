namespace HKW.MVVM;

internal sealed class DelaySubscription<TSource> : IDisposable
{
    private readonly IObserver<TSource> _observer;
    private readonly TimeSpan _dueTime;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly SingleAssignmentDisposable _subscription = new();
    private readonly Queue<Notification> _notifications = [];
    private ITimer? _timer;
    private bool _accepting = true;
    private bool _disposed;

    public DelaySubscription(
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
        catch (Exception error)
        {
            bool notify;
            lock (_gate)
                notify = !_disposed;
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
            _accepting = false;
            _notifications.Clear();
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
        _subscription.Dispose();
    }

    private void OnNext(TSource value) => Enqueue(new Notification(value, null, false, _timeProvider.GetTimestamp()));

    private void OnError(Exception error) => Enqueue(new Notification(default, error, true, _timeProvider.GetTimestamp()));

    private void OnCompleted() => Enqueue(new Notification(default, null, true, _timeProvider.GetTimestamp()));

    private void Enqueue(Notification notification)
    {
        lock (_gate)
        {
            if (!_accepting || _disposed)
            {
                return;
            }

            if (notification.IsTerminal)
            {
                _accepting = false;
                _subscription.Dispose();
            }

            _notifications.Enqueue(notification);
            if (_timer is null)
            {
                ScheduleHead();
            }
        }
    }

    private void ScheduleHead()
    {
        var notification = _notifications.Peek();
        var remaining = _dueTime - _timeProvider.GetElapsedTime(notification.Timestamp);
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        _timer = _timeProvider.CreateTimer(
            static state => ((DelaySubscription<TSource>)state!).DrainOne(),
            this,
            remaining,
            System.Threading.Timeout.InfiniteTimeSpan
        );
    }

    private void DrainOne()
    {
        lock (_gate)
        {
            if (_disposed || _notifications.Count == 0)
            {
                return;
            }

            _timer?.Dispose();
            _timer = null;
            var notification = _notifications.Dequeue();
            if (notification.Error is not null)
            {
                _disposed = true;
                _notifications.Clear();
                _subscription.Dispose();
                _observer.OnError(notification.Error);
                return;
            }

            if (notification.IsTerminal)
            {
                _disposed = true;
                _notifications.Clear();
                _subscription.Dispose();
                _observer.OnCompleted();
                return;
            }

            _observer.OnNext(notification.Value!);
            if (!_disposed && _notifications.Count > 0)
            {
                ScheduleHead();
            }
        }
    }

    private readonly record struct Notification(
        TSource? Value,
        Exception? Error,
        bool IsTerminal,
        long Timestamp
    );
}
