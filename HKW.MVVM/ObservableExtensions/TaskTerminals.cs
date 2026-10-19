namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 返回第一个值; 当源序列为空且完成时返回默认值
    /// </summary>
    public static Task<TSource?> FirstOrDefaultAsync<TSource>(
        this IObservable<TSource> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Take(1).DefaultIfEmpty().ToTask(cancellationToken);
    }

    /// <summary>
    /// 返回第一个匹配的值; 当没有值匹配时返回默认值
    /// </summary>
    public static Task<TSource?> FirstOrDefaultAsync<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// 在源序列完成后返回最后一个值
    /// </summary>
    public static Task<TSource> LastAsync<TSource>(
        this IObservable<TSource> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.ToTask(cancellationToken);
    }

    /// <summary>
    /// 在源序列完成后返回最后一个匹配的值
    /// </summary>
    public static Task<TSource> LastAsync<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).LastAsync(cancellationToken);
    }

    /// <summary>
    /// 返回最后一个值; 当源序列为空且完成时返回默认值
    /// </summary>
    public static Task<TSource?> LastOrDefaultAsync<TSource>(
        this IObservable<TSource> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.DefaultIfEmpty().ToTask(cancellationToken);
    }

    /// <summary>
    /// 返回最后一个匹配的值; 当没有值匹配时返回默认值
    /// </summary>
    public static Task<TSource?> LastOrDefaultAsync<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).LastOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// 返回唯一的值; 当源序列不恰好包含一个值时失败
    /// </summary>
    public static Task<TSource> SingleAsync<TSource>(
        this IObservable<TSource> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(source, new SingleObservable<TSource>(source))
            .ToTask(cancellationToken);
    }

    /// <summary>
    /// 返回唯一匹配的值; 当不恰好有一个值匹配时失败
    /// </summary>
    public static Task<TSource> SingleAsync<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).SingleAsync(cancellationToken);
    }

    /// <summary>
    /// 返回唯一的值; 源序列为空时返回默认值, 包含多个值时失败
    /// </summary>
    public static Task<TSource?> SingleOrDefaultAsync<TSource>(
        this IObservable<TSource> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(source, new SingleObservable<TSource>(source))
            .DefaultIfEmpty()
            .ToTask(cancellationToken);
    }

    /// <summary>
    /// 返回唯一匹配的值; 没有匹配项时返回默认值, 存在多个匹配项时失败
    /// </summary>
    public static Task<TSource?> SingleOrDefaultAsync<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).SingleOrDefaultAsync(cancellationToken);
    }
}

internal sealed class SingleObservable<TSource>(IObservable<TSource> source) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new SingleObserver<TSource>(observer);
        sink.SetSubscription(source.Subscribe(sink));
        return sink;
    }
}

internal sealed class SingleObserver<TSource>(IObserver<TSource> observer)
    : OperatorObserver<TSource, TSource>(observer)
{
    private bool _hasValue;
    private TSource? _value;

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        if (_hasValue)
        {
            Fail(
                new InvalidOperationException(
                    "The observable sequence contains more than one element."
                )
            );
            return;
        }

        _hasValue = true;
        _value = value;
    }

    public override void OnCompleted()
    {
        if (!IsStopped)
        {
            if (_hasValue)
            {
                Observer.OnNext(_value!);
            }

            base.OnCompleted();
        }
    }
}
