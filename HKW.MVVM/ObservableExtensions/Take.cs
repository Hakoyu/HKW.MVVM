namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 最多发出指定数量的源值,然后完成
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要从中取值的可观察序列</param>
    /// <param name="count">要发出的最大数量</param>
    /// <returns>最多包含 <paramref name="count"/> 个值的可观察序列</returns>
    public static IObservable<TSource> Take<TSource>(this IObservable<TSource> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return Create(
                source,
                observer =>
                {
                    observer.OnCompleted();
                    return _emptyDisposable;
                }
            );
        }

        return Create(
            source,
            observer =>
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
            }
        );
    }
}
