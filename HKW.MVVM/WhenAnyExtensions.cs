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
    /// <b>REFLECTION: CONDITIONAL.</b> A direct property on an <see cref="IPropertyNotifier"/> is read through
    /// a compiled getter. Other sources and nested paths fall back to reflection-based path observation.
    /// </remarks>
    public static IObservable<TValue> WhenAnyValue<TSource, TValue>(
        this TSource source,
        Expression<Func<TSource, TValue>> property
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(property);

        if (source is IPropertyNotifier && PropertyPath.IsDirectProperty(property))
        {
            return new DirectPropertyObservable<TSource, TValue>(
                source,
                property.Compile(),
                property.GetPropertyName()
            );
        }

        return new PropertyPathObservable<TSource, TValue>(source, property);
    }

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
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> Each property delegates to the single-property
    /// <see cref="WhenAnyValue{TSource,TValue}(TSource, Expression{Func{TSource,TValue}})"/> implementation,
    /// which prefers <see cref="IPropertyNotifier"/> and falls back to reflection when necessary.
    /// </remarks>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Func<T1, T2, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            [source.WhenAnyValue(property1).Box(), source.WhenAnyValue(property2).Box()],
            values => selector((T1)values[0]!, (T2)values[1]!)
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
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> All properties delegate to the single-property
    /// <see cref="WhenAnyValue{TSource,TValue}(TSource, Expression{Func{TSource,TValue}})"/> implementation,
    /// which prefers <see cref="IPropertyNotifier"/> and falls back to reflection when necessary.
    /// </remarks>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, T3, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Func<T1, T2, T3, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            [
                source.WhenAnyValue(property1).Box(),
                source.WhenAnyValue(property2).Box(),
                source.WhenAnyValue(property3).Box(),
            ],
            values => selector((T1)values[0]!, (T2)values[1]!, (T3)values[2]!)
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
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> All properties delegate to the single-property
    /// <see cref="WhenAnyValue{TSource,TValue}(TSource, Expression{Func{TSource,TValue}})"/> implementation,
    /// which prefers <see cref="IPropertyNotifier"/> and falls back to reflection when necessary.
    /// </remarks>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, T3, T4, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Expression<Func<TSource, T4>> property4,
        Func<T1, T2, T3, T4, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            [
                source.WhenAnyValue(property1).Box(),
                source.WhenAnyValue(property2).Box(),
                source.WhenAnyValue(property3).Box(),
                source.WhenAnyValue(property4).Box(),
            ],
            values => selector((T1)values[0]!, (T2)values[1]!, (T3)values[2]!, (T4)values[3]!)
        );

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
    /// <see cref="IPropertyNotifier"/> and falls back to reflection when necessary.
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
        return Select(
            source.WhenAnyValue(property),
            value => selector(new PropertyObservation<TSource, TValue>(source, propertyName, value))
        );
    }

    private static IObservable<TResult> Select<TSource, TResult>(
        IObservable<TSource> source,
        Func<TSource, TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
            source.Subscribe(
                value =>
                {
                    try
                    {
                        observer.OnNext(selector(value));
                    }
                    catch (Exception exception)
                    {
                        observer.OnError(exception);
                    }
                },
                observer.OnError,
                observer.OnCompleted
            )
        );

    private static IObservable<TResult> Combine<TResult>(
        IReadOnlyList<IObservable<object?>> sources,
        Func<object?[], TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
        {
            var gate = new object();
            var values = new object?[sources.Count];
            var hasValue = new bool[sources.Count];
            var stopped = false;
            var subscriptions = new MultipleDisposable();

            for (var index = 0; index < sources.Count; index++)
            {
                var capturedIndex = index;
                subscriptions.Add(
                    sources[index]
                        .Subscribe(
                            value =>
                            {
                                TResult result;
                                lock (gate)
                                {
                                    if (stopped)
                                    {
                                        return;
                                    }

                                    values[capturedIndex] = value;
                                    hasValue[capturedIndex] = true;
                                    if (Array.IndexOf(hasValue, false) >= 0)
                                    {
                                        return;
                                    }

                                    try
                                    {
                                        result = selector((object?[])values.Clone());
                                    }
                                    catch (Exception exception)
                                    {
                                        stopped = true;
                                        observer.OnError(exception);
                                        subscriptions.Dispose();
                                        return;
                                    }
                                }

                                observer.OnNext(result);
                            },
                            error =>
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
                        )
                );
            }

            return subscriptions;
        });

    private static IObservable<object?> Box<T>(this IObservable<T> source) =>
        Select(source, value => (object?)value);

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
        private readonly Lock _gate = new();
        private readonly TSource _source;
        private readonly Func<TSource, TValue> _getter;
        private readonly string _propertyName;
        private readonly IObserver<TValue> _observer;
        private bool _hasValue;
        private TValue? _lastValue;
        private bool _disposed;

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
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
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
                    value = _getter(_source);
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
                catch (Exception exception)
                {
                    error = exception;
                    _disposed = true;
                    _source.PropertyChanged -= OnPropertyChanged;
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
    }

    private sealed class PropertyPathObservable<TSource, TValue>(
        TSource source,
        Expression<Func<TSource, TValue>> expression
    ) : IObservable<TValue>
        where TSource : class, INotifyPropertyChanged
    {
        private readonly PropertyInfo[] _path = PropertyPath.Parse(expression);

        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            return new PropertyPathSubscription<TSource, TValue>(source, _path, observer);
        }
    }

    private sealed class PropertyPathSubscription<TSource, TValue> : IDisposable
        where TSource : class, INotifyPropertyChanged
    {
        private readonly Lock _gate = new();
        private readonly TSource _source;
        private readonly PropertyInfo[] _path;
        private readonly IObserver<TValue> _observer;
        private readonly List<(
            INotifyPropertyChanged Owner,
            PropertyChangedEventHandler Handler
        )> _handlers = [];
        private bool _hasValue;
        private TValue? _lastValue;
        private bool _disposed;

        public PropertyPathSubscription(
            TSource source,
            PropertyInfo[] path,
            IObserver<TValue> observer
        )
        {
            _source = source;
            _path = path;
            _observer = observer;
            RebuildAndPublish();
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
                DetachHandlers();
            }
        }

        private void RebuildAndPublish()
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

                DetachHandlers();
                object? owner = _source;

                try
                {
                    for (var index = 0; index < _path.Length; index++)
                    {
                        if (owner is null)
                        {
                            return;
                        }

                        if (owner is INotifyPropertyChanged notifier)
                        {
                            var watchedName = _path[index].Name;
                            PropertyChangedEventHandler handler = (_, eventArgs) =>
                            {
                                if (
                                    string.IsNullOrEmpty(eventArgs.PropertyName)
                                    || eventArgs.PropertyName == watchedName
                                )
                                {
                                    RebuildAndPublish();
                                }
                            };
                            notifier.PropertyChanged += handler;
                            _handlers.Add((notifier, handler));
                        }

                        owner = _path[index].GetValue(owner);
                    }

                    value = (TValue?)owner;
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
                    DetachHandlers();
                }
                catch (Exception exception)
                {
                    error = exception;
                    _disposed = true;
                    DetachHandlers();
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

        private void DetachHandlers()
        {
            foreach (var (owner, handler) in _handlers)
            {
                owner.PropertyChanged -= handler;
            }

            _handlers.Clear();
        }
    }

    private static class PropertyPath
    {
        public static bool IsDirectProperty(LambdaExpression expression)
        {
            Expression body = expression.Body;
            if (body is UnaryExpression unary)
            {
                body = unary.Operand;
            }

            return body
                    is MemberExpression
                    {
                        Member: PropertyInfo { GetMethod: not null },
                        Expression: ParameterExpression parameter,
                    }
                && parameter == expression.Parameters[0];
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
