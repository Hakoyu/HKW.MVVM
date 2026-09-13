namespace HKW.MVVM;

/// <summary>
/// 描述响应式对象的属性更改.
/// </summary>
/// <typeparam name="TSender">发生属性更改的对象类型.</typeparam>
public interface IPropertyChangedEventArgs<out TSender>
{
    /// <summary>
    /// 发生属性更改的对象.
    /// </summary>
    TSender Sender { get; }

    /// <summary>
    /// 发生更改的属性名称.
    /// </summary>
    string? PropertyName { get; }
}

/// <summary>
/// <see cref="IPropertyChangedEventArgs{TSender}"/> 的默认实现.
/// </summary>
/// <typeparam name="TSender">发生属性更改的对象类型.</typeparam>
public readonly record struct PropertyChangedEventArgs<TSender>(
    TSender Sender,
    string? PropertyName
) : IPropertyChangedEventArgs<TSender>;

internal sealed class PropertyChangeSubject<T> : IObservable<T>
{
    private readonly Lock _gate = new();
    private readonly List<IObserver<T>> _observers = [];

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_gate)
        {
            _observers.Add(observer);
        }

        return new ActionDisposable(() =>
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        });
    }

    public void OnNext(T value)
    {
        IObserver<T>[] observers;
        lock (_gate)
        {
            observers = _observers.ToArray();
        }

        foreach (var observer in observers)
        {
            observer.OnNext(value);
        }
    }
}

/// <summary>
/// 便捷订阅重载.
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
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return source.Subscribe(new OnNextObserver<T>(onNext));
    }

    /// <summary>
    /// 订阅可观察序列,并提供针对值,错误和完成的回调.
    /// </summary>
    /// <typeparam name="T">序列产生的值类型.</typeparam>
    /// <param name="source">要订阅的可观察序列.</param>
    /// <param name="onNext">对每个值调用的操作.</param>
    /// <param name="onError">序列以错误终止时调用的操作.</param>
    /// <param name="onCompleted">序列成功完成时调用的可选操作.</param>
    /// <returns>可取消该订阅的可释放对象.</returns>
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
