namespace HKW.MVVM;

/// <summary>
/// Common observable operators implemented without taking a dependency on System.Reactive.
/// </summary>
public static class ObservableExtensions
{
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector)
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
                () => ForwardCompletion(observer, subscription, ref stopped));
            return subscription;
        });
    }

    public static IObservable<TSource> Where<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate)
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
                () => ForwardCompletion(observer, subscription, ref stopped));
            return subscription;
        });
    }

    public static IObservable<TSource> DistinctUntilChanged<TSource>(this IObservable<TSource> source) =>
        DistinctUntilChanged(source, EqualityComparer<TSource>.Default);

    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source,
        IEqualityComparer<TSource> comparer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);

        return Create<TSource>(observer =>
        {
            var hasValue = false;
            TSource? lastValue = default;
            return source.Subscribe(
                value =>
                {
                    bool equals;
                    try
                    {
                        equals = hasValue && comparer.Equals(lastValue!, value);
                    }
                    catch (Exception exception)
                    {
                        observer.OnError(exception);
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
                observer.OnError,
                observer.OnCompleted);
        });
    }

    public static IObservable<TSource> StartWith<TSource>(
        this IObservable<TSource> source,
        TSource value)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create<TSource>(observer =>
        {
            observer.OnNext(value);
            return source.Subscribe(observer);
        });
    }

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
                observer.OnCompleted);
        });
    }

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
                () => ForwardCompletion(observer, subscription, ref stopped));
            return subscription;
        });
    }

    public static IObservable<TSource> Do<TSource>(
        this IObservable<TSource> source,
        Action<TSource> onNext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return Create<TSource>(observer => source.Subscribe(
            value =>
            {
                try
                {
                    onNext(value);
                }
                catch (Exception exception)
                {
                    observer.OnError(exception);
                    return;
                }

                observer.OnNext(value);
            },
            observer.OnError,
            observer.OnCompleted));
    }

    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return Create<TSource>(observer => source.Subscribe(
            value => Post(synchronizationContext, () => observer.OnNext(value)),
            error => Post(synchronizationContext, () => observer.OnError(error)),
            () => Post(synchronizationContext, observer.OnCompleted)));
    }

    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime) => Throttle(source, dueTime, TimeProvider.System);

    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);

        return Create<TSource>(observer =>
        {
            var gate = new object();
            ITimer? timer = null;
            var version = 0L;
            var stopped = false;
            var hasValue = false;
            TSource? lastValue = default;

            void Emit(long expectedVersion)
            {
                TSource? value;
                lock (gate)
                {
                    if (stopped || !hasValue || version != expectedVersion)
                    {
                        return;
                    }

                    hasValue = false;
                    value = lastValue;
                }

                observer.OnNext(value!);
            }

            var subscription = source.Subscribe(
                value =>
                {
                    long currentVersion;
                    lock (gate)
                    {
                        if (stopped)
                        {
                            return;
                        }

                        hasValue = true;
                        lastValue = value;
                        currentVersion = ++version;
                        timer?.Dispose();
                        timer = timeProvider.CreateTimer(
                            static state =>
                            {
                                var work = ((Action<long> Emit, long Version))state!;
                                work.Emit(work.Version);
                            },
                            ((Action<long>)Emit, currentVersion),
                            dueTime,
                            Timeout.InfiniteTimeSpan);
                    }
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
                        hasValue = false;
                        timer?.Dispose();
                    }

                    observer.OnError(error);
                },
                () =>
                {
                    TSource? value;
                    bool emit;
                    lock (gate)
                    {
                        if (stopped)
                        {
                            return;
                        }

                        stopped = true;
                        timer?.Dispose();
                        emit = hasValue;
                        value = lastValue;
                        hasValue = false;
                    }

                    if (emit)
                    {
                        observer.OnNext(value!);
                    }

                    observer.OnCompleted();
                });

            return new CompositeDisposableWithAction(subscription, () =>
            {
                lock (gate)
                {
                    stopped = true;
                    timer?.Dispose();
                }
            });
        });
    }

    public static IObservable<TSource> Catch<TSource>(
        this IObservable<TSource> source,
        Func<Exception, IObservable<TSource>> handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);
        return Create<TSource>(observer =>
        {
            var subscriptions = new CompositeDisposable();
            subscriptions.Add(source.Subscribe(
                observer.OnNext,
                error =>
                {
                    IObservable<TSource> replacement;
                    try
                    {
                        replacement = handler(error) ?? throw new InvalidOperationException("The catch handler returned null.");
                    }
                    catch (Exception exception)
                    {
                        observer.OnError(exception);
                        return;
                    }

                    subscriptions.Add(replacement.Subscribe(observer));
                },
                observer.OnCompleted));
            return subscriptions;
        });
    }

    public static IObservable<TSource> Return<TSource>(TSource value) =>
        Create<TSource>(observer =>
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return new ActionDisposable(() => { });
        });

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

    private static void ForwardError<T>(
        IObserver<T> observer,
        IDisposable subscription,
        ref bool stopped,
        Exception error)
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
        ref bool stopped)
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        observer.OnCompleted();
        subscription.Dispose();
    }

    private sealed class CompositeDisposableWithAction(IDisposable subscription, Action dispose) : IDisposable
    {
        private IDisposable? _subscription = subscription;
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
            Interlocked.Exchange(ref _subscription, null)?.Dispose();
        }
    }
}