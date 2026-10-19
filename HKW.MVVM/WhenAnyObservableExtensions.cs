using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace HKW.MVVM;

#pragma warning disable S107, S2436, S3776
public static partial class WhenAnyExtensions
{
    /// <summary>
    /// 观察一个可观察序列属性, 并始终转发其最新序列的值
    /// </summary>
    /// <typeparam name="TSource">属性所有者类型</typeparam>
    /// <typeparam name="TValue">内部序列的值类型</typeparam>
    /// <param name="source">要观察的对象</param>
    /// <param name="property">返回可观察序列的属性路径</param>
    /// <returns>转发最新属性序列的冷可观察序列</returns>
    public static IObservable<TValue> WhenAnyObservable<TSource, TValue>(
        this TSource source,
        Expression<Func<TSource, IObservable<TValue>>> property
    )
        where TSource : class, INotifyPropertyChanged => source.WhenAnyValue(property).Switch();

    /// <summary>
    /// 观察两个可观察序列属性, 并发出其最新值的元组
    /// </summary>
    public static IObservable<(T1, T2)> WhenAnyObservable<TSource, T1, T2>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            static (value1, value2) => (value1, value2)
        );

    /// <summary>
    /// 观察两个可观察序列属性, 并投影其最新值
    /// </summary>
    public static IObservable<TResult> WhenAnyObservable<TSource, T1, T2, TResult>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Func<T1, T2, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            selector
        );
    }

    /// <summary>
    /// 观察三个可观察序列属性, 并发出其最新值的元组
    /// </summary>
    public static IObservable<(T1, T2, T3)> WhenAnyObservable<TSource, T1, T2, T3>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            static (value1, value2, value3) => (value1, value2, value3)
        );

    /// <summary>
    /// 观察三个可观察序列属性, 并投影其最新值
    /// </summary>
    public static IObservable<TResult> WhenAnyObservable<TSource, T1, T2, T3, TResult>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Func<T1, T2, T3, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            selector
        );
    }

    /// <summary>
    /// 观察四个可观察序列属性, 并发出其最新值的元组
    /// </summary>
    public static IObservable<(T1, T2, T3, T4)> WhenAnyObservable<TSource, T1, T2, T3, T4>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            static (value1, value2, value3, value4) => (value1, value2, value3, value4)
        );

    /// <summary>
    /// 观察四个可观察序列属性, 并投影其最新值
    /// </summary>
    public static IObservable<TResult> WhenAnyObservable<TSource, T1, T2, T3, T4, TResult>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Func<T1, T2, T3, T4, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            selector
        );
    }

    /// <summary>
    /// 观察 5 个可观察序列属性, 并发出最新值元组
    /// </summary>
    public static IObservable<(T1, T2, T3, T4, T5)> WhenAnyObservable<TSource, T1, T2, T3, T4, T5>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            static (value1, value2, value3, value4, value5) =>
                (value1, value2, value3, value4, value5)
        );

    /// <summary>
    /// 观察 5 个可观察序列属性, 并投影其最新值
    /// </summary>
    public static IObservable<TResult> WhenAnyObservable<TSource, T1, T2, T3, T4, T5, TResult>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5,
        Func<T1, T2, T3, T4, T5, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            selector
        );
    }

    /// <summary>
    /// 观察 6 个可观察序列属性, 并发出最新值元组
    /// </summary>
    public static IObservable<(T1, T2, T3, T4, T5, T6)> WhenAnyObservable<
        TSource,
        T1,
        T2,
        T3,
        T4,
        T5,
        T6
    >(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5,
        Expression<Func<TSource, IObservable<T6>>> property6
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            source.WhenAnyObservable(property6),
            static (value1, value2, value3, value4, value5, value6) =>
                (value1, value2, value3, value4, value5, value6)
        );

    /// <summary>
    /// 观察 6 个可观察序列属性, 并投影其最新值
    /// </summary>
    public static IObservable<TResult> WhenAnyObservable<TSource, T1, T2, T3, T4, T5, T6, TResult>(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5,
        Expression<Func<TSource, IObservable<T6>>> property6,
        Func<T1, T2, T3, T4, T5, T6, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            source.WhenAnyObservable(property6),
            selector
        );
    }

    /// <summary>
    /// 观察 7 个可观察序列属性, 并发出最新值元组
    /// </summary>
    public static IObservable<(T1, T2, T3, T4, T5, T6, T7)> WhenAnyObservable<
        TSource,
        T1,
        T2,
        T3,
        T4,
        T5,
        T6,
        T7
    >(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5,
        Expression<Func<TSource, IObservable<T6>>> property6,
        Expression<Func<TSource, IObservable<T7>>> property7
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            source.WhenAnyObservable(property6),
            source.WhenAnyObservable(property7),
            static (value1, value2, value3, value4, value5, value6, value7) =>
                (value1, value2, value3, value4, value5, value6, value7)
        );

    /// <summary>
    /// 观察 7 个可观察序列属性, 并投影其最新值
    /// </summary>
    public static IObservable<TResult> WhenAnyObservable<
        TSource,
        T1,
        T2,
        T3,
        T4,
        T5,
        T6,
        T7,
        TResult
    >(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5,
        Expression<Func<TSource, IObservable<T6>>> property6,
        Expression<Func<TSource, IObservable<T7>>> property7,
        Func<T1, T2, T3, T4, T5, T6, T7, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            source.WhenAnyObservable(property6),
            source.WhenAnyObservable(property7),
            selector
        );
    }

    /// <summary>
    /// 观察 8 个可观察序列属性, 并发出最新值元组
    /// </summary>
    public static IObservable<(T1, T2, T3, T4, T5, T6, T7, T8)> WhenAnyObservable<
        TSource,
        T1,
        T2,
        T3,
        T4,
        T5,
        T6,
        T7,
        T8
    >(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5,
        Expression<Func<TSource, IObservable<T6>>> property6,
        Expression<Func<TSource, IObservable<T7>>> property7,
        Expression<Func<TSource, IObservable<T8>>> property8
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            source.WhenAnyObservable(property6),
            source.WhenAnyObservable(property7),
            source.WhenAnyObservable(property8),
            static (value1, value2, value3, value4, value5, value6, value7, value8) =>
                (value1, value2, value3, value4, value5, value6, value7, value8)
        );

    /// <summary>
    /// 观察 8 个可观察序列属性, 并投影其最新值
    /// </summary>
    public static IObservable<TResult> WhenAnyObservable<
        TSource,
        T1,
        T2,
        T3,
        T4,
        T5,
        T6,
        T7,
        T8,
        TResult
    >(
        this TSource source,
        Expression<Func<TSource, IObservable<T1>>> property1,
        Expression<Func<TSource, IObservable<T2>>> property2,
        Expression<Func<TSource, IObservable<T3>>> property3,
        Expression<Func<TSource, IObservable<T4>>> property4,
        Expression<Func<TSource, IObservable<T5>>> property5,
        Expression<Func<TSource, IObservable<T6>>> property6,
        Expression<Func<TSource, IObservable<T7>>> property7,
        Expression<Func<TSource, IObservable<T8>>> property8,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyObservable(property1),
            source.WhenAnyObservable(property2),
            source.WhenAnyObservable(property3),
            source.WhenAnyObservable(property4),
            source.WhenAnyObservable(property5),
            source.WhenAnyObservable(property6),
            source.WhenAnyObservable(property7),
            source.WhenAnyObservable(property8),
            selector
        );
    }

    /// <summary>
    /// 观察属性路径, 并在更改前或更改后发出包含根对象、最终属性名和值的观察结果
    /// 嵌套节点被替换时会安全地重新绑定; 路径含空节点时暂停发出, 直到路径恢复
    /// </summary>
    /// <typeparam name="TSource">属性所有者类型</typeparam>
    /// <typeparam name="TValue">最终属性的值类型</typeparam>
    /// <param name="source">要观察的根对象</param>
    /// <param name="property">以根对象为起点的属性路径</param>
    /// <param name="beforeChange">为 <see langword="true"/> 时观察更改前通知, 否则观察更改后通知</param>
    /// <param name="skipInitial">是否跳过订阅时可用的当前值</param>
    /// <param name="isDistinct">是否抑制与上次发出值相等的结果</param>
    /// <param name="comparer">用于去重的比较器; 为 <see langword="null"/> 时使用默认比较器</param>
    /// <returns>属性观察结果构成的冷可观察序列</returns>
    public static IObservable<PropertyObservation<TSource, TValue>> ObservableForProperty<
        TSource,
        TValue
    >(
        this TSource source,
        Expression<Func<TSource, TValue>> property,
        bool beforeChange = false,
        bool skipInitial = true,
        bool isDistinct = true,
        IEqualityComparer<TValue>? comparer = null
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(property);

        var path = PropertyPath.Parse(property);
        return ObservableExtensions.WithLogger(
            new PropertyObservationObservable<TSource, TValue>(
                source,
                path,
                beforeChange,
                skipInitial,
                isDistinct,
                comparer ?? EqualityComparer<TValue>.Default
            ),
            source
        );
    }

    /// <summary>
    /// 观察属性更改前的值
    /// </summary>
    public static IObservable<PropertyObservation<TSource, TValue>> WhenAnyChanging<
        TSource,
        TValue
    >(
        this TSource source,
        Expression<Func<TSource, TValue>> property,
        bool skipInitial = true,
        bool isDistinct = true,
        IEqualityComparer<TValue>? comparer = null
    )
        where TSource : class, INotifyPropertyChanged =>
        source.ObservableForProperty(property, true, skipInitial, isDistinct, comparer);

    /// <summary>
    /// 观察属性更改后的值
    /// </summary>
    public static IObservable<PropertyObservation<TSource, TValue>> WhenAnyChanged<TSource, TValue>(
        this TSource source,
        Expression<Func<TSource, TValue>> property,
        bool skipInitial = true,
        bool isDistinct = true,
        IEqualityComparer<TValue>? comparer = null
    )
        where TSource : class, INotifyPropertyChanged =>
        source.ObservableForProperty(property, false, skipInitial, isDistinct, comparer);

    private sealed class PropertyObservationObservable<TSource, TValue>(
        TSource source,
        PropertyInfo[] path,
        bool beforeChange,
        bool skipInitial,
        bool isDistinct,
        IEqualityComparer<TValue> comparer
    ) : IObservable<PropertyObservation<TSource, TValue>>
        where TSource : class, INotifyPropertyChanged
    {
        public IDisposable Subscribe(IObserver<PropertyObservation<TSource, TValue>> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            return new PropertyObservationSubscription<TSource, TValue>(
                source,
                path,
                beforeChange,
                skipInitial,
                isDistinct,
                comparer,
                observer
            );
        }
    }

    private sealed class PropertyObservationSubscription<TSource, TValue> : IDisposable
        where TSource : class, INotifyPropertyChanged
    {
        private readonly
#if NET10_0_OR_GREATER
        Lock
#else
        object
#endif
        _gate = new();
        private readonly TSource _source;
        private readonly PropertyInfo[] _path;
        private readonly Func<object, object?>[] _getters;
        private readonly Func<object, TValue> _leafGetter;
        private readonly bool _beforeChange;
        private readonly bool _isDistinct;
        private readonly IEqualityComparer<TValue> _comparer;
        private readonly IObserver<PropertyObservation<TSource, TValue>> _observer;
        private readonly object?[] _owners;
        private readonly INotifyPropertyChanged?[] _changedNotifiers;
        private readonly INotifyPropertyChanging?[] _changingNotifiers;
        private readonly PropertyChangedEventHandler[] _changedHandlers;
        private readonly PropertyChangingEventHandler[] _changingHandlers;
        private readonly Queue<Action> _notifications = new();
        private bool _hasValue;
        private TValue? _lastValue;
        private bool _isDraining;
        private bool _disposed;

        public PropertyObservationSubscription(
            TSource source,
            PropertyInfo[] path,
            bool beforeChange,
            bool skipInitial,
            bool isDistinct,
            IEqualityComparer<TValue> comparer,
            IObserver<PropertyObservation<TSource, TValue>> observer
        )
        {
            _source = source;
            _path = path;
            _beforeChange = beforeChange;
            _isDistinct = isDistinct;
            _comparer = comparer;
            _observer = observer;
            _owners = new object?[path.Length];
            _changedNotifiers = new INotifyPropertyChanged?[path.Length];
            _changingNotifiers = new INotifyPropertyChanging?[path.Length];
            _changedHandlers = new PropertyChangedEventHandler[path.Length];
            _changingHandlers = new PropertyChangingEventHandler[path.Length];
            _getters = new Func<object, object?>[path.Length - 1];
            for (var index = 0; index < path.Length - 1; index++)
            {
                _getters[index] = PropertyPathGetterCache.Getters.Get(path[index]);
            }
            _leafGetter = PropertyPathLeafGetterCache<TValue>.Getters.Get(path[^1]);

            for (var index = 0; index < path.Length; index++)
            {
                var capturedIndex = index;
                _changedHandlers[index] = (_, args) => OnChanged(capturedIndex, args);
                _changingHandlers[index] = (_, args) => OnChanging(capturedIndex, args);
            }

            RebuildAndMaybePublish(0, publish: !skipInitial);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _notifications.Clear();
                DetachFrom(0);
            }
        }

        private void OnChanging(int index, PropertyChangingEventArgs args)
        {
            if (!_beforeChange || !Matches(index, args.PropertyName))
                return;
            PublishCurrent();
        }

        private void OnChanged(int index, PropertyChangedEventArgs args)
        {
            if (!Matches(index, args.PropertyName))
                return;
            RebuildAndMaybePublish(index, publish: !_beforeChange);
        }

        private bool Matches(int index, string? propertyName) =>
            string.IsNullOrEmpty(propertyName) || propertyName == _path[index].Name;

        private void RebuildAndMaybePublish(int changedIndex, bool publish)
        {
            var shouldDrain = false;
            lock (_gate)
            {
                if (_disposed)
                    return;
                try
                {
                    object? owner = changedIndex == 0 ? _source : _owners[changedIndex];
                    DetachFrom(changedIndex + 1);
                    TValue? value = default;
                    for (var index = changedIndex; index < _path.Length; index++)
                    {
                        if (owner is null)
                            return;
                        if (index > changedIndex || _owners[index] is null)
                        {
                            _owners[index] = owner;
                            Attach(index, owner);
                        }
                        if (index == _path.Length - 1)
                            value = _leafGetter(owner);
                        else
                            owner = _getters[index](owner);
                    }
                    if (publish && Accept(value!))
                    {
                        var observation = new PropertyObservation<TSource, TValue>(
                            _source,
                            _path[^1].Name,
                            value!
                        );
                        shouldDrain = Enqueue(() => _observer.OnNext(observation));
                    }
                }
                catch (TargetInvocationException exception)
                {
                    var error = exception.InnerException ?? exception;
                    Stop();
                    shouldDrain = Enqueue(() => _observer.OnError(error));
                }
                catch (Exception exception)
                {
                    Stop();
                    shouldDrain = Enqueue(() => _observer.OnError(exception));
                }
            }
            if (shouldDrain)
                DrainNotifications();
        }

        private void PublishCurrent()
        {
            var shouldDrain = false;
            lock (_gate)
            {
                if (_disposed)
                    return;
                try
                {
                    var owner = _owners[^1];
                    if (owner is null)
                        return;
                    var value = _leafGetter(owner);
                    if (Accept(value))
                    {
                        var observation = new PropertyObservation<TSource, TValue>(
                            _source,
                            _path[^1].Name,
                            value
                        );
                        shouldDrain = Enqueue(() => _observer.OnNext(observation));
                    }
                }
                catch (TargetInvocationException exception)
                {
                    var error = exception.InnerException ?? exception;
                    Stop();
                    shouldDrain = Enqueue(() => _observer.OnError(error));
                }
                catch (Exception exception)
                {
                    Stop();
                    shouldDrain = Enqueue(() => _observer.OnError(exception));
                }
            }
            if (shouldDrain)
                DrainNotifications();
        }

        private bool Enqueue(Action notification)
        {
            _notifications.Enqueue(notification);
            if (_isDraining)
                return false;
            _isDraining = true;
            return true;
        }

        private void DrainNotifications()
        {
            while (true)
            {
                Action notification;
                lock (_gate)
                {
                    if (_notifications.Count == 0)
                    {
                        _isDraining = false;
                        return;
                    }
                    notification = _notifications.Dequeue();
                }

                try
                {
                    notification();
                }
                catch
                {
                    lock (_gate)
                    {
                        _notifications.Clear();
                        _isDraining = false;
                        if (!_disposed)
                            Stop();
                    }
                    throw;
                }
            }
        }

        private bool Accept(TValue value)
        {
            if (_isDistinct && _hasValue && _comparer.Equals(_lastValue!, value))
                return false;
            _hasValue = true;
            _lastValue = value;
            return true;
        }

        private void Attach(int index, object owner)
        {
            if (owner is INotifyPropertyChanged changed)
            {
                changed.PropertyChanged += _changedHandlers[index];
                _changedNotifiers[index] = changed;
            }
            if (_beforeChange && owner is INotifyPropertyChanging changing)
            {
                changing.PropertyChanging += _changingHandlers[index];
                _changingNotifiers[index] = changing;
            }
        }

        private void Stop()
        {
            _disposed = true;
            DetachFrom(0);
        }

        private void DetachFrom(int startIndex)
        {
            for (var index = startIndex; index < _path.Length; index++)
            {
                if (_changedNotifiers[index] is { } changed)
                    changed.PropertyChanged -= _changedHandlers[index];
                if (_changingNotifiers[index] is { } changing)
                    changing.PropertyChanging -= _changingHandlers[index];
                _changedNotifiers[index] = null;
                _changingNotifiers[index] = null;
                _owners[index] = null;
            }
        }
    }
}
#pragma warning restore S107, S2436, S3776
