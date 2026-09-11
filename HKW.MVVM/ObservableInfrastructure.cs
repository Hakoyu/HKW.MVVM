namespace HKW.MVVM;

internal sealed class AnonymousObservable<T>(Func<IObserver<T>, IDisposable> subscribe) : IObservable<T>
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
    Action? onCompleted = null) : IObserver<T>
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

internal sealed class CompositeDisposable : IDisposable
{
    private readonly object _gate = new();
    private List<IDisposable>? _items = [];

    public void Add(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            if (_items is not null)
            {
                _items.Add(item);
                return;
            }
        }

        item.Dispose();
    }

    public void Dispose()
    {
        List<IDisposable>? items;
        lock (_gate)
        {
            items = _items;
            _items = null;
        }

        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            item.Dispose();
        }
    }
}

internal sealed class ExceptionSubject : IObservable<Exception>, IDisposable
{
    private readonly object _gate = new();
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
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext) =>
        Subscribe(source, onNext, error => throw new InvalidOperationException("Observable terminated with an error.", error));

    public static IDisposable Subscribe<T>(
        this IObservable<T> source,
        Action<T> onNext,
        Action<Exception> onError,
        Action? onCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        ArgumentNullException.ThrowIfNull(onError);
        return source.Subscribe(new AnonymousObserver<T>(onNext, onError, onCompleted));
    }
}