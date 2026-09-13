using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM;

/// <summary>
/// 存储可观察序列的最新值,并为只读的拥有者属性引发通知.
/// </summary>
public sealed class ObservableAsPropertyHelper<T>
    : IDisposable,
        INotifyPropertyChanged,
        INotifyPropertyChanging
{
    private readonly Lock _gate = new();
    private readonly IObservable<T> _source;
    private readonly ObservableObject _owner;
    private readonly string _propertyName;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly bool _scheduleOnThreadPool;
    private readonly SerialActionQueue? _notificationQueue;
    private readonly ExceptionSubject _exceptions = new();
    private IDisposable? _subscription;
    private T _value;
    private bool _started;
    private bool _disposed;

    internal ObservableAsPropertyHelper(
        IObservable<T> source,
        ObservableObject owner,
        string propertyName,
        T initialValue,
        bool deferSubscription,
        SynchronizationContext? synchronizationContext
    )
    {
        _source = source;
        _owner = owner;
        _propertyName = propertyName;
        _value = initialValue;
        _synchronizationContext = synchronizationContext;
        _notificationQueue = synchronizationContext is null
            ? null
            : new SerialActionQueue(action => synchronizationContext.Post(static state => ((Action)state!).Invoke(), action));

        if (deferSubscription is false)
        {
            EnsureSubscribed();
        }
    }

    internal ObservableAsPropertyHelper(
        IObservable<T> source,
        ObservableObject owner,
        string propertyName,
        T initialValue,
        bool deferSubscription,
        ObservableSchedulers scheduler
    )
    {
        _source = source;
        _owner = owner;
        _propertyName = propertyName;
        _value = initialValue;
        (_synchronizationContext, _scheduleOnThreadPool) = scheduler switch
        {
            ObservableSchedulers.Current =>
                (SynchronizationContext.Current, SynchronizationContext.Current is null),
            ObservableSchedulers.ThreadPool => (null, true),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scheduler),
                scheduler,
                "Unknown observable scheduler."
            ),
        };
        _notificationQueue = new SerialActionQueue(action =>
        {
            if (_scheduleOnThreadPool)
            {
                ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), action, preferLocal: false);
            }
            else
            {
                _synchronizationContext!.Post(static state => ((Action)state!).Invoke(), action);
            }
        });

        if (deferSubscription is false)
        {
            EnsureSubscribed();
        }
    }

    /// <summary>
/// 在 <see cref="Value"/> 变更之前引发.
/// </summary>
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>
/// 在 <see cref="Value"/> 变更之后引发.
/// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
/// 获取从源可观察序列接收的最新值.
/// </summary>
    public T Value
    {
        get
        {
            EnsureSubscribed();
            lock (_gate)
            {
                return _value;
            }
        }
    }

    /// <summary>
/// 接收源产生的终止性错误.
/// </summary>
    public IObservable<Exception> ThrownExceptions => _exceptions;

    /// <summary>
/// 停止观察源并释放所有已占用的资源.
/// </summary>
    public void Dispose()
    {
        IDisposable? subscription;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            subscription = _subscription;
            _subscription = null;
        }

        subscription?.Dispose();
        _notificationQueue?.Dispose();
        _exceptions.Dispose();
    }

    private void EnsureSubscribed()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            _subscription = _source.Subscribe(SetValue, _exceptions.OnNext);
        }
    }

    private void SetValue(T value)
    {
        if (
            _scheduleOnThreadPool is false
            && (
                _synchronizationContext is null
                || SynchronizationContext.Current == _synchronizationContext
            )
        )
        {
            SetValueCore(value);
            return;
        }

        _notificationQueue!.Enqueue(() => SetValueCore(value));
    }

    private void SetValueCore(T value)
    {
        PropertyChangingEventHandler? propertyChanging;
        PropertyChangedEventHandler? propertyChanged;
        lock (_gate)
        {
            if (_disposed || EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            propertyChanging = PropertyChanging;
            propertyChanged = PropertyChanged;
        }

        propertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
        PropertyNotificationDispatcher.NotifyPropertyChanging(_owner, _propertyName);
        lock (_gate)
        {
            if (_disposed) return;
            _value = value;
        }
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        PropertyNotificationDispatcher.NotifyPropertyChanged(_owner, _propertyName);
    }

}

/// <summary>
/// 提供用于将可观察值公开为只读属性的扩展.
/// </summary>
public static class ObservableAsPropertyHelperExtensions
{
    /// <summary>
    /// 将可观察序列转换为针对由表达式选择的只读拥有者属性的、带调度的辅助对象.
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型.</typeparam>
    /// <typeparam name="TValue">属性的值类型.</typeparam>
    /// <param name="source">提供属性值的序列.</param>
    /// <param name="owner">拥有该只读属性的对象.</param>
    /// <param name="property">选择 <paramref name="owner"/> 上直接属性的表达式.</param>
    /// <param name="scheduler">用于派发值变更和通知的调度器.</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值.</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时.</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象.</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 会在创建辅助对象时捕获
    /// <see cref="SynchronizationContext.Current"/>,不存在上下文时回退到线程池.
    /// <see cref="ObservableSchedulers.ThreadPool"/> 则始终将变更排队到线程池.
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        ObservableSchedulers scheduler,
        TValue initialValue = default!,
        bool deferSubscription = false
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        return ToProperty(
            source,
            owner,
            property.GetPropertyName(),
            scheduler,
            initialValue,
            deferSubscription
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对由表达式选择的只读拥有者属性的辅助对象.
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型.</typeparam>
    /// <typeparam name="TValue">属性的值类型.</typeparam>
    /// <param name="source">提供属性值的序列.</param>
    /// <param name="owner">拥有该只读属性的对象.</param>
    /// <param name="property">选择 <paramref name="owner"/> 上直接属性的表达式.</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值.</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时.</param>
    /// <param name="synchronizationContext">用于派发值变更和通知的可选上下文.</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象.</returns>
    /// <remarks>
    /// <b>反射:有条件.</b>属性名称从表达式中提取.拥有者通知
    /// 优先使用 <see cref="IPropertyChangeNotifier"/>,否则通过由 <see cref="MethodInfo"/> 实例
    /// 创建的缓存委托来调用 CommunityToolkit 的受保护方法.
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        TValue initialValue = default!,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        var propertyName = property.GetPropertyName();
        return ToProperty(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            synchronizationContext
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对按名称标识的只读拥有者属性的、带调度的辅助对象.
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型.</typeparam>
    /// <typeparam name="TValue">属性的值类型.</typeparam>
    /// <param name="source">提供属性值的序列.</param>
    /// <param name="owner">拥有该只读属性的对象.</param>
    /// <param name="propertyName">变更通知中使用的拥有者属性名称.</param>
    /// <param name="scheduler">用于派发值变更和通知的调度器.</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值.</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时.</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象.</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 会在创建辅助对象时捕获
    /// <see cref="SynchronizationContext.Current"/>,不存在上下文时回退到线程池.
    /// <see cref="ObservableSchedulers.ThreadPool"/> 则始终将变更排队到线程池.
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        ObservableSchedulers scheduler,
        TValue initialValue = default!,
        bool deferSubscription = false
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new ObservableAsPropertyHelper<TValue>(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            scheduler
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对按名称标识的只读拥有者属性的辅助对象.
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型.</typeparam>
    /// <typeparam name="TValue">属性的值类型.</typeparam>
    /// <param name="source">提供属性值的序列.</param>
    /// <param name="owner">拥有该只读属性的对象.</param>
    /// <param name="propertyName">变更通知中使用的拥有者属性名称.</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值.</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时.</param>
    /// <param name="synchronizationContext">用于派发值变更和通知的可选上下文.</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象.</returns>
    /// <remarks>
    /// <b>反射:有条件.</b>拥有者通知优先使用 <see cref="IPropertyChangeNotifier"/>,
    /// 否则通过仅使用一次反射创建的缓存委托来调用 CommunityToolkit 的受保护方法.
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        TValue initialValue = default!,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new ObservableAsPropertyHelper<TValue>(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            synchronizationContext
        );
    }
}

internal static class PropertyNotificationDispatcher
{
    private static readonly Action<
        ObservableObject,
        PropertyChangingEventArgs
    > PropertyChangingDelegate = CreateDelegate<PropertyChangingEventArgs>("OnPropertyChanging");

    private static readonly Action<
        ObservableObject,
        PropertyChangedEventArgs
    > PropertyChangedDelegate = CreateDelegate<PropertyChangedEventArgs>("OnPropertyChanged");

    public static void NotifyPropertyChanging(ObservableObject owner, string propertyName)
    {
        if (owner is IPropertyChangeNotifier notifier)
        {
            notifier.NotifyPropertyChanging(propertyName);
            return;
        }

        PropertyChangingDelegate(owner, new PropertyChangingEventArgs(propertyName));
    }

    public static void NotifyPropertyChanged(ObservableObject owner, string propertyName)
    {
        if (owner is IPropertyChangeNotifier notifier)
        {
            notifier.NotifyPropertyChanged(propertyName);
            return;
        }

        PropertyChangedDelegate(owner, new PropertyChangedEventArgs(propertyName));
    }

    private static Action<ObservableObject, TEventArgs> CreateDelegate<TEventArgs>(
        string methodName
    )
        where TEventArgs : EventArgs =>
        typeof(ObservableObject)
            .GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(TEventArgs)],
                modifiers: null
            )
            ?.CreateDelegate<Action<ObservableObject, TEventArgs>>()
        ?? throw new InvalidOperationException(
            $"{typeof(ObservableObject).FullName} does not expose {methodName}({typeof(TEventArgs).Name})."
        );
}
