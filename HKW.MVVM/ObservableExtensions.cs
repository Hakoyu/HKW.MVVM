using Microsoft.Extensions.Logging;

namespace HKW.MVVM;

/// <summary>Specifies where an observable operation should be scheduled.</summary>
public enum ObservableSchedulers
{
    /// <summary>Uses the <see cref="SynchronizationContext.Current"/> captured by the operator.</summary>
    Current,

    /// <summary>Uses the .NET thread pool.</summary>
    ThreadPool,
}

/// <summary>
/// Common observable operators implemented without taking a dependency on System.Reactive.
/// </summary>
public static class ObservableExtensions
{
    /// <summary>
    /// Logs each notification from an observable sequence and forwards the sequence unchanged.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to log.</param>
    /// <param name="loggerOwner">The object whose runtime type supplies the logger category.</param>
    /// <param name="message">A label used to identify this sequence in log entries.</param>
    /// <returns>A cold observable sequence that logs and forwards every source notification.</returns>
    /// <remarks>
    /// Values and successful completion are logged at <see cref="LogLevel.Debug"/>; errors are logged at
    /// <see cref="LogLevel.Error"/> and retain the original exception. Logging begins only after subscription.
    /// <b>REFLECTION: NO.</b> The logger is obtained through <see cref="LoggerMixins.Log(IEnableLogger)"/>
    /// and notifications are forwarded directly.
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        IEnableLogger loggerOwner,
        string? message = null
    )
    {
        ArgumentNullException.ThrowIfNull(loggerOwner);
        return Log(source, loggerOwner.Log(), message);
    }

    /// <summary>
    /// Logs each notification from an observable sequence at the specified level using a logger-enabled owner.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to log.</param>
    /// <param name="loggerOwner">The object whose runtime type supplies the logger category.</param>
    /// <param name="logLevel">The level used to log every sequence notification.</param>
    /// <param name="message">A label used to identify this sequence in log entries.</param>
    /// <returns>A cold observable sequence that logs and forwards every source notification.</returns>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        IEnableLogger loggerOwner,
        LogLevel logLevel,
        string? message = null
    )
    {
        ArgumentNullException.ThrowIfNull(loggerOwner);
        return Log(source, loggerOwner.Log(), logLevel, message);
    }

    /// <summary>
    /// Logs each notification from an observable sequence with a specified logger and forwards it unchanged.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to log.</param>
    /// <param name="logger">The logger that receives sequence notifications.</param>
    /// <param name="message">A label used to identify this sequence in log entries.</param>
    /// <returns>A cold observable sequence that logs and forwards every source notification.</returns>
    /// <remarks>
    /// Values and successful completion are logged at <see cref="LogLevel.Debug"/>; errors are logged at
    /// <see cref="LogLevel.Error"/> and retain the original exception. Logging begins only after subscription.
    /// <b>REFLECTION: NO.</b> Logging and observer notification methods are invoked directly.
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        ILogger logger,
        string? message = null
    )
    {
        return Log(source, logger, LogLevel.Debug, LogLevel.Error, message);
    }

    /// <summary>
    /// Logs each notification from an observable sequence at the specified level and forwards it unchanged.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to log.</param>
    /// <param name="logger">The logger that receives sequence notifications.</param>
    /// <param name="logLevel">The level used to log every sequence notification.</param>
    /// <param name="message">A label used to identify this sequence in log entries.</param>
    /// <returns>A cold observable sequence that logs and forwards every source notification.</returns>
    /// <remarks>
    /// The supplied level is used for values, errors, and successful completion. Errors retain the original
    /// exception. Logging begins only after subscription.
    /// <b>REFLECTION: NO.</b> Logging and observer notification methods are invoked directly.
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        ILogger logger,
        LogLevel logLevel,
        string? message = null
    )
    {
        return Log(source, logger, logLevel, logLevel, message);
    }

    private static IObservable<TSource> Log<TSource>(
        IObservable<TSource> source,
        ILogger logger,
        LogLevel notificationLogLevel,
        LogLevel errorLogLevel,
        string? message
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(logger);
        var label = string.IsNullOrWhiteSpace(message) ? "Observable" : message;

        return Create<TSource>(observer =>
            source.Subscribe(
                value =>
                {
                    logger.Log(notificationLogLevel, "{Observable}: OnNext({Value})", label, value);
                    observer.OnNext(value);
                },
                error =>
                {
                    logger.Log(
                        errorLogLevel,
                        error,
                        "{Observable}: OnError({ErrorMessage})",
                        label,
                        error.Message
                    );
                    observer.OnError(error);
                },
                () =>
                {
                    logger.Log(notificationLogLevel, "{Observable}: OnCompleted()", label);
                    observer.OnCompleted();
                }
            )
        );
    }

    /// <summary>Projects each source value into a new form.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TResult">The projected value type.</typeparam>
    /// <param name="source">The observable sequence to transform.</param>
    /// <param name="selector">The projection applied to each value.</param>
    /// <returns>An observable sequence containing projected values.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Values are transformed by invoking the supplied delegate directly.</remarks>
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        return Create<TResult>(observer =>
        {
            var stopped = false;
            var subscription = new SingleAssignmentDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (stopped)
                    {
                        return;
                    }

                    TResult result;
                    try
                    {
                        result = selector(value);
                    }
                    catch (Exception exception)
                    {
                        stopped = true;
                        observer.OnError(exception);
                        subscription.Dispose();
                        return;
                    }

                    observer.OnNext(result);
                },
                error => ForwardError(observer, subscription, ref stopped, error),
                () => ForwardCompletion(observer, subscription, ref stopped)
            );
            return subscription;
        });
    }

    /// <summary>Filters an observable sequence using a predicate.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to filter.</param>
    /// <param name="predicate">A function that determines whether a value is emitted.</param>
    /// <returns>An observable sequence containing only values accepted by the predicate.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The predicate is invoked directly for each value.</remarks>
    public static IObservable<TSource> Where<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return Create<TSource>(observer =>
        {
            var stopped = false;
            var subscription = new SingleAssignmentDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (stopped)
                    {
                        return;
                    }

                    bool shouldEmit;
                    try
                    {
                        shouldEmit = predicate(value);
                    }
                    catch (Exception exception)
                    {
                        stopped = true;
                        observer.OnError(exception);
                        subscription.Dispose();
                        return;
                    }

                    if (shouldEmit)
                    {
                        observer.OnNext(value);
                    }
                },
                error => ForwardError(observer, subscription, ref stopped, error),
                () => ForwardCompletion(observer, subscription, ref stopped)
            );
            return subscription;
        });
    }

    /// <summary>Suppresses consecutive duplicate values using the default equality comparer.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence whose consecutive values are compared.</param>
    /// <returns>An observable sequence without consecutive duplicates.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Equality is evaluated by <see cref="EqualityComparer{T}.Default"/>.</remarks>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source
    ) => DistinctUntilChanged(source, EqualityComparer<TSource>.Default);

    /// <summary>Suppresses consecutive duplicate values using a specified equality comparer.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence whose consecutive values are compared.</param>
    /// <param name="comparer">The comparer used to determine whether adjacent values are equal.</param>
    /// <returns>An observable sequence without consecutive duplicates.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Equality is evaluated by calling the supplied comparer directly.</remarks>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source,
        IEqualityComparer<TSource> comparer
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);

        return Create<TSource>(observer =>
        {
            var hasValue = false;
            TSource? lastValue = default;
            var stopped = false;
            var subscription = new SingleAssignmentDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (stopped)
                    {
                        return;
                    }

                    bool equals;
                    try
                    {
                        equals = hasValue && comparer.Equals(lastValue!, value);
                    }
                    catch (Exception exception)
                    {
                        stopped = true;
                        observer.OnError(exception);
                        subscription.Dispose();
                        return;
                    }

                    if (equals)
                    {
                        return;
                    }

                    hasValue = true;
                    lastValue = value;
                    observer.OnNext(value);
                },
                error => ForwardError(observer, subscription, ref stopped, error),
                () => ForwardCompletion(observer, subscription, ref stopped)
            );
            return subscription;
        });
    }

    /// <summary>Prepends one value to an observable sequence.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to precede.</param>
    /// <param name="value">The value emitted before subscribing to the source.</param>
    /// <returns>An observable sequence beginning with <paramref name="value"/>.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The initial value is sent directly to the observer.</remarks>
    public static IObservable<TSource> StartWith<TSource>(
        this IObservable<TSource> source,
        TSource value
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create<TSource>(observer =>
        {
            observer.OnNext(value);
            return source.Subscribe(observer);
        });
    }

    /// <summary>Skips a specified number of source values and then emits the remainder.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to skip values from.</param>
    /// <param name="count">The number of initial values to skip.</param>
    /// <returns>An observable sequence containing values after the skipped prefix.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Values are counted directly.</remarks>
    public static IObservable<TSource> Skip<TSource>(this IObservable<TSource> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return Create<TSource>(observer =>
        {
            var remaining = count;
            return source.Subscribe(
                value =>
                {
                    if (remaining > 0)
                    {
                        remaining--;
                    }
                    else
                    {
                        observer.OnNext(value);
                    }
                },
                observer.OnError,
                observer.OnCompleted
            );
        });
    }

    /// <summary>Emits at most a specified number of source values and then completes.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to take values from.</param>
    /// <param name="count">The maximum number of values to emit.</param>
    /// <returns>An observable sequence containing no more than <paramref name="count"/> values.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Values are counted directly and the upstream subscription is disposed when the limit is reached.</remarks>
    public static IObservable<TSource> Take<TSource>(this IObservable<TSource> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return Create<TSource>(observer =>
            {
                observer.OnCompleted();
                return new ActionDisposable(() => { });
            });
        }

        return Create<TSource>(observer =>
        {
            var remaining = count;
            var stopped = false;
            var subscription = new SingleAssignmentDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (stopped)
                    {
                        return;
                    }

                    observer.OnNext(value);
                    remaining--;
                    if (remaining == 0)
                    {
                        stopped = true;
                        observer.OnCompleted();
                        subscription.Dispose();
                    }
                },
                error => ForwardError(observer, subscription, ref stopped, error),
                () => ForwardCompletion(observer, subscription, ref stopped)
            );
            return subscription;
        });
    }

    /// <summary>Invokes an action for each value before forwarding that value unchanged.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to inspect.</param>
    /// <param name="onNext">The side-effect action invoked for each value.</param>
    /// <returns>An observable sequence that mirrors the source.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The side-effect delegate is invoked directly.</remarks>
    public static IObservable<TSource> Do<TSource>(
        this IObservable<TSource> source,
        Action<TSource> onNext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return Create<TSource>(observer =>
        {
            var stopped = false;
            var subscription = new SingleAssignmentDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (stopped)
                    {
                        return;
                    }

                    try
                    {
                        onNext(value);
                    }
                    catch (Exception exception)
                    {
                        ForwardError(observer, subscription, ref stopped, exception);
                        return;
                    }

                    observer.OnNext(value);
                },
                error => ForwardError(observer, subscription, ref stopped, error),
                () => ForwardCompletion(observer, subscription, ref stopped)
            );
            return subscription;
        });
    }

    /// <summary>Dispatches source values, errors, and completion through a synchronization context.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence whose notifications are dispatched.</param>
    /// <param name="synchronizationContext">The synchronization context that receives notifications.</param>
    /// <returns>An observable sequence whose notifications are posted to the context.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Notifications are scheduled with <see cref="SynchronizationContext.Post"/>.</remarks>
    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return Create<TSource>(observer =>
            source.Subscribe(
                value => Post(synchronizationContext, () => observer.OnNext(value)),
                error => Post(synchronizationContext, () => observer.OnError(error)),
                () => Post(synchronizationContext, observer.OnCompleted)
            )
        );
    }

    /// <summary>Dispatches source notifications through the selected scheduler.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence whose notifications are dispatched.</param>
    /// <param name="scheduler">The scheduler used for observer notifications.</param>
    /// <returns>An observable sequence whose notifications are scheduled on the selected scheduler.</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> captures
    /// <see cref="SynchronizationContext.Current"/> when this operator is created. If no context is available,
    /// it falls back to the thread pool. Notifications are queued in order.
    /// </remarks>
    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        ObservableSchedulers scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var synchronizationContext = GetSynchronizationContext(scheduler);

        return Create<TSource>(observer =>
        {
            var dispatcher = new NotificationDispatcher<TSource>(observer, synchronizationContext);
            var subscription = source.Subscribe(
                dispatcher.OnNext,
                dispatcher.OnError,
                dispatcher.OnCompleted
            );
            dispatcher.SetSubscription(subscription);
            return dispatcher;
        });
    }

    /// <summary>Schedules subscription to the source on the selected scheduler.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to subscribe to.</param>
    /// <param name="scheduler">The scheduler used for the subscription action.</param>
    /// <returns>An observable sequence whose source subscription is scheduled.</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> captures
    /// <see cref="SynchronizationContext.Current"/> when this operator is created. If no context is available,
    /// it falls back to the thread pool. Disposing before the scheduled action runs prevents the source from being
    /// subscribed.
    /// </remarks>
    public static IObservable<TSource> SubscribeOn<TSource>(
        this IObservable<TSource> source,
        ObservableSchedulers scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var synchronizationContext = GetSynchronizationContext(scheduler);

        return Create<TSource>(observer =>
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

                    var subscription = source.Subscribe(observer);
                    scheduledSubscription.SetSubscription(subscription);
                }
            );
            return scheduledSubscription;
        });
    }

    /// <summary>
    /// Emits only the most recent value after the source remains quiet for the specified duration.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to throttle.</param>
    /// <param name="dueTime">The required quiet period.</param>
    /// <returns>A throttled observable sequence using <see cref="TimeProvider.System"/>.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Delayed emissions use <see cref="TimeProvider"/> timers.</remarks>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime
    ) => Throttle(source, dueTime, TimeProvider.System);

    /// <summary>
    /// Emits only the most recent value after the source remains quiet for the specified duration,
    /// and dispatches notifications through the selected scheduler.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to throttle.</param>
    /// <param name="dueTime">The required quiet period.</param>
    /// <param name="scheduler">The scheduler used for throttled notifications.</param>
    /// <returns>A throttled observable sequence whose notifications use the selected scheduler.</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        ObservableSchedulers scheduler
    ) => Throttle(source, dueTime, TimeProvider.System).ObserveOn(scheduler);

    /// <summary>
    /// Emits only the most recent value after the source remains quiet for the specified duration,
    /// using a supplied time provider.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to throttle.</param>
    /// <param name="dueTime">The required quiet period.</param>
    /// <param name="timeProvider">The provider used to create timers.</param>
    /// <returns>A throttled observable sequence.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Delayed emissions use <see cref="TimeProvider.CreateTimer"/> directly.</remarks>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);

        return Create<TSource>(observer => new ThrottleSubscription<TSource>(
            source,
            observer,
            dueTime,
            timeProvider
        ));
    }

    /// <summary>Continues with a replacement observable when the source terminates with an error.</summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <param name="source">The observable sequence to monitor for errors.</param>
    /// <param name="handler">A function that maps the source error to a replacement sequence.</param>
    /// <returns>An observable sequence that mirrors the source or its replacement after an error.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The error handler is invoked directly.</remarks>
    public static IObservable<TSource> Catch<TSource>(
        this IObservable<TSource> source,
        Func<Exception, IObservable<TSource>> handler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);
        return Create<TSource>(observer =>
        {
            var subscriptions = new MultipleDisposable();
            subscriptions.Add(
                source.Subscribe(
                    observer.OnNext,
                    error =>
                    {
                        IObservable<TSource> replacement;
                        try
                        {
                            replacement =
                                handler(error)
                                ?? throw new InvalidOperationException(
                                    "The catch handler returned null."
                                );
                        }
                        catch (Exception exception)
                        {
                            observer.OnError(exception);
                            return;
                        }

                        subscriptions.Add(replacement.Subscribe(observer));
                    },
                    observer.OnCompleted
                )
            );
            return subscriptions;
        });
    }

    /// <summary>Creates an observable sequence that emits one value and then completes.</summary>
    /// <typeparam name="TSource">The emitted value type.</typeparam>
    /// <param name="value">The single value to emit.</param>
    /// <returns>An observable sequence containing exactly one value.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The value and completion notification are sent directly.</remarks>
    public static IObservable<TSource> Return<TSource>(TSource value) =>
        Create<TSource>(observer =>
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return new ActionDisposable(() => { });
        });

    /// <summary>Creates an observable sequence that completes without emitting any values.</summary>
    /// <typeparam name="TSource">The sequence value type.</typeparam>
    /// <returns>An empty, immediately completing observable sequence.</returns>
    /// <remarks><b>REFLECTION: NO.</b> Completion is sent directly to the observer.</remarks>
    public static IObservable<TSource> Empty<TSource>() =>
        Create<TSource>(observer =>
        {
            observer.OnCompleted();
            return new ActionDisposable(() => { });
        });

    private static IObservable<T> Create<T>(Func<IObserver<T>, IDisposable> subscribe) =>
        new AnonymousObservable<T>(subscribe);

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

    private sealed class NotificationDispatcher<T>(
        IObserver<T> observer,
        SynchronizationContext? context
    ) : IDisposable
    {
        private readonly object _gate = new();
        private IDisposable? _subscription;
        private bool _stopped;
        private bool _disposed;

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

            Schedule(
                context,
                () =>
                {
                    lock (_gate)
                    {
                        if (_disposed)
                        {
                            return;
                        }
                    }

                    observer.OnNext(value);
                }
            );
        }

        public void OnError(Exception error) => ScheduleTerminal(() => observer.OnError(error));

        public void OnCompleted() => ScheduleTerminal(observer.OnCompleted);

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

            Schedule(
                context,
                () =>
                {
                    lock (_gate)
                    {
                        if (_disposed)
                        {
                            return;
                        }
                    }

                    terminal();
                    Dispose();
                }
            );
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
}
