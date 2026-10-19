namespace HKW.MVVM;

/// <summary>
/// 表示不包含值的异步操作结果
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// 获取唯一有意义的 <see cref="Unit"/> 值
    /// </summary>
    public static Unit Default { get; } = new();

    /// <inheritdoc/>
    public override string ToString() => "()";
}

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将任务转换为发出其结果的可观察序列
    /// </summary>
    /// <typeparam name="TSource">任务结果类型</typeparam>
    /// <param name="task">要观察的任务</param>
    /// <returns>发出任务结果后完成的可观察序列</returns>
    public static IObservable<TSource> FromAsync<TSource>(Task<TSource> task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return FromAsync<TSource>(_ => task);
    }

    /// <summary>
    /// 将任务转换为发出 <see cref="Unit"/> 的可观察序列
    /// </summary>
    /// <param name="task">要观察的任务</param>
    /// <returns>任务成功后发出 <see cref="Unit"/> 并完成的可观察序列</returns>
    public static IObservable<Unit> FromAsync(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return FromAsync(_ => task);
    }

    /// <summary>
    /// 使用异步工厂为每个订阅创建可观察序列
    /// </summary>
    /// <typeparam name="TSource">任务结果类型</typeparam>
    /// <param name="taskFactory">为每个订阅创建任务的工厂</param>
    /// <returns>发出任务结果后完成的可观察序列</returns>
    public static IObservable<TSource> FromAsync<TSource>(Func<Task<TSource>> taskFactory)
    {
        ArgumentNullException.ThrowIfNull(taskFactory);
        return FromAsync<TSource>(_ => taskFactory());
    }

    /// <summary>
    /// 使用异步工厂为每个订阅创建可观察序列
    /// </summary>
    /// <param name="taskFactory">为每个订阅创建任务的工厂</param>
    /// <returns>任务成功后发出 <see cref="Unit"/> 并完成的可观察序列</returns>
    public static IObservable<Unit> FromAsync(Func<Task> taskFactory)
    {
        ArgumentNullException.ThrowIfNull(taskFactory);
        return FromAsync(_ => taskFactory());
    }

    /// <summary>
    /// 使用支持取消的异步工厂为每个订阅创建可观察序列
    /// </summary>
    /// <typeparam name="TSource">任务结果类型</typeparam>
    /// <param name="taskFactory">接收订阅取消令牌并创建任务的工厂</param>
    /// <returns>发出任务结果后完成的可观察序列</returns>
    public static IObservable<TSource> FromAsync<TSource>(
        Func<CancellationToken, Task<TSource>> taskFactory
    )
    {
        ArgumentNullException.ThrowIfNull(taskFactory);
        return Create<TSource>(observer =>
        {
            var subscription = new AsyncSubscription<TSource>(observer);
            Task<TSource> task;
            try
            {
                task = taskFactory(subscription.CancellationToken);
                if (task is null)
                {
                    throw new InvalidOperationException("The task factory returned null.");
                }
            }
            catch
            {
                subscription.Dispose();
                throw;
            }

            subscription.Start(task);
            return subscription;
        });
    }

    /// <summary>
    /// 使用支持取消的异步工厂为每个订阅创建可观察序列
    /// </summary>
    /// <param name="taskFactory">接收订阅取消令牌并创建任务的工厂</param>
    /// <returns>任务成功后发出 <see cref="Unit"/> 并完成的可观察序列</returns>
    public static IObservable<Unit> FromAsync(Func<CancellationToken, Task> taskFactory)
    {
        ArgumentNullException.ThrowIfNull(taskFactory);
        return FromAsync<Unit>(async cancellationToken =>
        {
            var task = taskFactory(cancellationToken);
            if (task is null)
            {
                throw new InvalidOperationException("The task factory returned null.");
            }

            await task.ConfigureAwait(false);
            return Unit.Default;
        });
    }

    /// <summary>
    /// 将可观察序列转换为返回最后一个值的任务
    /// </summary>
    /// <typeparam name="TSource">序列值类型</typeparam>
    /// <param name="source">要转换的可观察序列</param>
    /// <param name="cancellationToken">用于取消等待和释放订阅的令牌</param>
    /// <returns>返回序列最后一个值的任务</returns>
    /// <exception cref="InvalidOperationException">序列未产生任何值即完成</exception>
    public static Task<TSource> ToTask<TSource>(
        this IObservable<TSource> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<TSource>(cancellationToken);
        }

        var state = new ToTaskObserver<TSource>(cancellationToken);
        state.Start(source);
        return state.Task;
    }

    private sealed class AsyncSubscription<T>(IObserver<T> observer) : IDisposable
    {
        private readonly Lock _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private bool _stopped;

        public CancellationToken CancellationToken => _cancellation.Token;

        public void Start(Task<T> task) => _ = RunAsync(task);

        private async Task RunAsync(Task<T> task)
        {
            T result;
            try
            {
                result = await task.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                NotifyError(exception);
                return;
            }

            NotifySuccess(result);
        }

        private void NotifySuccess(T result)
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                try
                {
                    observer.OnNext(result);
                    observer.OnCompleted();
                }
                catch
                {
                    // Observer callbacks cannot be propagated from an asynchronous continuation.
                }
            }

            _cancellation.Dispose();
        }

        private void NotifyError(Exception exception)
        {
            lock (_gate)
            {
                if (_stopped || _cancellation.IsCancellationRequested)
                {
                    return;
                }

                _stopped = true;
                try
                {
                    observer.OnError(exception);
                }
                catch
                {
                    // Observer callbacks cannot be propagated from an asynchronous continuation.
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
                try
                {
                    _cancellation.Cancel();
                }
                catch (AggregateException)
                {
                    // Cancellation callback failures must not prevent subscription disposal.
                }
            }

            _cancellation.Dispose();
        }
    }

    private sealed class ToTaskObserver<T>(CancellationToken cancellationToken) : IObserver<T>
    {
        private readonly Lock _gate = new();
        private readonly SingleAssignmentDisposable _subscription = new();
        private readonly TaskCompletionSource<T> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private CancellationTokenRegistration _registration;
        private bool _hasValue;
        private bool _stopped;
        private T? _lastValue;

        public Task<T> Task => _completion.Task;

        public void Start(IObservable<T> source)
        {
            var registration = cancellationToken.Register(
                static state => ((ToTaskObserver<T>)state!).Cancel(),
                this
            );
            SetRegistration(registration);

            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }
            }

            IDisposable resource;
            try
            {
                resource = source.Subscribe(this);
                if (resource is null)
                {
                    throw new InvalidOperationException("The source returned a null subscription.");
                }
            }
            catch (Exception exception)
            {
                OnError(exception);
                return;
            }

            _subscription.Disposable = resource;
        }

        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _hasValue = true;
                _lastValue = value;
            }
        }

        public void OnError(Exception error)
        {
            ArgumentNullException.ThrowIfNull(error);
            if (TryStop() is false)
            {
                return;
            }

            DisposeResources();
            _completion.TrySetException(error);
        }

        public void OnCompleted()
        {
            T? value;
            bool hasValue;
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                hasValue = _hasValue;
                value = _lastValue;
            }

            DisposeResources();
            if (hasValue)
            {
                _completion.TrySetResult(value!);
            }
            else
            {
                _completion.TrySetException(
                    new InvalidOperationException("The observable sequence contains no elements.")
                );
            }
        }

        private void Cancel()
        {
            if (TryStop() is false)
            {
                return;
            }

            DisposeResources();
            _completion.TrySetCanceled(cancellationToken);
        }

        private bool TryStop()
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return false;
                }

                _stopped = true;
                return true;
            }
        }

        private void SetRegistration(CancellationTokenRegistration registration)
        {
            var dispose = false;
            lock (_gate)
            {
                if (_stopped)
                {
                    dispose = true;
                }
                else
                {
                    _registration = registration;
                }
            }

            if (dispose)
            {
                registration.Dispose();
            }
        }

        private void DisposeResources()
        {
            try
            {
                _subscription.Dispose();
            }
            catch
            {
                // Source disposal failures must not prevent task termination.
            }

            _registration.Dispose();
        }
    }
}
