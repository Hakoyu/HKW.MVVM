using System.Linq.Expressions;
using System.Reflection;

namespace HKW.MVVM;

/// <summary>
/// 可观察序列扩展的共享实现细节
/// </summary>
public static partial class ObservableExtensions
{
    private static readonly IDisposable _emptyDisposable = new ActionDisposable(static () => { });

    internal static IObservable<T> WithLogger<T>(IObservable<T> observable, object source) =>
        source is IEnableLogger loggerOwner
            ? new ObservableWithLogger<T>(observable, () => loggerOwner.Log())
            : observable;

    internal static IObservable<TResult> PreserveLogger<TSource, TResult>(
        IObservable<TSource> source,
        IObservable<TResult> result
    ) =>
        source is IObservableWithLogger observableWithLogger
            ? new ObservableWithLogger<TResult>(result, observableWithLogger.GetLogger)
            : result;

    private static IObservable<T> CreateCore<T>(Func<IObserver<T>, IDisposable> subscribe) =>
        new AnonymousObservable<T>(subscribe);

    private static IObservable<TSource> CreateCore<TSource>(
        IObservable<TSource> source,
        Func<IObserver<TSource>, IDisposable> subscribe
    ) => PreserveLogger(source, CreateCore(subscribe));

    private static IObservable<TSource> SubscribeOnCore<TSource>(
        IObservable<TSource> source,
        SynchronizationContext? synchronizationContext
    ) =>
        CreateCore(
            source,
            observer =>
            {
                var scheduledSubscription = new ScheduledSubscription();
                Schedule(
                    synchronizationContext,
                    () =>
                    {
                        if (scheduledSubscription.IsDisposed)
                        {
                            return;
                        }

                        try
                        {
                            var subscription = source.Subscribe(observer);
                            scheduledSubscription.SetSubscription(subscription);
                        }
                        catch (Exception exception)
                        {
                            if (scheduledSubscription.IsDisposed is false)
                            {
                                observer.OnError(exception);
                            }

                            scheduledSubscription.Dispose();
                        }
                    }
                );
                return scheduledSubscription;
            }
        );

    private static void Post(SynchronizationContext context, Action action) =>
        context.Post(static state => ((Action)state!).Invoke(), action);

    private static SynchronizationContext? GetSynchronizationContext(
        ObservableSchedulers scheduler
    ) =>
        scheduler switch
        {
            ObservableSchedulers.Current => SynchronizationContext.Current,
            ObservableSchedulers.ThreadPool => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(scheduler),
                scheduler,
                "Unknown observable scheduler."
            ),
        };

    private static void Schedule(SynchronizationContext? synchronizationContext, Action action)
    {
        if (synchronizationContext is not null)
        {
            Post(synchronizationContext, action);
            return;
        }

        ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), action);
    }

    private sealed class NotificationDispatcher<T> : IDisposable
    {
        private readonly IObserver<T> _observer;
        private readonly object _gate = new();
        private readonly SerialActionQueue _queue;
        private IDisposable? _subscription;
        private bool _stopped;
        private bool _disposed;

        public NotificationDispatcher(IObserver<T> observer, SynchronizationContext? context)
        {
            _observer = observer;
            _queue = new SerialActionQueue(action => Schedule(context, action));
        }

        public void SetSubscription(IDisposable subscription)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    subscription.Dispose();
                    return;
                }

                _subscription = subscription;
            }
        }

        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_stopped || _disposed)
                {
                    return;
                }
            }

            _queue.Enqueue(() =>
            {
                lock (_gate)
                {
                    if (_disposed)
                        return;
                }
                _observer.OnNext(value);
            });
        }

        public void OnError(Exception error) => ScheduleTerminal(() => _observer.OnError(error));

        public void OnCompleted() => ScheduleTerminal(_observer.OnCompleted);

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
            _queue.Dispose();
        }

        private void ScheduleTerminal(Action terminal)
        {
            lock (_gate)
            {
                if (_stopped || _disposed)
                {
                    return;
                }

                _stopped = true;
            }

            _queue.Enqueue(() =>
            {
                lock (_gate)
                {
                    if (_disposed)
                        return;
                }
                terminal();
                Dispose();
            });
        }
    }

    private sealed class ScheduledSubscription : IDisposable
    {
        private readonly object _gate = new();
        private IDisposable? _subscription;
        private bool _disposed;

        public bool IsDisposed
        {
            get
            {
                lock (_gate)
                {
                    return _disposed;
                }
            }
        }

        public void SetSubscription(IDisposable subscription)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    subscription.Dispose();
                    return;
                }

                _subscription = subscription;
            }
        }

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
        }
    }

    private static void ForwardError<T>(
        IObserver<T> observer,
        IDisposable subscription,
        ref bool stopped,
        Exception error
    )
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        observer.OnError(error);
        subscription.Dispose();
    }

    private static void ForwardCompletion<T>(
        IObserver<T> observer,
        IDisposable subscription,
        ref bool stopped
    )
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        observer.OnCompleted();
        subscription.Dispose();
    }

    private static void ValidateTimeArguments<TSource>(
        IObservable<TSource> source,
        TimeSpan time,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(time, TimeSpan.Zero);
    }

    private static class PropertySetter
    {
        public static Action<TTarget, TValue> Create<TTarget, TValue>(
            Expression<Func<TTarget, TValue>> propertyExpression
        )
        {
            var key = PropertyPathCacheKey<TTarget, TValue>.CreateCacheKey(propertyExpression);
            return PropertySetterCache<TTarget, TValue>.Setters.Get(key);
        }

#pragma warning disable S3398
        private static Action<TTarget, TValue> KeyCompile<TTarget, TValue>(
            PropertyPathCacheKey<TTarget, TValue> key
        )
        {
            var propertyExpression = key.Expression;
            var property = key.FinalProperty;
            var value = Expression.Parameter(typeof(TValue), "value");
            Expression assignedValue = value;
            if (assignedValue.Type != property.PropertyType)
            {
                assignedValue = Expression.Convert(assignedValue, property.PropertyType);
            }

            try
            {
                return Expression
                    .Lambda<Action<TTarget, TValue>>(
                        Expression.Assign(GetPropertyBody(propertyExpression), assignedValue),
                        propertyExpression.Parameters[0],
                        value
                    )
                    .Compile(preferInterpretation: true);
            }
            catch (Exception exception)
                when (exception is ArgumentException or InvalidOperationException)
            {
                throw new ArgumentException(
                    "The key expression must identify an assignable property path.",
                    nameof(key),
                    exception
                );
            }
        }

        private static PropertyInfo GetTargetProperty(LambdaExpression expression)
        {
            var current = GetPropertyBody(expression);
            PropertyInfo? finalProperty = null;

            while (current is MemberExpression member)
            {
                if (member.Member is not PropertyInfo property || property.GetMethod is null)
                {
                    throw new ArgumentException(
                        "The expression must contain readable properties only.",
                        nameof(expression)
                    );
                }

                finalProperty ??= property;
                current = member.Expression!;
            }

            if (current != expression.Parameters[0] || finalProperty is null)
            {
                throw new ArgumentException(
                    "The expression must be a property path rooted at its parameter.",
                    nameof(expression)
                );
            }

            return finalProperty;
        }
#pragma warning restore S3398
        private static Expression GetPropertyBody(LambdaExpression expression) =>
            expression.Body
                is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
            } conversion
                ? conversion.Operand
                : expression.Body;

        private static class PropertySetterCache<TTarget, TValue>
        {
            public static readonly MemoizingLRUCache<
                PropertyPathCacheKey<TTarget, TValue>,
                Action<TTarget, TValue>
            > Setters = new(KeyCompile, 64);
        }

        private readonly struct PropertyPathCacheKey<TTarget, TValue>
            : IEquatable<PropertyPathCacheKey<TTarget, TValue>>
        {
            private readonly int _hashCode;

            private PropertyPathCacheKey(
                Expression<Func<TTarget, TValue>> expression,
                PropertyInfo finalProperty,
                int hashCode
            )
            {
                Expression = expression;
                FinalProperty = finalProperty;
                _hashCode = hashCode;
            }

            public Expression<Func<TTarget, TValue>> Expression { get; }

            public PropertyInfo FinalProperty { get; }

            public static PropertyPathCacheKey<TTarget, TValue> CreateCacheKey(
                Expression<Func<TTarget, TValue>> expression
            )
            {
                var finalProperty = GetTargetProperty(expression);
                if (finalProperty.SetMethod is null)
                {
                    throw new ArgumentException(
                        "The final property in the expression must be writable.",
                        nameof(expression)
                    );
                }

                var hash = new HashCode();
                Expression current = GetPropertyBody(expression);
                while (current is MemberExpression member)
                {
                    hash.Add(member.Member);
                    current = member.Expression!;
                }

                return new PropertyPathCacheKey<TTarget, TValue>(
                    expression,
                    finalProperty,
                    hash.ToHashCode()
                );
            }

            public bool Equals(PropertyPathCacheKey<TTarget, TValue> other)
            {
                var current = GetPropertyBody(Expression);
                var otherCurrent = GetPropertyBody(other.Expression);

                while (
                    current is MemberExpression member
                    && otherCurrent is MemberExpression otherMember
                )
                {
                    if (member.Member != otherMember.Member)
                    {
                        return false;
                    }

                    current = member.Expression!;
                    otherCurrent = otherMember.Expression!;
                }

                return current is ParameterExpression && otherCurrent is ParameterExpression;
            }

            public override bool Equals(object? obj) =>
                obj is PropertyPathCacheKey<TTarget, TValue> other && Equals(other);

            public override int GetHashCode() => _hashCode;
        }
    }
}
