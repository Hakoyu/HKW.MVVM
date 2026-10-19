namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将源值收集到固定大小的缓冲区中,并在缓冲区满时发出
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要缓冲的可观察序列</param>
    /// <param name="count">每个缓冲区包含的最大元素数</param>
    /// <returns>发出连续非重叠缓冲区的可观察序列</returns>
    /// <remarks>源成功完成时会先发出剩余的非空缓冲区</remarks>
    public static IObservable<IList<TSource>> Buffer<TSource>(
        this IObservable<TSource> source,
        int count
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        return BufferCore(
            source,
            System.Threading.Timeout.InfiniteTimeSpan,
            count,
            useTimer: false
        );
    }

    /// <summary>
    /// 将指定时间窗口内的源值收集到缓冲区中,并在每个窗口结束时发出
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要缓冲的可观察序列</param>
    /// <param name="timeSpan">每个缓冲窗口的时长</param>
    /// <returns>发出连续非重叠缓冲区的可观察序列</returns>
    /// <remarks>没有源值的时间窗口会发出空列表</remarks>
    public static IObservable<IList<TSource>> Buffer<TSource>(
        this IObservable<TSource> source,
        TimeSpan timeSpan
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeSpan, TimeSpan.Zero);
        return BufferCore(source, timeSpan, int.MaxValue, useTimer: true);
    }

    /// <summary>
    /// 将源值收集到缓冲区中,并在达到时间或数量边界时发出
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要缓冲的可观察序列</param>
    /// <param name="timeSpan">缓冲区允许保持打开的最长时间</param>
    /// <param name="count">每个缓冲区包含的最大元素数</param>
    /// <returns>发出连续非重叠缓冲区的可观察序列</returns>
    /// <remarks>任一边界先到都会打开新的时间窗口;空的时间窗口会发出空列表</remarks>
    public static IObservable<IList<TSource>> Buffer<TSource>(
        this IObservable<TSource> source,
        TimeSpan timeSpan,
        int count
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeSpan, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        return BufferCore(source, timeSpan, count, useTimer: true);
    }

    private static IObservable<IList<TSource>> BufferCore<TSource>(
        IObservable<TSource> source,
        TimeSpan timeSpan,
        int count,
        bool useTimer
    ) =>
        PreserveLogger(
            source,
            CreateCore<IList<TSource>>(observer => new BufferSubscription<TSource>(
                source,
                observer,
                timeSpan,
                count,
                useTimer,
                TimeProvider.System
            ))
        );
}
