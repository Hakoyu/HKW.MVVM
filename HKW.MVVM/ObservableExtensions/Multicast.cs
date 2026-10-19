namespace HKW.MVVM;

/// <summary>
/// 表示在显式连接后将单个源订阅多播给多个观察者的可观察序列
/// </summary>
/// <typeparam name="T">序列产生的值类型</typeparam>
public interface IConnectableObservable<out T> : IObservable<T>
{
    /// <summary>
    /// 建立到源序列的共享连接
    /// </summary>
    /// <returns>释放时断开共享源订阅的对象</returns>
    IDisposable Connect();
}

public static partial class ObservableExtensions
{
    /// <summary>
    /// 创建一个在连接后向当前订阅者多播源通知的可连接序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要共享的源序列</param>
    /// <returns>不重放历史值的可连接序列</returns>
    public static IConnectableObservable<TSource> Publish<TSource>(this IObservable<TSource> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ConnectableObservable<TSource>(source, 0);
    }

    /// <summary>
    /// 创建一个在连接后多播源通知, 并向新订阅者重放最多指定数量值的可连接序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要共享的源序列</param>
    /// <param name="bufferSize">为每个新订阅者保留的最大值数量</param>
    /// <returns>具有有界重放缓冲区的可连接序列</returns>
    public static IConnectableObservable<TSource> Replay<TSource>(
        this IObservable<TSource> source,
        int bufferSize
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);
        return new ConnectableObservable<TSource>(source, bufferSize);
    }

    /// <summary>
    /// 将可连接序列转换为在首个订阅者到达时连接、最后一个订阅者离开时断开的序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要按引用计数管理的可连接序列</param>
    /// <returns>自动管理共享连接的可观察序列</returns>
    public static IObservable<TSource> RefCount<TSource>(
        this IConnectableObservable<TSource> source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(source, new RefCountObservable<TSource>(source));
    }

    private sealed class ConnectableObservable<T> : IConnectableObservable<T>, IObservableWithLogger
    {
        private readonly IObservable<T> _source;
        private readonly int _bufferSize;
        private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
        private readonly List<ObserverSubscription> _observers = [];
        private readonly Queue<T> _buffer = [];
        private Connection? _connection;
        private TerminalNotification? _terminal;

        public ConnectableObservable(IObservable<T> source, int bufferSize)
        {
            _source = source;
            _bufferSize = bufferSize;
        }

        public IDisposable Connect()
        {
            Connection connection;
            lock (_gate)
            {
                if (_terminal is not null)
                {
                    return _emptyDisposable;
                }

                if (_connection is not null)
                {
                    return _connection;
                }

                connection = new Connection(this);
                _connection = connection;
            }

            try
            {
                connection.SetUpstream(_source.Subscribe(connection));
            }
            catch (Exception exception)
            {
                connection.OnError(exception);
            }

            return connection;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new ObserverSubscription(this, observer);
            var shouldDrain = false;

            lock (_gate)
            {
                foreach (var value in _buffer)
                {
                    shouldDrain |= subscription.Enqueue(Notification.Next(value));
                }

                if (_terminal is { } terminal)
                {
                    shouldDrain |= subscription.Enqueue(terminal.ToNotification());
                }
                else
                {
                    _observers.Add(subscription);
                }
            }

            if (shouldDrain)
            {
                subscription.Drain();
            }

            return subscription;
        }

        public Microsoft.Extensions.Logging.ILogger GetLogger() =>
            _source is IObservableWithLogger observableWithLogger
                ? observableWithLogger.GetLogger()
                : throw new InvalidOperationException(
                    "The observable does not provide a logger. Pass an ILogger explicitly."
                );

        private void Disconnect(Connection connection)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_connection, connection))
                {
                    _connection = null;
                }
            }
        }

        private void Remove(ObserverSubscription subscription)
        {
            lock (_gate)
            {
                _observers.Remove(subscription);
            }
        }

        private void Next(Connection connection, T value)
        {
            List<ObserverSubscription>? drains = null;
            lock (_gate)
            {
                if (!ReferenceEquals(_connection, connection) || _terminal is not null)
                {
                    return;
                }

                if (_bufferSize > 0)
                {
                    _buffer.Enqueue(value);
                    while (_buffer.Count > _bufferSize)
                    {
                        _buffer.Dequeue();
                    }
                }

                foreach (var observer in _observers)
                {
                    if (observer.Enqueue(Notification.Next(value)))
                    {
                        (drains ??= []).Add(observer);
                    }
                }
            }

            Drain(drains);
        }

        private void Terminate(Connection connection, Exception? error)
        {
            List<ObserverSubscription>? drains = null;
            lock (_gate)
            {
                if (!ReferenceEquals(_connection, connection) || _terminal is not null)
                {
                    return;
                }

                _terminal = new TerminalNotification(error);
                _connection = null;
                var notification = _terminal.Value.ToNotification();
                foreach (var observer in _observers)
                {
                    if (observer.Enqueue(notification))
                    {
                        (drains ??= []).Add(observer);
                    }
                }
                _observers.Clear();
            }

            connection.Stop();
            Drain(drains);
        }

        private static void Drain(List<ObserverSubscription>? subscriptions)
        {
            if (subscriptions is null)
            {
                return;
            }

            Exception? firstError = null;
            foreach (var subscription in subscriptions)
            {
                try
                {
                    subscription.Drain();
                }
                catch (Exception exception)
                {
                    firstError ??= exception;
                }
            }

            if (firstError is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
            }
        }

        private sealed class Connection(ConnectableObservable<T> owner) : IObserver<T>, IDisposable
        {
            private readonly SingleAssignmentDisposable _upstream = new();
            private int _stopped;

            public void SetUpstream(IDisposable upstream) => _upstream.Disposable = upstream;

            public void OnNext(T value)
            {
                if (Volatile.Read(ref _stopped) == 0)
                {
                    owner.Next(this, value);
                }
            }

            public void OnError(Exception error)
            {
                ArgumentNullException.ThrowIfNull(error);
                if (Interlocked.Exchange(ref _stopped, 1) == 0)
                {
                    owner.Terminate(this, error);
                }
            }

            public void OnCompleted()
            {
                if (Interlocked.Exchange(ref _stopped, 1) == 0)
                {
                    owner.Terminate(this, null);
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _stopped, 1) != 0)
                {
                    return;
                }

                owner.Disconnect(this);
                _upstream.Dispose();
            }

            public void Stop()
            {
                Interlocked.Exchange(ref _stopped, 1);
                _upstream.Dispose();
            }
        }

        private sealed class ObserverSubscription(
            ConnectableObservable<T> owner,
            IObserver<T> observer
        ) : IDisposable
        {
            private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
            private readonly Queue<Notification> _notifications = [];
            private bool _draining;
            private bool _disposed;

            public bool Enqueue(Notification notification)
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    _notifications.Enqueue(notification);
                    if (_draining)
                    {
                        return false;
                    }

                    _draining = true;
                    return true;
                }
            }

            public void Drain()
            {
                while (true)
                {
                    Notification notification;
                    lock (_gate)
                    {
                        if (_disposed || !_notifications.TryDequeue(out notification))
                        {
                            _draining = false;
                            return;
                        }
                    }

                    try
                    {
                        switch (notification.Kind)
                        {
                            case NotificationKind.Next:
                                observer.OnNext(notification.Value!);
                                break;
                            case NotificationKind.Error:
                                observer.OnError(notification.Error!);
                                Dispose();
                                break;
                            default:
                                observer.OnCompleted();
                                Dispose();
                                break;
                        }
                    }
                    catch
                    {
                        Dispose();
                        throw;
                    }
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    _notifications.Clear();
                }

                owner.Remove(this);
            }
        }

        private readonly record struct TerminalNotification(Exception? Error)
        {
            public Notification ToNotification() =>
                Error is null ? Notification.Completed() : Notification.Failed(Error);
        }

        private readonly record struct Notification(
            NotificationKind Kind,
            T? Value,
            Exception? Error
        )
        {
            public static Notification Next(T value) =>
                new(NotificationKind.Next, value, null);

            public static Notification Failed(Exception error) =>
                new(NotificationKind.Error, default, error);

            public static Notification Completed() =>
                new(NotificationKind.Completed, default, null);
        }

        private enum NotificationKind
        {
            Next,
            Error,
            Completed,
        }
    }

    private sealed class RefCountObservable<T>(IConnectableObservable<T> source) : IObservable<T>
    {
        private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
        private IDisposable? _connection;
        private int _subscriberCount;
        private long _generation;
        private bool _connecting;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = source.Subscribe(observer);
            var shouldConnect = false;
            var generation = 0L;

            lock (_gate)
            {
                _subscriberCount++;
                generation = _generation;
                if (_connection is null && !_connecting)
                {
                    _connecting = true;
                    shouldConnect = true;
                }
            }

            if (shouldConnect)
            {
                Connect(generation);
            }

            return new ActionDisposable(() => Release(subscription));
        }

        private void Connect(long generation)
        {
            while (true)
            {
                var connection = source.Connect();
                var retry = false;
                lock (_gate)
                {
                    if (
                        _subscriberCount > 0
                        && _generation == generation
                        && _connection is null
                    )
                    {
                        _connection = connection;
                        _connecting = false;
                        return;
                    }

                    if (_subscriberCount > 0 && _connection is null)
                    {
                        generation = _generation;
                        retry = true;
                    }
                    else
                    {
                        _connecting = false;
                    }
                }

                connection.Dispose();
                if (!retry)
                {
                    return;
                }
            }
        }

        private void Release(IDisposable subscription)
        {
            IDisposable? connection = null;
            lock (_gate)
            {
                if (_subscriberCount > 0 && --_subscriberCount == 0)
                {
                    _generation++;
                    connection = _connection;
                    _connection = null;
                }
            }

            subscription.Dispose();
            connection?.Dispose();
        }
    }
}
