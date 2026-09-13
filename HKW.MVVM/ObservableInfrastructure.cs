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
    private readonly Lock _gate = new();
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
/// 串行化已调度的回调,同时保持其入队顺序.
/// </summary>
internal sealed class SerialActionQueue(Action<Action> schedule) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Queue<Action> _actions = [];
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
            schedule(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            Action? action;
            lock (_gate)
            {
                if (_disposed || _actions.Count == 0)
                {
                    _scheduled = false;
                    return;
                }

                action = _actions.Dequeue();
            }

            action();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _actions.Clear();
        }
    }
}

internal sealed class ExceptionSubject : IObservable<Exception>, IDisposable
{
    private readonly Lock _gate = new();
    private List<IObserver<Exception>>? _observers = [];

    public IDisposable Subscribe(IObserver<Exception> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_gate)
        {
            if (_observers is null)
            {
                observer.OnCompleted();
                return new ActionDisposable(() => { });
            }

            _observers.Add(observer);
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
        IObserver<Exception>[] observers;
        lock (_gate)
        {
            observers = _observers?.ToArray() ?? [];
        }

        foreach (var observer in observers)
        {
            observer.OnNext(exception);
        }
    }

    public void Dispose()
    {
        IObserver<Exception>[] observers;
        lock (_gate)
        {
            observers = _observers?.ToArray() ?? [];
            _observers = null;
        }

        foreach (var observer in observers)
        {
            observer.OnCompleted();
        }
    }
}

/// <summary>
/// 无需 System.Reactive 的便捷订阅重载.
/// </summary>
public static class NativeObservableSubscriptionExtensions
{
    /// <summary>
    /// 订阅可观察序列,并对每个值调用一个操作.
    /// </summary>
    /// <typeparam name="T">序列产生的值类型.</typeparam>
    /// <param name="source">要订阅的可观察序列.</param>
    /// <param name="onNext">对每个值调用的操作.</param>
    /// <returns>可取消该订阅的可释放对象.</returns>
    /// <remarks><b>反射:否.</b>该方法直接创建 <see cref="IObserver{T}"/> 包装器.</remarks>
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return source.Subscribe(new OnNextObserver<T>(onNext));
    }

    /// <summary>
    /// 订阅可观察序列,并提供针对值、错误和完成的回调.
    /// </summary>
    /// <typeparam name="T">序列产生的值类型.</typeparam>
    /// <param name="source">要订阅的可观察序列.</param>
    /// <param name="onNext">对每个值调用的操作.</param>
    /// <param name="onError">序列以错误终止时调用的操作.</param>
    /// <param name="onCompleted">序列成功完成时调用的可选操作.</param>
    /// <returns>可取消该订阅的可释放对象.</returns>
    /// <remarks><b>反射:否.</b>该方法直接创建 <see cref="IObserver{T}"/> 包装器.</remarks>
    public static IDisposable Subscribe<T>(
        this IObservable<T> source,
        Action<T> onNext,
        Action<Exception> onError,
        Action? onCompleted = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        ArgumentNullException.ThrowIfNull(onError);
        return source.Subscribe(new AnonymousObserver<T>(onNext, onError, onCompleted));
    }
}
