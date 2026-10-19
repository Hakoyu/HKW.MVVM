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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return CreateCore(
            source,
            observer =>
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
            }
        );
    }
}
