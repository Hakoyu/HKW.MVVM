using Microsoft.Extensions.Logging;

namespace HKW.MVVM;

/// <summary>
/// 指定可观察操作应在何处调度.
/// </summary>
public enum ObservableSchedulers
{
    /// <summary>
    /// 使用操作符捕获的 <see cref="SynchronizationContext.Current"/>.
    /// </summary>
    Current,

    /// <summary>
    /// 使用 .NET 线程池.
    /// </summary>
    ThreadPool,
}

/// <summary>
/// 不依赖 System.Reactive 实现的常用可观察操作符.
/// </summary>
public static class ObservableExtensions
{
    /// <summary>
    /// 记录可观察序列的每个通知,并原样转发该序列.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要记录日志的可观察序列.</param>
    /// <param name="loggerOwner">其运行时类型用于提供日志记录器类别的对象.</param>
    /// <param name="message">用于在日志条目中标识该序列的标签.</param>
    /// <returns>记录并转发每个源通知的冷可观察序列.</returns>
    /// <remarks>
    /// 值与成功完成以 <see cref="LogLevel.Debug"/> 级别记录.
    /// 错误以<see cref="LogLevel.Error"/> 级别记录并保留原始异常.
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
    /// 使用启用了日志记录器的所有者,以指定级别记录可观察序列的每个通知.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要记录日志的可观察序列.</param>
    /// <param name="loggerOwner">其运行时类型用于提供日志记录器类别的对象.</param>
    /// <param name="logLevel">用于记录每个序列通知的级别.</param>
    /// <param name="message">用于在日志条目中标识该序列的标签.</param>
    /// <returns>记录并转发每个源通知的冷可观察序列.</returns>
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
    /// 使用指定的日志记录器记录可观察序列的每个通知,并原样转发.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要记录日志的可观察序列.</param>
    /// <param name="logger">接收序列通知的日志记录器.</param>
    /// <param name="message">用于在日志条目中标识该序列的标签.</param>
    /// <returns>记录并转发每个源通知的冷可观察序列.</returns>
    /// <remarks>
    /// 值与成功完成以 <see cref="LogLevel.Debug"/> 级别记录.
    /// 错误以<see cref="LogLevel.Error"/> 级别记录并保留原始异常.
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
    /// 以指定级别记录可观察序列的每个通知,并原样转发.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要记录日志的可观察序列.</param>
    /// <param name="logger">接收序列通知的日志记录器.</param>
    /// <param name="logLevel">用于记录每个序列通知的级别.</param>
    /// <param name="message">用于在日志条目中标识该序列的标签.</param>
    /// <returns>记录并转发每个源通知的冷可观察序列.</returns>
    /// <remarks>
    /// 提供的级别用于值,错误和成功完成.错误会保留原始异常.
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

    /// <summary>
    /// 将每个源值投影为新形式.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <typeparam name="TResult">投影后的值类型.</typeparam>
    /// <param name="source">要转换的可观察序列.</param>
    /// <param name="selector">应用于每个值的投影.</param>
    /// <returns>包含投影值的可观察序列.</returns>
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new SelectObservable<TSource, TResult>(source, selector);
    }

    /// <summary>
    /// 使用谓词筛选可观察序列.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要筛选的可观察序列.</param>
    /// <param name="predicate">用于确定是否发出某个值的函数.</param>
    /// <returns>仅包含谓词接受的值的可观察序列.</returns>
    public static IObservable<TSource> Where<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new WhereObservable<TSource>(source, predicate);
    }

    /// <summary>
    /// 使用默认相等比较器抑制连续的重复值.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要比较相邻值的可观察序列.</param>
    /// <returns>不含连续重复值的可观察序列.</returns>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source
    ) => DistinctUntilChanged(source, EqualityComparer<TSource>.Default);

    /// <summary>
    /// 使用指定的相等比较器抑制连续的重复值.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要比较相邻值的可观察序列.</param>
    /// <param name="comparer">用于判定相邻值是否相等的比较器.</param>
    /// <returns>不含连续重复值的可观察序列.</returns>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source,
        IEqualityComparer<TSource> comparer
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);
        return new DistinctUntilChangedObservable<TSource>(source, comparer);
    }

    /// <summary>
    /// 在可观察序列前插入一个值.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要置于其前的可观察序列.</param>
    /// <param name="value">在订阅源之前发出的值.</param>
    /// <returns>以 <paramref name="value"/> 开头的可观察序列.</returns>
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

    /// <summary>
    /// 跳过指定数量的源值,然后发出其余值.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要跳过其值的可观察序列.</param>
    /// <param name="count">要跳过的起始值数量.</param>
    /// <returns>包含跳过前缀之后剩余值的可观察序列.</returns>
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

    /// <summary>
    /// 最多发出指定数量的源值,然后完成.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要从中取值的可观察序列.</param>
    /// <param name="count">要发出的最大数量.</param>
    /// <returns>最多包含 <paramref name="count"/> 个值的可观察序列.</returns>
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

    /// <summary>
    /// 在转发每个值之前对其调用一个操作,并原样转发该值.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要检查的可观察序列.</param>
    /// <param name="onNext">对每个值调用的副作用操作.</param>
    /// <returns>镜像源的可观察序列.</returns>
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

    /// <summary>
    /// 通过同步上下文派发源的值,错误和完成通知.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要派发其通知的可观察序列.</param>
    /// <param name="synchronizationContext">接收通知的同步上下文.</param>
    /// <returns>通知被投递到该上下文的可观察序列.</returns>
    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
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

    /// <summary>
    /// 通过选定的调度器派发源通知.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要派发其通知的可观察序列.</param>
    /// <param name="scheduler">用于观察者通知的调度器.</param>
    /// <returns>通知在选定调度器上进行调度的可观察序列.</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 在创建该操作符时捕获
    /// <see cref="SynchronizationContext.Current"/>. 如果没有可用的上下文,
    /// 则回退到线程池.通知按顺序排队.
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

    /// <summary>
    /// 在同步上下文上调度对源的订阅.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要订阅的可观察序列.</param>
    /// <param name="synchronizationContext">用于订阅操作的同步上下文.</param>
    /// <returns>其源订阅被投递到该上下文的可观察序列.</returns>
    public static IObservable<TSource> SubscribeOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return SubscribeOnCore(source, synchronizationContext);
    }

    /// <summary>
    /// 在选定的调度器上调度对源的订阅.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要订阅的可观察序列.</param>
    /// <param name="scheduler">用于订阅操作的调度器.</param>
    /// <returns>其源订阅被调度的可观察序列.</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 在创建该操作符时捕获
    /// <see cref="SynchronizationContext.Current"/>.如果没有可用的上下文,
    /// 则回退到线程池. 在计划的操作执行前释放,可避免对源进行订阅.
    /// </remarks>
    public static IObservable<TSource> SubscribeOn<TSource>(
        this IObservable<TSource> source,
        ObservableSchedulers scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var synchronizationContext = GetSynchronizationContext(scheduler);
        return SubscribeOnCore(source, synchronizationContext);
    }

    /// <summary>
    /// 仅当源在指定时长内保持静默后,才发出最近的一个值.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要限流的可观察序列.</param>
    /// <param name="dueTime">所需的静默时长.</param>
    /// <returns>使用 <see cref="TimeProvider.System"/> 的限流可观察序列.</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime
    ) => Throttle(source, dueTime, TimeProvider.System);

    /// <summary>
    /// 仅当源在指定时长内保持静默后,才发出最近的一个值,
    /// 并通过选定的调度器派发通知.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要限流的可观察序列.</param>
    /// <param name="dueTime">所需的静默时长.</param>
    /// <param name="scheduler">用于限流通知的调度器.</param>
    /// <returns>通知使用选定调度器的限流可观察序列.</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        ObservableSchedulers scheduler
    ) => Throttle(source, dueTime, TimeProvider.System).ObserveOn(scheduler);

    /// <summary>
    /// 使用提供的时间提供程序,仅当源在指定时长内保持静默后,才发出最近的一个值.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要限流的可观察序列.</param>
    /// <param name="dueTime">所需的静默时长.</param>
    /// <param name="timeProvider">用于创建计时器的时间提供程序.</param>
    /// <returns>限流的可观察序列.</returns>
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

    /// <summary>
    /// 当源以错误终止时,继续使用替换的可观察序列.
    /// </summary>
    /// <typeparam name="TSource">源值类型.</typeparam>
    /// <param name="source">要监视错误的可观察序列.</param>
    /// <param name="handler">将源错误映射为替换序列的函数.</param>
    /// <returns>镜像源,或在发生错误后镜像其替换序列的可观察序列.</returns>
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

    /// <summary>
    /// 创建一个发出单个值后即完成的可观察序列.
    /// </summary>
    /// <typeparam name="TSource">发出的值类型.</typeparam>
    /// <param name="value">要发出的单个值.</param>
    /// <returns>仅包含一个值的可观察序列.</returns>
    public static IObservable<TSource> Return<TSource>(TSource value) =>
        Create<TSource>(observer =>
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return new ActionDisposable(() => { });
        });

    /// <summary>
    /// 创建一个不发出任何值即完成的可观察序列.
    /// </summary>
    /// <typeparam name="TSource">序列值类型.</typeparam>
    /// <returns>立即完成的空可观察序列.</returns>
    public static IObservable<TSource> Empty<TSource>() =>
        Create<TSource>(observer =>
        {
            observer.OnCompleted();
            return new ActionDisposable(() => { });
        });

    private static IObservable<T> Create<T>(Func<IObserver<T>, IDisposable> subscribe) =>
        new AnonymousObservable<T>(subscribe);

    private static IObservable<TSource> SubscribeOnCore<TSource>(
        IObservable<TSource> source,
        SynchronizationContext? synchronizationContext
    ) =>
        Create<TSource>(observer =>
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
        });

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
}
