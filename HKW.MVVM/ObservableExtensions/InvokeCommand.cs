using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace HKW.MVVM;

/// <summary>
/// 表示一个活动的命令桥接, 并公开桥接终止异常
/// </summary>
public sealed class CommandInvocationResult : IDisposable
{
    private readonly CommandExceptionStream _exceptions = new();
    private Action? _dispose;
    private int _stopped;

    internal CommandInvocationResult() { }

    /// <summary>
    /// 获取源、命令路径或命令回调产生的终止异常流
    /// </summary>
    public IObservable<Exception> ThrownExceptions => _exceptions;

    internal bool IsStopped => Volatile.Read(ref _stopped) != 0;

    internal void Attach(Action dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);
        if (Interlocked.CompareExchange(ref _dispose, dispose, null) is not null)
        {
            throw new InvalidOperationException("The command bridge is already attached.");
        }

        if (IsStopped)
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    internal void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _dispose, null)?.Invoke();
        _exceptions.Publish(error);
        _exceptions.Dispose();
    }

    /// <summary>停止源订阅和待处理工作, 并完成 <see cref="ThrownExceptions"/></summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _dispose, null)?.Invoke();
        _exceptions.Dispose();
    }

    private sealed class CommandExceptionStream : IObservable<Exception>, IDisposable
    {
        private readonly
#if NET10_0_OR_GREATER
        Lock
#else
        object
#endif
        _gate = new();
        private List<IObserver<Exception>>? _observers = [];
        private Exception? _terminalException;

        public IDisposable Subscribe(IObserver<Exception> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            Exception? terminalException;
            lock (_gate)
            {
                if (_observers is not null)
                {
                    _observers.Add(observer);
                    return new ActionDisposable(() => Unsubscribe(observer));
                }

                terminalException = _terminalException;
            }

            if (terminalException is not null)
            {
                observer.OnNext(terminalException);
            }
            observer.OnCompleted();
            return new ActionDisposable(static () => { });
        }

        public void Publish(Exception error)
        {
            IObserver<Exception>[] observers;
            lock (_gate)
            {
                if (_observers is null || _terminalException is not null)
                {
                    return;
                }

                _terminalException = error;
                observers = _observers.ToArray();
            }

            foreach (var observer in observers)
            {
                try
                {
                    observer.OnNext(error);
                }
                catch
                {
                    // Observer failures must not escape the source notification thread.
                }
            }
        }

        public void Dispose()
        {
            IObserver<Exception>[] observers;
            lock (_gate)
            {
                observers = _observers?.ToArray() ?? [];
                _observers = null;
            }

            foreach (var observer in observers)
            {
                try
                {
                    observer.OnCompleted();
                }
                catch
                {
                    // Observer failures must not escape bridge shutdown.
                }
            }
        }

        private void Unsubscribe(IObserver<Exception> observer)
        {
            lock (_gate)
            {
                _observers?.Remove(observer);
            }
        }
    }
}

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将可观察序列产生的每个值传递给命令
    /// </summary>
    /// <remarks>桥接错误由返回结果的 <see cref="CommandInvocationResult.ThrownExceptions"/> 发布</remarks>
    public static CommandInvocationResult InvokeCommand<TSource>(
        this IObservable<TSource> source,
        ICommand command
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(command);
        if (command is IAsyncRelayCommand)
        {
            throw new ArgumentException(
                "Async commands require an InvokeCommand overload with an explicit AsyncConcurrencyMode.",
                nameof(command)
            );
        }
        return StartSynchronousBridge(source, ObservableExtensions.Return<ICommand?>(command));
    }

    /// <summary>
    /// 将每个源值以强类型参数传递给 Toolkit 命令
    /// </summary>
    public static CommandInvocationResult InvokeCommand<TSource>(
        this IObservable<TSource> source,
        IRelayCommand<TSource> command
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        return InvokeCommand(source, (ICommand)command);
    }

    /// <summary>
    /// 将可观察序列产生的每个值传递给目标对象当前的命令属性
    /// </summary>
    public static CommandInvocationResult InvokeCommand<TSource, TTarget>(
        this IObservable<TSource> source,
        TTarget target,
        Expression<Func<TTarget, ICommand?>> commandProperty
    )
        where TTarget : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(commandProperty);
        return StartSynchronousBridge(source, target.WhenAnyValue(commandProperty));
    }

    /// <summary>
    /// 使用指定重叠策略异步调用 Toolkit 命令
    /// </summary>
    /// <param name="source">提供命令参数的可观察序列</param>
    /// <param name="command">要调用的异步 Toolkit 命令</param>
    /// <param name="mode">必需的异步重叠策略</param>
    public static CommandInvocationResult InvokeCommand<TSource>(
        this IObservable<TSource> source,
        IAsyncRelayCommand<TSource> command,
        AsyncConcurrencyMode mode
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(command);
        if (Enum.IsDefined(mode) is false)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var result = new CommandInvocationResult();
        var bridge = new AsyncCommandInvocationSubscription<TSource>(result, command, mode);
        result.Attach(bridge.Dispose);
        bridge.Start(source);
        return result;
    }

    /// <summary>
    /// 观察命令对指定参数的可执行状态, 并立即发布当前值
    /// </summary>
    public static IObservable<bool> ObserveCanExecute<T>(
        this IRelayCommand<T> command,
        T? parameter = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        return Create<bool>(observer =>
        {
            EventHandler? handler = null;
            handler = (_, _) =>
            {
                try
                {
                    observer.OnNext(command.CanExecute(parameter));
                }
                catch (Exception error)
                {
                    command.CanExecuteChanged -= handler;
                    observer.OnError(error);
                }
            };
            command.CanExecuteChanged += handler;
            handler(command, EventArgs.Empty);
            return new ActionDisposable(() => command.CanExecuteChanged -= handler);
        });
    }

    /// <summary>
    /// 观察异步命令的运行状态, 并立即发布当前值
    /// </summary>
    public static IObservable<bool> ObserveIsRunning(this IAsyncRelayCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Create<bool>(observer =>
        {
            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (
                    string.IsNullOrEmpty(args.PropertyName)
                    || args.PropertyName == nameof(command.IsRunning)
                )
                {
                    observer.OnNext(command.IsRunning);
                }
            };
            command.PropertyChanged += handler;
            observer.OnNext(command.IsRunning);
            return new ActionDisposable(() => command.PropertyChanged -= handler);
        });
    }

    private static CommandInvocationResult StartSynchronousBridge<TSource>(
        IObservable<TSource> source,
        IObservable<ICommand?> commands
    )
    {
        var result = new CommandInvocationResult();
        var bridge = new CommandInvocationSubscription<TSource>(result);
        result.Attach(bridge.Dispose);
        bridge.Start(source, commands);
        return result;
    }

    private sealed class CommandInvocationSubscription<TSource>(CommandInvocationResult result)
        : IDisposable
    {
        private readonly
#if NET10_0_OR_GREATER
        Lock
#else
        object
#endif
        _gate = new();
        private readonly SingleAssignmentDisposable _commandSubscription = new();
        private readonly SingleAssignmentDisposable _sourceSubscription = new();
        private ICommand? _command;
        private bool _sourceCompleted;
        private bool _disposed;

        public void Start(IObservable<TSource> source, IObservable<ICommand?> commands)
        {
            try
            {
                _commandSubscription.Disposable = commands.Subscribe(
                    SetCommand,
                    result.Fail,
                    static () => { }
                );
                if (result.IsStopped is false)
                {
                    _sourceSubscription.Disposable = source.Subscribe(
                        Invoke,
                        result.Fail,
                        DisposeSource
                    );
                }
            }
            catch (Exception error)
            {
                result.Fail(error);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _command = null;
            }
            _sourceSubscription.Dispose();
            _commandSubscription.Dispose();
        }

        private void DisposeSource()
        {
            lock (_gate)
            {
                _sourceCompleted = true;
            }
            _sourceSubscription.Dispose();
        }

        private void SetCommand(ICommand? command)
        {
            lock (_gate)
            {
                if (_disposed is false)
                {
                    _command = command;
                }
            }
        }

        private void Invoke(TSource value)
        {
            ICommand? command;
            lock (_gate)
            {
                command = _disposed || _sourceCompleted ? null : _command;
            }
            if (command is null)
            {
                return;
            }
            if (command is IAsyncRelayCommand)
            {
                result.Fail(
                    new InvalidOperationException(
                        "Async commands require an InvokeCommand overload with an explicit AsyncConcurrencyMode."
                    )
                );
                return;
            }

            try
            {
                if (command.CanExecute(value))
                {
                    command.Execute(value);
                }
            }
            catch (Exception error)
            {
                result.Fail(error);
            }
        }
    }

    private sealed class AsyncCommandInvocationSubscription<T>(
        CommandInvocationResult result,
        IAsyncRelayCommand<T> command,
        AsyncConcurrencyMode mode
    ) : IDisposable
    {
        private readonly
#if NET10_0_OR_GREATER
        Lock
#else
        object
#endif
        _gate = new();
        private readonly Queue<T?> _queue = [];
        private readonly HashSet<Task> _expectedCancellations = [];
        private readonly SingleAssignmentDisposable _sourceSubscription = new();
        private readonly HashSet<Task> _runningTasks = [];
        private T? _latestPending;
        private bool _hasLatestPending;
        private bool _starting;
        private bool _sourceCompleted;
        private bool _disposed;

        public void Start(IObservable<T> source)
        {
            try
            {
                _sourceSubscription.Disposable = source.Subscribe(
                    Receive,
                    result.Fail,
                    DisposeSource
                );
            }
            catch (Exception error)
            {
                result.Fail(error);
            }
        }

        public void Dispose()
        {
            var cancel = false;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _queue.Clear();
                _latestPending = default;
                _hasLatestPending = false;
                foreach (var task in _runningTasks)
                {
                    _expectedCancellations.Add(task);
                }
                cancel = mode != AsyncConcurrencyMode.Concurrent && command.CanBeCanceled;
            }
            _sourceSubscription.Dispose();
            if (cancel)
            {
                command.Cancel();
            }
        }

        private void DisposeSource()
        {
            lock (_gate)
            {
                _sourceCompleted = true;
            }
            _sourceSubscription.Dispose();
        }

        private void Receive(T value)
        {
            var start = false;
            var cancelPrevious = false;
            lock (_gate)
            {
                if (_disposed || _sourceCompleted)
                {
                    return;
                }

                switch (mode)
                {
                    case AsyncConcurrencyMode.Sequential when _starting || _runningTasks.Count > 0:
                        _queue.Enqueue(value);
                        return;
                    case AsyncConcurrencyMode.Drop when _starting || _runningTasks.Count > 0:
                        return;
                    case AsyncConcurrencyMode.Latest when _starting || _runningTasks.Count > 0:
                        _latestPending = value;
                        _hasLatestPending = true;
                        cancelPrevious = command.CanBeCanceled;
                        if (cancelPrevious)
                        {
                            foreach (var task in _runningTasks)
                            {
                                _expectedCancellations.Add(task);
                            }
                        }
                        start = false;
                        break;
                    default:
                        _starting = true;
                        start = true;
                        break;
                }
            }

            if (cancelPrevious)
            {
                command.Cancel();
            }
            if (start)
            {
                StartExecution(value);
            }
        }

        private void StartExecution(T? value)
        {
            Task task;
            try
            {
                if (command.CanExecute(value) is false)
                {
                    lock (_gate)
                    {
                        _starting = false;
                    }
                    StartNextPending();
                    return;
                }
                task =
                    command.ExecuteAsync(value)
                    ?? throw new InvalidOperationException(
                        "The asynchronous command returned a null task."
                    );
            }
            catch (Exception error)
            {
                lock (_gate)
                {
                    _starting = false;
                }
                result.Fail(error);
                return;
            }

            lock (_gate)
            {
                _starting = false;
                if (_disposed)
                {
                    return;
                }
                _runningTasks.Add(task);
            }
            _ = ObserveCompletionAsync(task);
        }

        private async Task ObserveCompletionAsync(Task task)
        {
            Exception? failure = null;
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                lock (_gate)
                {
                    var cancellationWasRequested = _expectedCancellations.Contains(task);
                    if (!cancellationWasRequested || error is not OperationCanceledException)
                    {
                        failure = error;
                    }
                }
            }

            T? next = default;
            var hasNext = false;
            lock (_gate)
            {
                _runningTasks.Remove(task);
                _expectedCancellations.Remove(task);
                if (
                    failure is null
                    && !_disposed
                    && mode == AsyncConcurrencyMode.Sequential
                    && _queue.Count > 0
                )
                {
                    next = _queue.Dequeue();
                    _starting = true;
                    hasNext = true;
                }
                else if (
                    failure is null
                    && !_disposed
                    && mode == AsyncConcurrencyMode.Latest
                    && _hasLatestPending
                )
                {
                    next = _latestPending;
                    _latestPending = default;
                    _hasLatestPending = false;
                    _starting = true;
                    hasNext = true;
                }
            }

            if (failure is not null)
            {
                result.Fail(failure);
            }
            else if (hasNext)
            {
                StartExecution(next);
            }
        }

        private void StartNextPending()
        {
            T? next = default;
            var hasNext = false;
            lock (_gate)
            {
                if (!_disposed && mode == AsyncConcurrencyMode.Sequential && _queue.Count > 0)
                {
                    next = _queue.Dequeue();
                    _starting = true;
                    hasNext = true;
                }
                else if (!_disposed && mode == AsyncConcurrencyMode.Latest && _hasLatestPending)
                {
                    next = _latestPending;
                    _latestPending = default;
                    _hasLatestPending = false;
                    _starting = true;
                    hasNext = true;
                }
            }
            if (hasNext)
            {
                StartExecution(next);
            }
        }
    }
}
