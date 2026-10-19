namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 通过同步上下文派发源的值,错误和完成通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要派发其通知的可观察序列</param>
    /// <param name="synchronizationContext">接收通知的同步上下文</param>
    /// <returns>通知被投递到该上下文的可观察序列</returns>
    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return ObserveOn(
            source,
            ObservableSchedulers.FromSynchronizationContext(synchronizationContext)
        );
    }

    /// <summary>
    /// 通过选定的调度器派发源通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要派发其通知的可观察序列</param>
    /// <param name="scheduler">用于观察者通知的调度器</param>
    /// <returns>通知在选定调度器上进行调度的可观察序列</returns>
    /// <remarks>通知按源顺序进行调度.</remarks>
    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        IObservableScheduler scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scheduler);

        return CreateCore(
            source,
            observer =>
            {
                var dispatcher = new NotificationDispatcher<TSource>(observer, scheduler);
                var subscription = source.Subscribe(
                    dispatcher.OnNext,
                    dispatcher.OnError,
                    dispatcher.OnCompleted
                );
                dispatcher.SetSubscription(subscription);
                return dispatcher;
            }
        );
    }
}
