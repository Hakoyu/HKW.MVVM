using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace HKW.MVVM;

/// <summary>
/// 指定 <c>SelectAsync</c> 如何处理重叠的异步操作
/// </summary>
public enum AsyncConcurrencyMode
{
    /// <summary>
    /// 新值到达时取消上一个操作
    /// </summary>
    Latest,

    /// <summary>
    /// 按照源序列顺序逐个处理值
    /// </summary>
    Sequential,

    /// <summary>
    /// 操作执行期间忽略接收到的值
    /// </summary>
    Drop,

    /// <summary>
    /// 并发处理所有值, 并按完成顺序发出结果
    /// </summary>
    Concurrent,
}

public static partial class ObservableExtensions
{
    private const int DefaultAsyncCapacity = 16;

    /// <summary>
    /// 通过有界通道将异步序列转换为可观察序列
    /// </summary>
    /// <param name="source">要使用的异步序列</param>
    /// <param name="capacity">正的有界通道容量; 默认为 16</param>
    /// <remarks>通道已满时, 生产操作会异步等待, 从而施加有界背压</remarks>
    public static IObservable<TSource> FromAsyncEnumerable<TSource>(
        IAsyncEnumerable<TSource> source,
        int capacity = DefaultAsyncCapacity
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return FromAsyncEnumerable(_ => source, capacity);
    }

    /// <summary>
    /// 通过有界通道, 根据异步序列工厂创建可观察序列
    /// </summary>
    /// <param name="sourceFactory">每次订阅时调用、且能够响应取消操作的工厂</param>
    /// <param name="capacity">正的有界通道容量; 默认为 16</param>
    /// <remarks>通道已满时, 生产操作会异步等待, 从而施加有界背压</remarks>
    public static IObservable<TSource> FromAsyncEnumerable<TSource>(
        Func<CancellationToken, IAsyncEnumerable<TSource>> sourceFactory,
        int capacity = DefaultAsyncCapacity
    )
    {
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ValidateCapacity(capacity);
        return Create<TSource>(observer =>
        {
            var subscription = new AsyncEnumerableSubscription<TSource>(observer, capacity);
            subscription.Start(sourceFactory);
            return subscription;
        });
    }

    /// <summary>
    /// 将可观察序列转换为有界异步序列
    /// </summary>
    /// <param name="source">要使用的可观察序列</param>
    /// <param name="capacity">正的有界通道容量; 默认为 16</param>
    /// <param name="cancellationToken">停止枚举并释放源订阅</param>
    /// <remarks>
    /// 源序列在一个线程池任务中订阅通道已满时, 同步生产者将会等待
    /// 请勿创建生产者、消费者和必需的单线程调度程序相互等待的循环; 
    /// 即使使用了有界背压, 这种循环仍可能导致死锁
    /// </remarks>
    public static async IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(
        this IObservable<TSource> source,
        int capacity = DefaultAsyncCapacity,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateCapacity(capacity);

        var channel = Channel.CreateBounded<TSource>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            }
        );
        var subscription = new SingleAssignmentDisposable();
        using var registration = cancellationToken.Register(() =>
        {
            channel.Writer.TryComplete(new OperationCanceledException(cancellationToken));
            subscription.Dispose();
        });

        _ = Task.Run(() =>
        {
            try
            {
                var resource = source.Subscribe(
                    value =>
                        channel
                            .Writer.WriteAsync(value, cancellationToken)
                            .AsTask()
                            .GetAwaiter()
                            .GetResult(),
                    error => channel.Writer.TryComplete(error),
                    () => channel.Writer.TryComplete()
                );
                subscription.Disposable =
                    resource
                    ?? throw new InvalidOperationException(
                        "The source returned a null subscription."
                    );
            }
            catch (Exception error)
            {
                channel.Writer.TryComplete(error);
            }
        });

        try
        {
            await foreach (
                var value in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                yield return value;
            }
        }
        finally
        {
            subscription.Dispose();
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// 使用指定的并发行为异步投影源值
    /// </summary>
    public static IObservable<TResult> SelectAsync<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> selector,
        AsyncConcurrencyMode mode = AsyncConcurrencyMode.Latest
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        if (Enum.IsDefined(mode) is false)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        return Create<TResult>(observer =>
        {
            var subscription = new SelectAsyncSubscription<TSource, TResult>(
                observer,
                selector,
                mode
            );
            subscription.Start(source);
            return subscription;
        });
    }

    /// <summary>
    /// 使用指定的并发行为异步投影源值
    /// </summary>
    public static IObservable<TResult> SelectAsync<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, Task<TResult>> selector,
        AsyncConcurrencyMode mode = AsyncConcurrencyMode.Latest
    )
    {
        ArgumentNullException.ThrowIfNull(selector);
        return SelectAsync(source, (value, _) => selector(value), mode);
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
    }

    private sealed class AsyncEnumerableSubscription<T>(IObserver<T> observer, int capacity)
        : IDisposable
    {
        private readonly
#if NET10_0_OR_GREATER
        Lock
#else
        object
#endif
        _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Channel<T> _channel = Channel.CreateBounded<T>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            }
        );
        private bool _stopped;

        public void Start(Func<CancellationToken, IAsyncEnumerable<T>> sourceFactory)
        {
            _ = ProduceAsync(sourceFactory);
            _ = ConsumeAsync();
        }

        private async Task ProduceAsync(Func<CancellationToken, IAsyncEnumerable<T>> sourceFactory)
        {
            try
            {
                var source =
                    sourceFactory(_cancellation.Token)
                    ?? throw new InvalidOperationException(
                        "The asynchronous sequence factory returned null."
                    );
                await foreach (
                    var value in source.WithCancellation(_cancellation.Token).ConfigureAwait(false)
                )
                {
                    await _channel
                        .Writer.WriteAsync(value, _cancellation.Token)
                        .ConfigureAwait(false);
                }

                _channel.Writer.TryComplete();
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                _channel.Writer.TryComplete();
            }
            catch (Exception error)
            {
                _channel.Writer.TryComplete(error);
            }
        }

        private async Task ConsumeAsync()
        {
            try
            {
                await foreach (
                    var value in _channel
                        .Reader.ReadAllAsync(_cancellation.Token)
                        .ConfigureAwait(false)
                )
                {
                    lock (_gate)
                    {
                        if (_stopped)
                        {
                            return;
                        }

                        observer.OnNext(value);
                    }
                }

                Stop(null);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
            catch (Exception error)
            {
                Stop(error);
            }
        }

        private void Stop(Exception? error)
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                if (error is null)
                {
                    observer.OnCompleted();
                }
                else
                {
                    observer.OnError(error);
                }
            }

            _cancellation.Dispose();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
            }

            _cancellation.Cancel();
            _channel.Writer.TryComplete();
            _cancellation.Dispose();
        }
    }

    private sealed class SelectAsyncSubscription<TSource, TResult> : IObserver<TSource>, IDisposable
    {
        private readonly
#if NET10_0_OR_GREATER
        Lock
#else
        object
#endif
        _gate = new();
        private readonly IObserver<TResult> _observer;
        private readonly Func<TSource, CancellationToken, Task<TResult>> _selector;
        private readonly AsyncConcurrencyMode _mode;
        private readonly SingleAssignmentDisposable _upstream = new();
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Queue<TSource> _queue = new();
        private readonly HashSet<CancellationTokenSource> _operations = [];
        private bool _sourceCompleted;
        private bool _busy;
        private bool _stopped;
        private long _latestVersion;

        public SelectAsyncSubscription(
            IObserver<TResult> observer,
            Func<TSource, CancellationToken, Task<TResult>> selector,
            AsyncConcurrencyMode mode
        )
        {
            _observer = observer;
            _selector = selector;
            _mode = mode;
        }

        public void Start(IObservable<TSource> source)
        {
            try
            {
                _upstream.Disposable =
                    source.Subscribe(this)
                    ?? throw new InvalidOperationException(
                        "The source returned a null subscription."
                    );
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        public void OnNext(TSource value)
        {
            CancellationTokenSource? previous = null;
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                if (_mode == AsyncConcurrencyMode.Sequential && _busy)
                {
                    _queue.Enqueue(value);
                    return;
                }

                if (_mode == AsyncConcurrencyMode.Drop && _busy)
                {
                    return;
                }

                if (_mode == AsyncConcurrencyMode.Latest)
                {
                    previous = _operations.FirstOrDefault();
                    if (previous is not null)
                    {
                        _operations.Remove(previous);
                    }
                }

                _busy = true;
            }

            SafeCancel(previous);
            previous?.Dispose();
            StartOperation(value);
        }

        private void StartOperation(TSource value)
        {
            var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            long version;
            lock (_gate)
            {
                if (_stopped)
                {
                    operation.Dispose();
                    return;
                }

                _operations.Add(operation);
                version = ++_latestVersion;
            }

            Task<TResult> task;
            try
            {
                task =
                    _selector(value, operation.Token)
                    ?? throw new InvalidOperationException(
                        "The asynchronous selector returned null."
                    );
            }
            catch (Exception error)
            {
                OperationFinished(operation, version, default, error);
                return;
            }

            _ = AwaitOperationAsync(task, operation, version);
        }

        private async Task AwaitOperationAsync(
            Task<TResult> task,
            CancellationTokenSource operation,
            long version
        )
        {
            try
            {
                var result = await task.ConfigureAwait(false);
                OperationFinished(operation, version, result, null);
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
                OperationFinished(operation, version, default, null);
            }
            catch (Exception error)
            {
                OperationFinished(operation, version, default, error);
            }
        }

        private void OperationFinished(
            CancellationTokenSource operation,
            long version,
            TResult? result,
            Exception? error
        )
        {
            TSource? next = default;
            var hasNext = false;
            var complete = false;
            var emit = false;
            lock (_gate)
            {
                if (_operations.Remove(operation) is false)
                {
                    operation.Dispose();
                    return;
                }

                if (_stopped)
                {
                    operation.Dispose();
                    return;
                }

                if (error is not null)
                {
                    _stopped = true;
                }
                else if (_mode != AsyncConcurrencyMode.Latest || version == _latestVersion)
                {
                    emit = operation.IsCancellationRequested is false;
                }

                if (_mode is AsyncConcurrencyMode.Sequential or AsyncConcurrencyMode.Drop)
                {
                    _busy = false;
                }

                if (error is null && _mode == AsyncConcurrencyMode.Sequential && _queue.Count > 0)
                {
                    next = _queue.Dequeue();
                    _busy = true;
                    hasNext = true;
                }

                complete = error is null && _sourceCompleted && _operations.Count == 0 && !hasNext;
                if (complete)
                {
                    _stopped = true;
                }

                if (emit)
                {
                    _observer.OnNext(result!);
                }

                if (error is not null)
                {
                    _observer.OnError(error);
                }
                else if (complete)
                {
                    _observer.OnCompleted();
                }
            }

            operation.Dispose();
            if (error is not null)
            {
                DisposeResources();
            }
            else if (hasNext)
            {
                StartOperation(next!);
            }
            else if (complete)
            {
                DisposeResources();
            }
        }

        public void OnError(Exception error) => Fail(error);

        public void OnCompleted()
        {
            var complete = false;
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _sourceCompleted = true;
                complete = _operations.Count == 0 && _queue.Count == 0;
                if (complete)
                {
                    _stopped = true;
                    _observer.OnCompleted();
                }
            }

            if (complete)
            {
                DisposeResources();
            }
        }

        private void Fail(Exception error)
        {
            ArgumentNullException.ThrowIfNull(error);
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                _observer.OnError(error);
            }

            DisposeResources();
        }

        private static void SafeCancel(CancellationTokenSource? cancellation)
        {
            if (cancellation is null)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException) { }
            catch (AggregateException) { }
        }

        private void DisposeResources()
        {
            _upstream.Dispose();
            SafeCancel(_lifetime);
            _lifetime.Dispose();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                _queue.Clear();
            }

            DisposeResources();
        }
    }
}
