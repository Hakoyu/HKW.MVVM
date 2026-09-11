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
/// Convenience subscription overloads that do not require System.Reactive.
/// </summary>
public static class NativeObservableSubscriptionExtensions
{
    /// <summary>
    /// Subscribes to an observable sequence and invokes an action for every value.
    /// </summary>
    /// <typeparam name="T">The type of value produced by the sequence.</typeparam>
    /// <param name="source">The observable sequence to subscribe to.</param>
    /// <param name="onNext">The action invoked for each value.</param>
    /// <returns>A disposable object that cancels the subscription.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The method creates an <see cref="IObserver{T}"/> wrapper directly.</remarks>
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext) =>
        Subscribe(
            source,
            onNext,
            error =>
                throw new InvalidOperationException("Observable terminated with an error.", error)
        );

    /// <summary>
    /// Subscribes to an observable sequence with callbacks for values, errors, and completion.
    /// </summary>
    /// <typeparam name="T">The type of value produced by the sequence.</typeparam>
    /// <param name="source">The observable sequence to subscribe to.</param>
    /// <param name="onNext">The action invoked for each value.</param>
    /// <param name="onError">The action invoked when the sequence terminates with an error.</param>
    /// <param name="onCompleted">The optional action invoked when the sequence completes successfully.</param>
    /// <returns>A disposable object that cancels the subscription.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The method creates an <see cref="IObserver{T}"/> wrapper directly.</remarks>
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
