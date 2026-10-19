using Microsoft.Extensions.Logging;

namespace HKW.MVVM;

/// <summary>
/// 调度可观察序列的工作项.
/// </summary>
public interface IObservableScheduler
{
    /// <summary>
    /// 调度要执行的操作.
    /// </summary>
    /// <param name="action">要调度的操作.</param>
    /// <returns>可用于在操作开始前取消操作的资源.</returns>
    IDisposable Schedule(Action action);
}

/// <summary>
/// 提供内置的可观察序列调度器以及同步上下文调度器工厂.
/// </summary>
public static class ObservableSchedulers
{
    /// <summary>
    /// 获取在调用线程上立即执行操作的调度器.
    /// </summary>
    public static IObservableScheduler Immediate { get; } = new ImmediateScheduler();

    /// <summary>
    /// 获取在当前线程上按先进先出顺序执行操作的调度器.
    /// 嵌套调度使用蹦床队列,不会递归执行.
    /// </summary>
    public static IObservableScheduler CurrentThread { get; } = new CurrentThreadScheduler();

    /// <summary>
    /// 获取在线程池上执行操作的调度器.
    /// </summary>
    public static IObservableScheduler ThreadPool { get; } = new ThreadPoolScheduler();

    /// <summary>
    /// 创建将操作投递到指定同步上下文的调度器.
    /// </summary>
    /// <param name="synchronizationContext">接收操作的同步上下文.</param>
    /// <returns>同步上下文调度器.</returns>
    public static IObservableScheduler FromSynchronizationContext(
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return new SynchronizationContextScheduler(synchronizationContext);
    }

    private sealed class ImmediateScheduler : IObservableScheduler
    {
        public IDisposable Schedule(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
            return new ActionDisposable(static () => { });
        }
    }

    private sealed class CurrentThreadScheduler : IObservableScheduler
    {
        [ThreadStatic]
        private static Queue<ScheduledAction>? _queue;

        public IDisposable Schedule(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var scheduledAction = new ScheduledAction(action);
            if (_queue is not null)
            {
                _queue.Enqueue(scheduledAction);
                return scheduledAction;
            }

            var queue = new Queue<ScheduledAction>();
            _queue = queue;
            queue.Enqueue(scheduledAction);
            try
            {
                while (queue.TryDequeue(out var queuedAction))
                {
                    queuedAction.Invoke();
                }
            }
            finally
            {
                queue.Clear();
                _queue = null;
            }

            return scheduledAction;
        }
    }

    private sealed class ThreadPoolScheduler : IObservableScheduler
    {
        public IDisposable Schedule(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var scheduledAction = new ScheduledAction(action);
            System.Threading.ThreadPool.QueueUserWorkItem(
                static state => state.Invoke(),
                scheduledAction,
                preferLocal: false
            );
            return scheduledAction;
        }
    }

    private sealed class SynchronizationContextScheduler(SynchronizationContext context)
        : IObservableScheduler
    {
        public IDisposable Schedule(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var scheduledAction = new ScheduledAction(action);
            context.Post(static state => ((ScheduledAction)state!).Invoke(), scheduledAction);
            return scheduledAction;
        }
    }

    private sealed class ScheduledAction(Action action) : IDisposable
    {
        private Action? _action = action;

        public void Invoke() => Interlocked.Exchange(ref _action, null)?.Invoke();

        public void Dispose() => Interlocked.Exchange(ref _action, null);
    }
}

internal interface IObservableWithLogger
{
    ILogger GetLogger();
}

internal sealed class ObservableWithLogger<T> : IObservable<T>, IObservableWithLogger
{
    private readonly IObservable<T> _source;
    private readonly Lazy<ILogger> _logger;

    public ObservableWithLogger(IObservable<T> source, Func<ILogger> loggerFactory)
    {
        _source = source;
        _logger = new Lazy<ILogger>(loggerFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ILogger GetLogger() => _logger.Value;

    public IDisposable Subscribe(IObserver<T> observer) => _source.Subscribe(observer);
}
