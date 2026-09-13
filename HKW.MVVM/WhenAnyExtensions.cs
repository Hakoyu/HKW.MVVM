using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace HKW.MVVM;

/// <summary>
/// A property observation containing the object, property name, and current value.
/// </summary>
public readonly record struct PropertyObservation<TSender, TValue>(
    TSender Sender,
    string PropertyName,
    TValue Value
);

/// <summary>
/// Converts <see cref="INotifyPropertyChanged"/> properties into cold observable streams.
/// </summary>
public static class WhenAnyExtensions
{
    /// <summary>
    /// Observes one property path and emits its current value on subscription followed by distinct changes.
    /// Nested paths are rebound when an intermediate object changes; an unavailable path is suppressed until it recovers.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="TValue">The final property value type.</typeparam>
    /// <param name="source">The source object whose property is observed.</param>
    /// <param name="property">A property path rooted at <paramref name="source"/>, such as <c>x =&gt; x.Address.City</c>.</param>
    /// <returns>A cold observable sequence of final property values.</returns>
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> A direct property is read through a compiled getter.
    /// Nested paths fall back to reflection-based path observation and rebinding.
    /// </remarks>
    public static IObservable<TValue> WhenAnyValue<TSource, TValue>(
        this TSource source,
        Expression<Func<TSource, TValue>> property
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(property);

        // A direct property only needs INotifyPropertyChanged. Keep nested paths on the
        // rebinding implementation, but avoid reflection and handler reconstruction for
        // the overwhelmingly common single-property case.
        if (PropertyPath.TryGetDirectProperty(property, out var directProperty))
        {
            return new DirectPropertyObservable<TSource, TValue>(
                source,
                DirectPropertyGetterCache<TSource, TValue>.Getters.Get(directProperty),
                directProperty.Name
            );
        }

        return new PropertyPathObservable<TSource, TValue>(source, property);
    }

    /// <summary>
    /// Observes two properties and emits their latest values as a tuple whenever either final value changes.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="T1">The first property value type.</typeparam>
    /// <typeparam name="T2">The second property value type.</typeparam>
    /// <param name="source">The source object whose properties are observed.</param>
    /// <param name="property1">The first property path.</param>
    /// <param name="property2">The second property path.</param>
    /// <returns>A cold observable sequence of tuples containing the latest property values.</returns>
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> Each property delegates to the single-property
    /// <see cref="WhenAnyValue{TSource,TValue}(TSource, Expression{Func{TSource,TValue}})"/> implementation,
    /// which uses a compiled getter for direct properties and falls back to reflection for nested paths.
    /// </remarks>
    public static IObservable<(T1, T2)> WhenAnyValue<TSource, T1, T2>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            static (value1, value2) => (value1, value2)
        );

    /// <summary>
    /// Observes two properties and projects their latest values whenever either final value changes.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="T1">The first property value type.</typeparam>
    /// <typeparam name="T2">The second property value type.</typeparam>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="source">The source object whose properties are observed.</param>
    /// <param name="property1">The first property path.</param>
    /// <param name="property2">The second property path.</param>
    /// <param name="selector">The function that combines the latest property values.</param>
    /// <returns>A cold observable sequence of projected results.</returns>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Func<T1, T2, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(source.WhenAnyValue(property1), source.WhenAnyValue(property2), selector);
    }

    /// <summary>
    /// Observes three properties and emits their latest values as a tuple whenever any final value changes.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="T1">The first property value type.</typeparam>
    /// <typeparam name="T2">The second property value type.</typeparam>
    /// <typeparam name="T3">The third property value type.</typeparam>
    /// <param name="source">The source object whose properties are observed.</param>
    /// <param name="property1">The first property path.</param>
    /// <param name="property2">The second property path.</param>
    /// <param name="property3">The third property path.</param>
    /// <returns>A cold observable sequence of tuples containing the latest property values.</returns>
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> All properties delegate to the single-property
    /// <see cref="WhenAnyValue{TSource,TValue}(TSource, Expression{Func{TSource,TValue}})"/> implementation,
    /// which uses a compiled getter for direct properties and falls back to reflection for nested paths.
    /// </remarks>
    public static IObservable<(T1, T2, T3)> WhenAnyValue<TSource, T1, T2, T3>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            static (value1, value2, value3) => (value1, value2, value3)
        );

    /// <summary>
    /// Observes three properties and projects their latest values whenever any final value changes.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="T1">The first property value type.</typeparam>
    /// <typeparam name="T2">The second property value type.</typeparam>
    /// <typeparam name="T3">The third property value type.</typeparam>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="source">The source object whose properties are observed.</param>
    /// <param name="property1">The first property path.</param>
    /// <param name="property2">The second property path.</param>
    /// <param name="property3">The third property path.</param>
    /// <param name="selector">The function that combines the latest property values.</param>
    /// <returns>A cold observable sequence of projected results.</returns>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, T3, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Func<T1, T2, T3, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            selector
        );
    }

    /// <summary>
    /// Observes four properties and emits their latest values as a tuple whenever any final value changes.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="T1">The first property value type.</typeparam>
    /// <typeparam name="T2">The second property value type.</typeparam>
    /// <typeparam name="T3">The third property value type.</typeparam>
    /// <typeparam name="T4">The fourth property value type.</typeparam>
    /// <param name="source">The source object whose properties are observed.</param>
    /// <param name="property1">The first property path.</param>
    /// <param name="property2">The second property path.</param>
    /// <param name="property3">The third property path.</param>
    /// <param name="property4">The fourth property path.</param>
    /// <returns>A cold observable sequence of tuples containing the latest property values.</returns>
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> All properties delegate to the single-property
    /// <see cref="WhenAnyValue{TSource,TValue}(TSource, Expression{Func{TSource,TValue}})"/> implementation,
    /// which uses a compiled getter for direct properties and falls back to reflection for nested paths.
    /// </remarks>
    public static IObservable<(T1, T2, T3, T4)> WhenAnyValue<TSource, T1, T2, T3, T4>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Expression<Func<TSource, T4>> property4
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            source.WhenAnyValue(property4),
            static (value1, value2, value3, value4) => (value1, value2, value3, value4)
        );

    /// <summary>
    /// Observes four properties and projects their latest values whenever any final value changes.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="T1">The first property value type.</typeparam>
    /// <typeparam name="T2">The second property value type.</typeparam>
    /// <typeparam name="T3">The third property value type.</typeparam>
    /// <typeparam name="T4">The fourth property value type.</typeparam>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="source">The source object whose properties are observed.</param>
    /// <param name="property1">The first property path.</param>
    /// <param name="property2">The second property path.</param>
    /// <param name="property3">The third property path.</param>
    /// <param name="property4">The fourth property path.</param>
    /// <param name="selector">The function that combines the latest property values.</param>
    /// <returns>A cold observable sequence of projected results.</returns>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, T3, T4, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Expression<Func<TSource, T4>> property4,
        Func<T1, T2, T3, T4, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            source.WhenAnyValue(property4),
            selector
        );
    }

    /// <summary>
    /// Observes a property path and projects an observation containing the sender, final property name, and value.
    /// </summary>
    /// <typeparam name="TSource">The notifying source type.</typeparam>
    /// <typeparam name="TValue">The final property value type.</typeparam>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="source">The source object whose property is observed.</param>
    /// <param name="property">The property path to observe.</param>
    /// <param name="selector">The function that projects each property observation.</param>
    /// <returns>A cold observable sequence of projected observations.</returns>
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> This method delegates value observation to
    /// <see cref="WhenAnyValue{TSource,TValue}(TSource, Expression{Func{TSource,TValue}})"/>, which prefers
    /// a compiled getter for direct properties and falls back to reflection for nested paths.
    /// </remarks>
    public static IObservable<TResult> WhenAny<TSource, TValue, TResult>(
        this TSource source,
        Expression<Func<TSource, TValue>> property,
        Func<PropertyObservation<TSource, TValue>, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        var propertyName = property.GetPropertyName();
        return ObservableExtensions.Select(
            source.WhenAnyValue(property),
            value => selector(new PropertyObservation<TSource, TValue>(source, propertyName, value))
        );
    }

    private static IObservable<TResult> Combine<T1, T2, TResult>(
        IObservable<T1> source1,
        IObservable<T2> source2,
        Func<T1, T2, TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
        {
            var gate = new Lock();
            var value1 = default(T1)!;
            var value2 = default(T2)!;
            var hasValue1 = false;
            var hasValue2 = false;
            var stopped = false;
            var completedSources = 0;
            var subscriptions = new MultipleDisposable();

            void Publish()
            {
                T1 current1;
                T2 current2;
                lock (gate)
                {
                    if (stopped || hasValue1 is false || hasValue2 is false)
                    {
                        return;
                    }

                    current1 = value1;
                    current2 = value2;
                }

                try
                {
                    observer.OnNext(selector(current1, current2));
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            }

            void Fail(Exception error)
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                }

                observer.OnError(error);
                subscriptions.Dispose();
            }

            void Complete()
            {
                var shouldComplete = false;
                lock (gate)
                {
                    if (stopped) return;
                    completedSources++;
                    shouldComplete = completedSources == 2;
                    if (shouldComplete) stopped = true;
                }
                if (shouldComplete)
                {
                    observer.OnCompleted();
                    subscriptions.Dispose();
                }
            }
            subscriptions.Add(
                source1.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value1 = value;
                            hasValue1 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source2.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value2 = value;
                            hasValue2 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );

            return subscriptions;
        });

    private static IObservable<TResult> Combine<T1, T2, T3, TResult>(
        IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        Func<T1, T2, T3, TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
        {
            var gate = new Lock();
            var value1 = default(T1)!;
            var value2 = default(T2)!;
            var value3 = default(T3)!;
            var hasValue1 = false;
            var hasValue2 = false;
            var hasValue3 = false;
            var stopped = false;
            var completedSources = 0;
            var subscriptions = new MultipleDisposable();

            void Publish()
            {
                T1 current1;
                T2 current2;
                T3 current3;
                lock (gate)
                {
                    if (stopped || hasValue1 is false || hasValue2 is false || hasValue3 is false)
                    {
                        return;
                    }

                    current1 = value1;
                    current2 = value2;
                    current3 = value3;
                }

                try
                {
                    observer.OnNext(selector(current1, current2, current3));
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            }

            void Fail(Exception error)
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                }

                observer.OnError(error);
                subscriptions.Dispose();
            }

            void Complete()
            {
                var shouldComplete = false;
                lock (gate)
                {
                    if (stopped) return;
                    completedSources++;
                    shouldComplete = completedSources == 3;
                    if (shouldComplete) stopped = true;
                }
                if (shouldComplete)
                {
                    observer.OnCompleted();
                    subscriptions.Dispose();
                }
            }
            subscriptions.Add(
                source1.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value1 = value;
                            hasValue1 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source2.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value2 = value;
                            hasValue2 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source3.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value3 = value;
                            hasValue3 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );

            return subscriptions;
        });

    private static IObservable<TResult> Combine<T1, T2, T3, T4, TResult>(
        IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        IObservable<T4> source4,
        Func<T1, T2, T3, T4, TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
        {
            var gate = new Lock();
            var value1 = default(T1)!;
            var value2 = default(T2)!;
            var value3 = default(T3)!;
            var value4 = default(T4)!;
            var hasValue1 = false;
            var hasValue2 = false;
            var hasValue3 = false;
            var hasValue4 = false;
            var stopped = false;
            var completedSources = 0;
            var subscriptions = new MultipleDisposable();

            void Publish()
            {
                T1 current1;
                T2 current2;
                T3 current3;
                T4 current4;
                lock (gate)
                {
                    if (
                        stopped
                        || hasValue1 is false
                        || hasValue2 is false
                        || hasValue3 is false
                        || hasValue4 is false
                    )
                    {
                        return;
                    }

                    current1 = value1;
                    current2 = value2;
                    current3 = value3;
                    current4 = value4;
                }

                try
                {
                    observer.OnNext(selector(current1, current2, current3, current4));
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            }

            void Fail(Exception error)
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                }

                observer.OnError(error);
                subscriptions.Dispose();
            }

            void Complete()
            {
                var shouldComplete = false;
                lock (gate)
                {
                    if (stopped) return;
                    completedSources++;
                    shouldComplete = completedSources == 4;
                    if (shouldComplete) stopped = true;
                }
                if (shouldComplete)
                {
                    observer.OnCompleted();
                    subscriptions.Dispose();
                }
            }
            subscriptions.Add(
                source1.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value1 = value;
                            hasValue1 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source2.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value2 = value;
                            hasValue2 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source3.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value3 = value;
                            hasValue3 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source4.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value4 = value;
                            hasValue4 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );

            return subscriptions;
        });

    private static class DirectPropertyGetterCache<TSource, TValue>
    {
        public static readonly MemoizingLRUCache<PropertyInfo, Func<TSource, TValue>> Getters = new(
            Compile,
            64
        );

        private static Func<TSource, TValue> Compile(PropertyInfo property)
        {
            var source = Expression.Parameter(typeof(TSource), "source");
            Expression body = Expression.Property(source, property);
            if (body.Type != typeof(TValue))
            {
                body = Expression.Convert(body, typeof(TValue));
            }

            return Expression.Lambda<Func<TSource, TValue>>(body, source).Compile();
        }
    }

    private static class PropertyPathLeafGetterCache<TValue>
    {
        public static readonly MemoizingLRUCache<PropertyInfo, Func<object, TValue>> Getters = new(
            Compile,
            64
        );

        private static Func<object, TValue> Compile(PropertyInfo property)
        {
            var owner = Expression.Parameter(typeof(object), "owner");
            Expression body = Expression.Property(
                Expression.Convert(owner, property.DeclaringType!),
                property
            );
            if (body.Type != typeof(TValue))
            {
                body = Expression.Convert(body, typeof(TValue));
            }

            return Expression.Lambda<Func<object, TValue>>(body, owner).Compile();
        }
    }

    private sealed class DirectPropertyObservable<TSource, TValue>(
        TSource source,
        Func<TSource, TValue> getter,
        string propertyName
    ) : IObservable<TValue>
        where TSource : class, INotifyPropertyChanged
    {
        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            return new DirectPropertySubscription<TSource, TValue>(
                source,
                getter,
                propertyName,
                observer
            );
        }
    }

    private sealed class DirectPropertySubscription<TSource, TValue> : IDisposable
        where TSource : class, INotifyPropertyChanged
    {
        private readonly TSource _source;
        private readonly Func<TSource, TValue> _getter;
        private readonly string _propertyName;
        private readonly IObserver<TValue> _observer;
        private bool _hasValue;
        private TValue? _lastValue;
        private int _disposed;

        public DirectPropertySubscription(
            TSource source,
            Func<TSource, TValue> getter,
            string propertyName,
            IObserver<TValue> observer
        )
        {
            _source = source;
            _getter = getter;
            _propertyName = propertyName;
            _observer = observer;
            _source.PropertyChanged += OnPropertyChanged;
            PublishCurrentValue();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _source.PropertyChanged -= OnPropertyChanged;
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == _propertyName
            )
            {
                PublishCurrentValue();
            }
        }

        private void PublishCurrentValue()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            TValue value;
            try
            {
                value = _getter(_source);
            }
            catch (Exception exception)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _source.PropertyChanged -= OnPropertyChanged;
                    _observer.OnError(exception);
                }

                return;
            }

            if (_hasValue && EqualityComparer<TValue>.Default.Equals(_lastValue!, value))
            {
                return;
            }

            _hasValue = true;
            _lastValue = value;
            _observer.OnNext(value);
        }
    }

    private sealed class PropertyPathObservable<TSource, TValue> : IObservable<TValue>
        where TSource : class, INotifyPropertyChanged
    {
        private readonly TSource _source;
        private readonly PropertyInfo[] _path;
        private readonly Func<object, TValue> _leafGetter;

        public PropertyPathObservable(TSource source, Expression<Func<TSource, TValue>> expression)
        {
            _source = source;
            _path = PropertyPath.Parse(expression);
            _leafGetter = PropertyPathLeafGetterCache<TValue>.Getters.Get(_path[^1]);
        }

        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            return new PropertyPathSubscription<TSource, TValue>(
                _source,
                _path,
                _leafGetter,
                observer
            );
        }
    }

    private sealed class PropertyPathSubscription<TSource, TValue> : IDisposable
        where TSource : class, INotifyPropertyChanged
    {
        private readonly Lock _gate = new();
        private readonly TSource _source;
        private readonly PropertyInfo[] _path;
        private readonly Func<object, TValue> _leafGetter;
        private readonly IObserver<TValue> _observer;
        private readonly object?[] _owners;
        private readonly INotifyPropertyChanged?[] _notifiers;
        private readonly PropertyChangedEventHandler[] _handlers;
        private bool _hasValue;
        private TValue? _lastValue;
        private bool _disposed;

        public PropertyPathSubscription(
            TSource source,
            PropertyInfo[] path,
            Func<object, TValue> leafGetter,
            IObserver<TValue> observer
        )
        {
            _source = source;
            _path = path;
            _leafGetter = leafGetter;
            _observer = observer;
            _owners = new object?[path.Length];
            _notifiers = new INotifyPropertyChanged?[path.Length];
            _handlers = new PropertyChangedEventHandler[path.Length];
            for (var index = 0; index < path.Length; index++)
            {
                var capturedIndex = index;
                _handlers[index] = (sender, eventArgs) =>
                    OnPropertyChanged(capturedIndex, eventArgs);
            }

            RebuildAndPublish(0);
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
                DetachHandlersFrom(0);
            }
        }

        private void OnPropertyChanged(int pathIndex, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == _path[pathIndex].Name
            )
            {
                RebuildAndPublish(pathIndex);
            }
        }

        private void RebuildAndPublish(int changedPathIndex)
        {
            TValue? value = default;
            Exception? error = null;
            var publish = false;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    object? owner;
                    if (changedPathIndex == 0 && _owners[0] is null)
                    {
                        owner = _source;
                    }
                    else
                    {
                        owner = _owners[changedPathIndex];
                    }

                    DetachHandlersFrom(changedPathIndex + 1);
                    for (var index = changedPathIndex; index < _path.Length; index++)
                    {
                        if (owner is null)
                        {
                            return;
                        }

                        if (index > changedPathIndex || _owners[index] is null)
                        {
                            _owners[index] = owner;
                            if (owner is INotifyPropertyChanged notifier)
                            {
                                notifier.PropertyChanged += _handlers[index];
                                _notifiers[index] = notifier;
                            }
                        }

                        if (index == _path.Length - 1)
                        {
                            value = _leafGetter(owner);
                        }
                        else
                        {
                            owner = _path[index].GetValue(owner);
                        }
                    }

                    if (
                        _hasValue is false
                        || EqualityComparer<TValue>.Default.Equals(_lastValue!, value!) is false
                    )
                    {
                        _hasValue = true;
                        _lastValue = value;
                        publish = true;
                    }
                }
                catch (TargetInvocationException exception)
                {
                    error = exception.InnerException ?? exception;
                    _disposed = true;
                    DetachHandlersFrom(0);
                }
                catch (Exception exception)
                {
                    error = exception;
                    _disposed = true;
                    DetachHandlersFrom(0);
                }
            }

            if (error is not null)
            {
                _observer.OnError(error);
            }
            else if (publish)
            {
                _observer.OnNext(value!);
            }
        }

        private void DetachHandlersFrom(int startIndex)
        {
            for (var index = startIndex; index < _path.Length; index++)
            {
                var notifier = _notifiers[index];
                if (notifier is not null)
                {
                    notifier.PropertyChanged -= _handlers[index];
                    _notifiers[index] = null;
                }

                _owners[index] = null;
            }
        }
    }

    private static class PropertyPath
    {
        public static bool TryGetDirectProperty(
            LambdaExpression expression,
            out PropertyInfo property
        )
        {
            Expression body = expression.Body;
            if (
                body is UnaryExpression
                {
                    NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
                } unary
            )
            {
                body = unary.Operand;
            }

            if (
                body
                    is MemberExpression
                    {
                        Member: PropertyInfo { GetMethod: not null } directProperty,
                        Expression: ParameterExpression parameter,
                    }
                && parameter == expression.Parameters[0]
            )
            {
                property = directProperty;
                return true;
            }

            property = null!;
            return false;
        }

        public static PropertyInfo[] Parse<TSource, TValue>(
            Expression<Func<TSource, TValue>> expression
        )
        {
            ArgumentNullException.ThrowIfNull(expression);
            Expression current = expression.Body;
            if (
                current is UnaryExpression
                {
                    NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
                } conversion
            )
            {
                current = conversion.Operand;
            }

            var path = new Stack<PropertyInfo>();
            while (current is MemberExpression memberExpression)
            {
                if (
                    memberExpression.Member is not PropertyInfo property
                    || property.GetMethod is null
                )
                {
                    throw new ArgumentException(
                        "The expression must contain readable properties only.",
                        nameof(expression)
                    );
                }

                path.Push(property);
                current = memberExpression.Expression!;
            }

            if (current != expression.Parameters[0] || path.Count == 0)
            {
                throw new ArgumentException(
                    "The expression must be a property path rooted at its parameter, for example x => x.Customer.Name.",
                    nameof(expression)
                );
            }

            return path.ToArray();
        }
    }
}
