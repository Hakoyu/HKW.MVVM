namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 当源以错误终止时,继续使用替换的可观察序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要监视错误的可观察序列</param>
    /// <param name="handler">将源错误映射为替换序列的函数</param>
    /// <returns>镜像源,或在发生错误后镜像其替换序列的可观察序列</returns>
    public static IObservable<TSource> Catch<TSource>(
        this IObservable<TSource> source,
        Func<Exception, IObservable<TSource>> handler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);
        return CreateCore(
            source,
            observer =>
            {
                var subscriptions = new MultipleDisposable();
                var stopped = 0;

                void OnCompleted()
                {
                    if (Interlocked.Exchange(ref stopped, 1) != 0)
                    {
                        return;
                    }

                    observer.OnCompleted();
                }

                void OnError(Exception exception)
                {
                    if (Interlocked.Exchange(ref stopped, 1) != 0)
                    {
                        return;
                    }

                    observer.OnError(exception);
                }

                subscriptions.Add(
                    source.Subscribe(
                        value =>
                        {
                            if (Volatile.Read(ref stopped) != 0)
                            {
                                return;
                            }

                            observer.OnNext(value);
                        },
                        error =>
                        {
                            if (Volatile.Read(ref stopped) != 0)
                            {
                                return;
                            }

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
                                OnError(exception);
                                return;
                            }

                            try
                            {
                                subscriptions.Add(
                                    replacement.Subscribe(
                                        value =>
                                        {
                                            if (Volatile.Read(ref stopped) != 0)
                                            {
                                                return;
                                            }

                                            observer.OnNext(value);
                                        },
                                        OnError,
                                        OnCompleted
                                    )
                                );
                            }
                            catch (Exception exception)
                            {
                                OnError(exception);
                            }
                        },
                        OnCompleted
                    )
                );

                return subscriptions;
            }
        );
    }
}
