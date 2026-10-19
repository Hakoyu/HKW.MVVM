using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM;

/// <summary>
/// 存储可观察序列的最新值,并为只读的拥有者属性引发通知
/// </summary>
public sealed class ObservableAsPropertyHelper<T>
    : IDisposable,
        INotifyPropertyChanged,
        INotifyPropertyChanging
{
    private enum SubscriptionState : byte
    {
        NotSubscribed,
        Subscribing,
        Subscribed,
    }

    private readonly Lock _gate = new();
    private readonly IObservable<T> _source;
    private readonly ObservableObject _owner;

    private readonly SynchronizationContext? _synchronizationContext;
    private readonly bool _scheduleOnThreadPool;
    private readonly SerialActionQueue? _notificationQueue;
    private readonly ExceptionSubject _exceptions = new();
    private readonly PropertyChangingEventArgs _valueChangingEventArgs = new(nameof(Value));
    private readonly PropertyChangedEventArgs _valueChangedEventArgs = new(nameof(Value));
    private readonly PropertyChangingEventArgs _ownerChangingEventArgs;
    private readonly PropertyChangedEventArgs _ownerChangedEventArgs;
    private IDisposable? _subscription;
    private T _value;
    private T _lastDistinctValue;
    private bool _hasLastDistinctValue;
    private SubscriptionState _subscriptionState;
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
        PropertyName = propertyName;
        _value = initialValue;
        _lastDistinctValue = initialValue;
        _hasLastDistinctValue = true;
        _ownerChangingEventArgs = new PropertyChangingEventArgs(PropertyName);
        _ownerChangedEventArgs = new PropertyChangedEventArgs(PropertyName);
        _synchronizationContext = synchronizationContext;
        _notificationQueue = synchronizationContext is null
            ? null
            : new SerialActionQueue(action =>
                synchronizationContext.Post(static state => ((Action)state!).Invoke(), action)
            );

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
        PropertyName = propertyName;
        _value = initialValue;
        _lastDistinctValue = initialValue;
        _hasLastDistinctValue = true;
        _ownerChangingEventArgs = new PropertyChangingEventArgs(propertyName);
        _ownerChangedEventArgs = new PropertyChangedEventArgs(propertyName);

        (_synchronizationContext, _scheduleOnThreadPool) = scheduler switch
        {
            ObservableSchedulers.Current => (
                SynchronizationContext.Current,
                SynchronizationContext.Current is null
            ),
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
                ThreadPool.QueueUserWorkItem(
                    static state => state.Invoke(),
                    action,
                    preferLocal: false
                );
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
    /// 在 <see cref="Value"/> 变更之前引发
    /// </summary>
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>
    /// 在 <see cref="Value"/> 变更之后引发
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 获取从源可观察序列接收的最新值
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
    /// 目标属性名
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 接收源产生的终止性错误
    /// </summary>
    public IObservable<Exception> ThrownExceptions => _exceptions;

    /// <summary>
    /// 获取一个值,指示是否已开始订阅源序列
    /// </summary>
    public bool IsSubscribed
    {
        get
        {
            lock (_gate)
            {
                return _subscriptionState is not SubscriptionState.NotSubscribed;
            }
        }
    }

    /// <summary>
    /// 停止观察源并释放所有已占用的资源
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
            _subscriptionState = SubscriptionState.NotSubscribed;
            subscription = _subscription;
            _subscription = null;
        }

        subscription?.Dispose();
        _notificationQueue?.Dispose();
        _exceptions.Dispose();
    }

    private void EnsureSubscribed()
    {
        IDisposable? subscription = null;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_subscriptionState is not SubscriptionState.NotSubscribed)
            {
                return;
            }

            _subscriptionState = SubscriptionState.Subscribing;
        }

        try
        {
            subscription = _source.Subscribe(SetValue, _exceptions.OnNext);
        }
        catch
        {
            lock (_gate)
            {
                if (_disposed is false && _subscriptionState == SubscriptionState.Subscribing)
                {
                    _subscriptionState = SubscriptionState.NotSubscribed;
                }
            }

            throw;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                _subscriptionState = SubscriptionState.NotSubscribed;
            }
            else
            {
                _subscription = subscription;
                _subscriptionState = SubscriptionState.Subscribed;
                subscription = null;
            }
        }

        subscription?.Dispose();
    }

    private void SetValue(T value)
    {
        if (TryAcceptValue(value) is false)
        {
            return;
        }

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

    private bool TryAcceptValue(T value)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            if (
                _hasLastDistinctValue
                && EqualityComparer<T>.Default.Equals(_lastDistinctValue, value)
            )
            {
                return false;
            }

            _lastDistinctValue = value;
            _hasLastDistinctValue = true;
            return true;
        }
    }

    private void SetValueCore(T value)
    {
        PropertyChangingEventHandler? propertyChanging;
        PropertyChangedEventHandler? propertyChanged;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            propertyChanging = PropertyChanging;
            propertyChanged = PropertyChanged;
        }

        propertyChanging?.Invoke(this, _valueChangingEventArgs);
        PropertyNotificationDispatcher.NotifyPropertyChanging(_owner, _ownerChangingEventArgs);

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _value = value;
        }

        propertyChanged?.Invoke(this, _valueChangedEventArgs);
        PropertyNotificationDispatcher.NotifyPropertyChanged(_owner, _ownerChangedEventArgs);
    }
}

internal static class PropertyNotificationDispatcher
{
    public static void NotifyPropertyChanging(
        ObservableObject owner,
        PropertyChangingEventArgs args
    )
    {
        if (owner is IPropertyChangeNotifier notifier)
        {
            notifier.NotifyPropertyChanging(args.PropertyName);
            return;
        }

        OnPropertyChanging(owner, args);
    }

    public static void NotifyPropertyChanged(ObservableObject owner, PropertyChangedEventArgs args)
    {
        if (owner is IPropertyChangeNotifier notifier)
        {
            notifier.NotifyPropertyChanged(args.PropertyName);
            return;
        }

        OnPropertyChanged(owner, args);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnPropertyChanging")]
    private static extern void OnPropertyChanging(
        ObservableObject owner,
        PropertyChangingEventArgs args
    );

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnPropertyChanged")]
    private static extern void OnPropertyChanged(
        ObservableObject owner,
        PropertyChangedEventArgs args
    );
}
