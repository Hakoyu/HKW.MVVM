namespace HKW.MVVM;

/// <summary>
/// 描述响应式对象的属性更改
/// </summary>
/// <typeparam name="TSender">发生属性更改的对象类型</typeparam>
public interface IPropertyChangeEventArgs<out TSender>
{
    /// <summary>
    /// 发生属性更改的对象
    /// </summary>
    TSender Sender { get; }

    /// <summary>
    /// 发生更改的属性名称
    /// </summary>
    string? PropertyName { get; }
}

/// <summary>
/// <see cref="IPropertyChangeEventArgs{TSender}"/> 的默认实现
/// </summary>
/// <typeparam name="TSender">发生属性更改的对象类型</typeparam>
public readonly record struct PropertyChangeEventArgs<TSender>(TSender Sender, string? PropertyName)
    : IPropertyChangeEventArgs<TSender>;

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
