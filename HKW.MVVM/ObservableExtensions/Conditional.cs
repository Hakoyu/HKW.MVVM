namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 返回可观察序列产生的第一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要获取第一个值的可观察序列</param>
    /// <param name="cancellationToken">用于取消等待和释放订阅的令牌</param>
    /// <returns>表示序列第一个值的任务</returns>
    /// <exception cref="InvalidOperationException">序列未产生任何值即完成</exception>
    public static Task<TSource> FirstAsync<TSource>(
        this IObservable<TSource> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Take(1).ToTask(cancellationToken);
    }

    /// <summary>
    /// 返回可观察序列中满足指定条件的第一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要搜索的可观察序列</param>
    /// <param name="predicate">用于确定值是否匹配的函数</param>
    /// <param name="cancellationToken">用于取消等待和释放订阅的令牌</param>
    /// <returns>表示第一个匹配值的任务</returns>
    /// <exception cref="InvalidOperationException">序列未产生匹配值即完成</exception>
    public static Task<TSource> FirstAsync<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).Take(1).ToTask(cancellationToken);
    }

    /// <summary>
    /// 在指定条件保持为 <see langword="true"/> 时发出源值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要从中获取值的可观察序列</param>
    /// <param name="predicate">用于确定是否继续发出值的函数</param>
    /// <returns>包含条件保持为 <see langword="true"/> 时的源值的可观察序列</returns>
    public static IObservable<TSource> TakeWhile<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return TakeWhile(source, (value, _) => predicate(value));
    }

    /// <summary>
    /// 在指定的值和索引条件保持为 <see langword="true"/> 时发出源值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要从中获取值的可观察序列</param>
    /// <param name="predicate">用于根据值及其从零开始的索引确定是否继续发出值的函数</param>
    /// <returns>包含条件保持为 <see langword="true"/> 时的源值的可观察序列</returns>
    public static IObservable<TSource> TakeWhile<TSource>(
        this IObservable<TSource> source,
        Func<TSource, int, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return PreserveLogger(source, new TakeWhileObservable<TSource>(source, predicate));
    }

    /// <summary>
    /// 跳过满足指定条件的连续起始值,然后发出其余值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要跳过其起始值的可观察序列</param>
    /// <param name="predicate">用于确定是否继续跳过值的函数</param>
    /// <returns>包含第一个不匹配值及其后续值的可观察序列</returns>
    public static IObservable<TSource> SkipWhile<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return SkipWhile(source, (value, _) => predicate(value));
    }

    /// <summary>
    /// 跳过满足指定值和索引条件的连续起始值,然后发出其余值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要跳过其起始值的可观察序列</param>
    /// <param name="predicate">用于根据值及其从零开始的索引确定是否继续跳过值的函数</param>
    /// <returns>包含第一个不匹配值及其后续值的可观察序列</returns>
    public static IObservable<TSource> SkipWhile<TSource>(
        this IObservable<TSource> source,
        Func<TSource, int, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return PreserveLogger(source, new SkipWhileObservable<TSource>(source, predicate));
    }
}

internal sealed class TakeWhileObservable<TSource>(
    IObservable<TSource> source,
    Func<TSource, int, bool> predicate
) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new TakeWhileObserver<TSource>(observer, predicate);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class TakeWhileObserver<TSource>(
    IObserver<TSource> observer,
    Func<TSource, int, bool> predicate
) : OperatorObserver<TSource, TSource>(observer)
{
    private int _index;

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        bool shouldTake;
        try
        {
            shouldTake = predicate(value, _index);
            _index = checked(_index + 1);
        }
        catch (Exception error)
        {
            Fail(error);
            return;
        }

        if (shouldTake)
        {
            Observer.OnNext(value);
        }
        else
        {
            OnCompleted();
        }
    }
}

internal sealed class SkipWhileObservable<TSource>(
    IObservable<TSource> source,
    Func<TSource, int, bool> predicate
) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new SkipWhileObserver<TSource>(observer, predicate);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class SkipWhileObserver<TSource>(
    IObserver<TSource> observer,
    Func<TSource, int, bool> predicate
) : OperatorObserver<TSource, TSource>(observer)
{
    private int _index;
    private bool _yielding;

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        if (_yielding is false)
        {
            bool shouldSkip;
            try
            {
                shouldSkip = predicate(value, _index);
                _index = checked(_index + 1);
            }
            catch (Exception error)
            {
                Fail(error);
                return;
            }

            _yielding = shouldSkip is false;
        }

        if (_yielding)
        {
            Observer.OnNext(value);
        }
    }
}
