using System;
using System.Collections.Generic;
using System.Text;

namespace HKW.MVVM;

internal sealed class AnonymousObservable<T>(Func<IObserver<T>, IDisposable> subscribe)
    : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return subscribe(observer);
    }
}

internal sealed class AnonymousObserver<T>(
    Action<T> onNext,
    Action<Exception>? onError = null,
    Action? onCompleted = null
) : IObserver<T>
{
    public void OnNext(T value) => onNext(value);

    public void OnError(Exception error) => (onError ?? (_ => { }))(error);

    public void OnCompleted() => onCompleted?.Invoke();
}

internal sealed class OnNextObserver<T>(Action<T> onNext) : IObserver<T>
{
    public void OnNext(T value) => onNext(value);

    public void OnError(Exception error) =>
        throw new InvalidOperationException("Observable terminated with an error.", error);

    public void OnCompleted() { }
}

internal sealed class ActionDisposable(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;

    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}

internal sealed class SingleAssignmentDisposable : IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private IDisposable? _disposable;
    private bool _assigned;
    private bool _disposed;

    public IDisposable Disposable
    {
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate)
            {
                if (_assigned)
                {
                    throw new InvalidOperationException(
                        "The disposable has already been assigned."
                    );
                }

                _assigned = true;
                if (_disposed is false)
                {
                    _disposable = value;
                    return;
                }
            }

            value.Dispose();
        }
    }

    public void Dispose()
    {
        IDisposable? disposable;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            disposable = _disposable;
            _disposable = null;
        }

        disposable?.Dispose();
    }
}

/// <summary>
/// 串行化已调度的回调,同时保持其入队顺序
/// </summary>
internal sealed class SerialActionQueue(Func<Action, IDisposable> schedule) : IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly Queue<Action> _actions = [];
    private SingleAssignmentDisposable? _scheduledWork;
    private bool _scheduled;
    private bool _disposed;

    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var shouldSchedule = false;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _actions.Enqueue(action);
            if (_scheduled is false)
            {
                _scheduled = true;
                shouldSchedule = true;
            }
        }

        if (shouldSchedule)
        {
            var scheduledWork = new SingleAssignmentDisposable();
            lock (_gate)
            {
                if (_disposed)
                {
                    scheduledWork.Dispose();
                }
                else
                {
                    _scheduledWork = scheduledWork;
                }
            }

            scheduledWork.Disposable = schedule(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            Action? action;
            SingleAssignmentDisposable? completedWork = null;
            lock (_gate)
            {
                if (_disposed || _actions.Count == 0)
                {
                    _scheduled = false;
                    completedWork = _scheduledWork;
                    _scheduledWork = null;
                    action = null;
                }
                else
                {
                    action = _actions.Dequeue();
                }
            }

            if (action is null)
            {
                completedWork?.Dispose();
                return;
            }

            action();
        }
    }

    public void Dispose()
    {
        SingleAssignmentDisposable? scheduledWork;
        lock (_gate)
        {
            _disposed = true;
            _actions.Clear();
            scheduledWork = _scheduledWork;
            _scheduledWork = null;
        }

        scheduledWork?.Dispose();
    }
}

internal sealed class ExceptionSubject(bool replayLatest = false)
    : IObservable<Exception>,
        IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private readonly Queue<Notification> _notifications = [];
    private List<IObserver<Exception>>? _observers = [];
    private Exception? _latest;
    private bool _isDraining;

    public IDisposable Subscribe(IObserver<Exception> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        var isCompleted = false;
        var shouldDrain = false;
        lock (_gate)
        {
            if (_observers is null)
            {
                isCompleted = true;
            }
            else
            {
                _observers.Add(observer);
                if (replayLatest && _latest is not null)
                {
                    _notifications.Enqueue(new Notification([observer], _latest, false));
                    if (!_isDraining)
                    {
                        _isDraining = true;
                        shouldDrain = true;
                    }
                }
            }
        }

        if (isCompleted)
        {
            observer.OnCompleted();
            return new ActionDisposable(() => { });
        }
        if (shouldDrain)
        {
            Drain();
        }

        return new ActionDisposable(() =>
        {
            lock (_gate)
            {
                _observers?.Remove(observer);
            }
        });
    }

    public void OnNext(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var shouldDrain = false;
        lock (_gate)
        {
            if (_observers is null)
            {
                return;
            }
            if (replayLatest)
            {
                _latest = exception;
            }
            _notifications.Enqueue(new Notification([.. _observers], exception, false));
            if (!_isDraining)
            {
                _isDraining = true;
                shouldDrain = true;
            }
        }

        if (shouldDrain)
        {
            Drain();
        }
    }

    public void Dispose()
    {
        var shouldDrain = false;
        lock (_gate)
        {
            if (_observers is null)
            {
                return;
            }
            _notifications.Enqueue(new Notification([.. _observers], null, true));
            _observers = null;
            if (!_isDraining)
            {
                _isDraining = true;
                shouldDrain = true;
            }
        }

        if (shouldDrain)
        {
            Drain();
        }
    }

    private void Drain()
    {
        while (true)
        {
            Notification notification;
            lock (_gate)
            {
                if (_notifications.Count == 0)
                {
                    _isDraining = false;
                    return;
                }
                notification = _notifications.Dequeue();
            }

            foreach (var observer in notification.Observers)
            {
                try
                {
                    if (notification.IsCompleted)
                    {
                        observer.OnCompleted();
                    }
                    else
                    {
                        observer.OnNext(notification.Exception!);
                    }
                }
                catch
                {
                    // Exception streams must not rethrow from bridge notification threads.
                }
            }
        }
    }

    private readonly record struct Notification(
        IObserver<Exception>[] Observers,
        Exception? Exception,
        bool IsCompleted
    );
}
