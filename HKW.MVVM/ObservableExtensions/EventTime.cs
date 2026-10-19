namespace HKW.MVVM;

/// <summary>
/// 表示事件通知及其发送方
/// </summary>
/// <typeparam name="TEventArgs">事件参数类型</typeparam>
public sealed class EventPattern<TEventArgs>
{
    /// <summary>
    /// 初始化事件通知
    /// </summary>
    /// <param name="sender">事件发送方</param>
    /// <param name="eventArgs">事件参数</param>
    public EventPattern(object? sender, TEventArgs eventArgs)
    {
        Sender = sender;
        EventArgs = eventArgs;
    }

    /// <summary>
    /// 获取事件发送方
    /// </summary>
    public object? Sender { get; }

    /// <summary>
    /// 获取事件参数
    /// </summary>
    public TEventArgs EventArgs { get; }
}

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将标准 .NET 事件转换为可观察序列
    /// </summary>
    /// <typeparam name="TEventArgs">事件参数类型</typeparam>
    /// <param name="addHandler">添加事件处理程序的操作</param>
    /// <param name="removeHandler">移除事件处理程序的操作</param>
    /// <returns>发布事件发送方和事件参数的可观察序列</returns>
    public static IObservable<EventPattern<TEventArgs>> FromEventPattern<TEventArgs>(
        Action<EventHandler<TEventArgs>> addHandler,
        Action<EventHandler<TEventArgs>> removeHandler
    )
    {
        ArgumentNullException.ThrowIfNull(addHandler);
        ArgumentNullException.ThrowIfNull(removeHandler);

        return Create<EventPattern<TEventArgs>>(observer =>
        {
            var gate = new Lock();
            var disposed = false;
            EventHandler<TEventArgs> handler = (sender, eventArgs) =>
            {
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    observer.OnNext(new EventPattern<TEventArgs>(sender, eventArgs));
                }
            };
            addHandler(handler);
            return new ActionDisposable(() =>
            {
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    removeHandler(handler);
                }
            });
        });
    }

    /// <summary>
    /// 使用显式委托转换器将事件转换为可观察序列
    /// </summary>
    /// <typeparam name="TDelegate">事件委托类型</typeparam>
    /// <typeparam name="TEventArgs">事件参数类型</typeparam>
    /// <param name="conversion">将标准事件处理程序转换为事件委托的函数</param>
    /// <param name="addHandler">添加事件处理程序的操作</param>
    /// <param name="removeHandler">移除事件处理程序的操作</param>
    /// <returns>发布事件发送方和事件参数的可观察序列</returns>
    /// <remarks>显式转换器不依赖运行时反射或动态代码，适用于 AOT 发布。</remarks>
    public static IObservable<EventPattern<TEventArgs>> FromEventPattern<TDelegate, TEventArgs>(
        Func<EventHandler<TEventArgs>, TDelegate> conversion,
        Action<TDelegate> addHandler,
        Action<TDelegate> removeHandler
    )
        where TDelegate : Delegate
    {
        ArgumentNullException.ThrowIfNull(conversion);
        ArgumentNullException.ThrowIfNull(addHandler);
        ArgumentNullException.ThrowIfNull(removeHandler);

        return Create<EventPattern<TEventArgs>>(observer =>
        {
            var gate = new Lock();
            var disposed = false;
            EventHandler<TEventArgs> forwardingHandler = (sender, eventArgs) =>
            {
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    observer.OnNext(new EventPattern<TEventArgs>(sender, eventArgs));
                }
            };
            var handler = conversion(forwardingHandler);
            if (handler is null)
            {
                throw new InvalidOperationException("The event delegate converter returned null.");
            }

            addHandler(handler);
            return new ActionDisposable(() =>
            {
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    removeHandler(handler);
                }
            });
        });
    }

    /// <summary>
    /// 在指定相对时间后发布单个值
    /// </summary>
    /// <param name="dueTime">发布第一个值前的等待时间</param>
    /// <returns>发布值 <c>0</c> 后完成的可观察序列</returns>
    public static IObservable<long> Timer(TimeSpan dueTime) =>
        Timer(dueTime, TimeProvider.System);

    /// <summary>
    /// 使用指定时间提供程序，在指定相对时间后发布单个值
    /// </summary>
    /// <param name="dueTime">发布第一个值前的等待时间</param>
    /// <param name="timeProvider">用于创建计时器的时间提供程序</param>
    /// <returns>发布值 <c>0</c> 后完成的可观察序列</returns>
    public static IObservable<long> Timer(TimeSpan dueTime, TimeProvider timeProvider)
    {
        ValidateDueTime(dueTime, nameof(dueTime));
        ArgumentNullException.ThrowIfNull(timeProvider);
        return CreateTimerObservable(dueTime, System.Threading.Timeout.InfiniteTimeSpan, timeProvider);
    }

    /// <summary>
    /// 在指定绝对时间发布单个值
    /// </summary>
    /// <param name="dueTime">发布值的时间</param>
    /// <returns>发布值 <c>0</c> 后完成的可观察序列</returns>
    public static IObservable<long> Timer(DateTimeOffset dueTime) =>
        Timer(dueTime, TimeProvider.System);

    /// <summary>
    /// 使用指定时间提供程序，在指定绝对时间发布单个值
    /// </summary>
    /// <param name="dueTime">发布值的时间</param>
    /// <param name="timeProvider">用于读取当前时间并创建计时器的时间提供程序</param>
    /// <returns>发布值 <c>0</c> 后完成的可观察序列</returns>
    public static IObservable<long> Timer(DateTimeOffset dueTime, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var delay = dueTime - timeProvider.GetUtcNow();
        return CreateTimerObservable(
            delay > TimeSpan.Zero ? delay : TimeSpan.Zero,
            System.Threading.Timeout.InfiniteTimeSpan,
            timeProvider
        );
    }

    /// <summary>
    /// 在指定绝对时间开始按固定周期发布递增值
    /// </summary>
    /// <param name="dueTime">发布第一个值的时间</param>
    /// <param name="period">后续值之间的时间间隔</param>
    /// <returns>从 <c>0</c> 开始发布递增值的可观察序列</returns>
    public static IObservable<long> Timer(DateTimeOffset dueTime, TimeSpan period) =>
        Timer(dueTime, period, TimeProvider.System);

    /// <summary>
    /// 使用指定时间提供程序，在指定绝对时间开始按固定周期发布递增值
    /// </summary>
    /// <param name="dueTime">发布第一个值的时间</param>
    /// <param name="period">后续值之间的时间间隔</param>
    /// <param name="timeProvider">用于读取当前时间并创建计时器的时间提供程序</param>
    /// <returns>从 <c>0</c> 开始发布递增值的可观察序列</returns>
    public static IObservable<long> Timer(
        DateTimeOffset dueTime,
        TimeSpan period,
        TimeProvider timeProvider
    )
    {
        ValidatePeriod(period, nameof(period));
        ArgumentNullException.ThrowIfNull(timeProvider);
        var delay = dueTime - timeProvider.GetUtcNow();
        return CreateTimerObservable(
            delay > TimeSpan.Zero ? delay : TimeSpan.Zero,
            period,
            timeProvider
        );
    }

    /// <summary>
    /// 在指定相对时间后开始按固定周期发布递增值
    /// </summary>
    /// <param name="dueTime">发布第一个值前的等待时间</param>
    /// <param name="period">后续值之间的时间间隔</param>
    /// <returns>从 <c>0</c> 开始发布递增值的可观察序列</returns>
    public static IObservable<long> Timer(TimeSpan dueTime, TimeSpan period) =>
        Timer(dueTime, period, TimeProvider.System);

    /// <summary>
    /// 使用指定时间提供程序，在指定相对时间后开始按固定周期发布递增值
    /// </summary>
    /// <param name="dueTime">发布第一个值前的等待时间</param>
    /// <param name="period">后续值之间的时间间隔</param>
    /// <param name="timeProvider">用于创建计时器的时间提供程序</param>
    /// <returns>从 <c>0</c> 开始发布递增值的可观察序列</returns>
    public static IObservable<long> Timer(
        TimeSpan dueTime,
        TimeSpan period,
        TimeProvider timeProvider
    )
    {
        ValidateDueTime(dueTime, nameof(dueTime));
        ValidatePeriod(period, nameof(period));
        ArgumentNullException.ThrowIfNull(timeProvider);
        return CreateTimerObservable(dueTime, period, timeProvider);
    }

    /// <summary>
    /// 按固定周期发布递增值
    /// </summary>
    /// <param name="period">值之间的时间间隔</param>
    /// <returns>在每个周期从 <c>0</c> 开始发布递增值的可观察序列</returns>
    public static IObservable<long> Interval(TimeSpan period) =>
        Interval(period, TimeProvider.System);

    /// <summary>
    /// 使用指定时间提供程序按固定周期发布递增值
    /// </summary>
    /// <param name="period">值之间的时间间隔</param>
    /// <param name="timeProvider">用于创建计时器的时间提供程序</param>
    /// <returns>在每个周期从 <c>0</c> 开始发布递增值的可观察序列</returns>
    public static IObservable<long> Interval(TimeSpan period, TimeProvider timeProvider)
    {
        ValidatePeriod(period, nameof(period));
        ArgumentNullException.ThrowIfNull(timeProvider);
        return CreateTimerObservable(period, period, timeProvider);
    }

    private static IObservable<long> CreateTimerObservable(
        TimeSpan dueTime,
        TimeSpan period,
        TimeProvider timeProvider
    ) =>
        Create<long>(observer =>
        {
            var subscription = new TimerObservableSubscription(observer, period);
            subscription.Start(timeProvider, dueTime);
            return subscription;
        });

    private static void ValidateDueTime(TimeSpan dueTime, string parameterName)
    {
        if (dueTime < System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePeriod(TimeSpan period, string parameterName)
    {
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private sealed class TimerObservableSubscription(
        IObserver<long> observer,
        TimeSpan period
    ) : IDisposable
    {
        private readonly Lock _gate = new();
        private ITimer? _timer;
        private long _next;
        private bool _oneShotFired;
        private bool _disposed;

        public void Start(TimeProvider timeProvider, TimeSpan dueTime)
        {
            var timer = timeProvider.CreateTimer(
                static state => ((TimerObservableSubscription)state!).Tick(),
                this,
                dueTime,
                period
            );

            lock (_gate)
            {
                if (_disposed)
                {
                    timer.Dispose();
                }
                else
                {
                    _timer = timer;
                }
            }
        }

        private void Tick()
        {
            lock (_gate)
            {
                if (_disposed || _oneShotFired)
                {
                    return;
                }

                var isOneShot = period == System.Threading.Timeout.InfiniteTimeSpan;
                if (isOneShot)
                {
                    _oneShotFired = true;
                }

                observer.OnNext(_next++);
                if (isOneShot && !_disposed)
                {
                    _disposed = true;
                    observer.OnCompleted();
                    _timer?.Dispose();
                    _timer = null;
                }
            }
        }

        public void Dispose()
        {
            ITimer? timer;
            lock (_gate)
            {
                if (_disposed && _timer is null)
                {
                    return;
                }

                _disposed = true;
                timer = _timer;
                _timer = null;
            }

            timer?.Dispose();
        }
    }
}

