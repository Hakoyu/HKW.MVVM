namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在转发每个值之前对其调用一个操作,并原样转发该值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要检查的可观察序列</param>
    /// <param name="onNext">对每个值调用的副作用操作</param>
    /// <returns>镜像源的可观察序列</returns>
    public static IObservable<TSource> Do<TSource>(
        this IObservable<TSource> source,
        Action<TSource> onNext
    )
    {
        ArgumentNullException.ThrowIfNull(onNext);
        return Do(source, onNext, null, null, null, null);
    }

    /// <summary>
    /// 在订阅、通知和释放生命周期点执行副作用, 并原样转发源序列
    /// </summary>
    public static IObservable<TSource> Do<TSource>(
        this IObservable<TSource> source,
        Action<TSource>? onNext = null,
        Action<Exception>? onError = null,
        Action? onCompleted = null,
        Action? onSubscribe = null,
        Action? onDispose = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return CreateCore(
            source,
            observer =>
            {
                var stopped = false;
                var disposeNotified = 0;
                var subscription = new SingleAssignmentDisposable();

                void NotifyDisposed()
                {
                    if (Interlocked.Exchange(ref disposeNotified, 1) == 0)
                    {
                        onDispose?.Invoke();
                    }
                }

                try
                {
                    onSubscribe?.Invoke();
                    subscription.Disposable = source.Subscribe(
                        value =>
                        {
                            if (stopped)
                            {
                                return;
                            }

                            try
                            {
                                onNext?.Invoke(value);
                            }
                            catch (Exception exception)
                            {
                                ForwardError(observer, subscription, ref stopped, exception);
                                NotifyDisposed();
                                return;
                            }

                            observer.OnNext(value);
                        },
                        error =>
                        {
                            try
                            {
                                onError?.Invoke(error);
                            }
                            catch (Exception exception)
                            {
                                error = exception;
                            }

                            ForwardError(observer, subscription, ref stopped, error);
                            NotifyDisposed();
                        },
                        () =>
                        {
                            try
                            {
                                onCompleted?.Invoke();
                            }
                            catch (Exception exception)
                            {
                                ForwardError(observer, subscription, ref stopped, exception);
                                NotifyDisposed();
                                return;
                            }

                            ForwardCompletion(observer, subscription, ref stopped);
                            NotifyDisposed();
                        }
                    );
                }
                catch (Exception exception)
                {
                    ForwardError(observer, subscription, ref stopped, exception);
                    NotifyDisposed();
                }

                return new ActionDisposable(() =>
                {
                    subscription.Dispose();
                    NotifyDisposed();
                });
            }
        );
    }
}
