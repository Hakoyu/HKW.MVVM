namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在同步上下文上调度对源的订阅
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要订阅的可观察序列</param>
    /// <param name="synchronizationContext">用于订阅操作的同步上下文</param>
    /// <returns>其源订阅被投递到该上下文的可观察序列</returns>
    public static IObservable<TSource> SubscribeOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return SubscribeOnCore(
            source,
            ObservableSchedulers.FromSynchronizationContext(synchronizationContext)
        );
    }

    /// <summary>
    /// 在选定的调度器上调度对源的订阅
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要订阅的可观察序列</param>
    /// <param name="scheduler">用于订阅操作的调度器</param>
    /// <returns>其源订阅被调度的可观察序列</returns>
    /// <remarks>在计划的操作执行前释放订阅,可避免对源进行订阅.</remarks>
    public static IObservable<TSource> SubscribeOn<TSource>(
        this IObservable<TSource> source,
        IObservableScheduler scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scheduler);
        return SubscribeOnCore(source, scheduler);
    }
}
