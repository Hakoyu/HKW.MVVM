namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 返回序列中的元素数
    /// </summary>
    public static IObservable<int> Count<TSource>(this IObservable<TSource> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Aggregate(0, static (count, _) => checked(count + 1));
    }

    /// <summary>
    /// 返回序列中满足条件的元素数
    /// </summary>
    public static IObservable<int> Count<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return source.Where(predicate).Count();
    }

    /// <summary>
    /// 在没有种子的情况下聚合序列
    /// </summary>
    public static IObservable<TSource> Aggregate<TSource>(
        this IObservable<TSource> source,
        Func<TSource, TSource, TSource> accumulator
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accumulator);
        return PreserveLogger(source, new AggregateObservable<TSource, TSource>(source, accumulator));
    }

    /// <summary>
    /// 从指定种子开始聚合序列
    /// </summary>
    public static IObservable<TAccumulate> Aggregate<TSource, TAccumulate>(
        this IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> accumulator
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accumulator);
        return PreserveLogger(
            source,
            new AggregateObservable<TSource, TAccumulate>(source, seed, accumulator)
        );
    }

    /// <summary>
    /// 聚合序列并投影最终结果
    /// </summary>
    public static IObservable<TResult> Aggregate<TSource, TAccumulate, TResult>(
        this IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> accumulator,
        Func<TAccumulate, TResult> resultSelector
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accumulator);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return source.Aggregate(seed, accumulator).Select(resultSelector);
    }
}

internal sealed class AggregateObservable<TSource, TAccumulate> : IObservable<TAccumulate>
{
    private readonly IObservable<TSource> _source;
    private readonly Func<TAccumulate, TSource, TAccumulate> _accumulator;
    private readonly bool _hasSeed;
    private readonly TAccumulate? _seed;

    public AggregateObservable(
        IObservable<TSource> source,
        Func<TAccumulate, TSource, TAccumulate> accumulator
    )
    {
        _source = source;
        _accumulator = accumulator;
    }

    public AggregateObservable(
        IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> accumulator
    )
    {
        _source = source;
        _seed = seed;
        _hasSeed = true;
        _accumulator = accumulator;
    }

    public IDisposable Subscribe(IObserver<TAccumulate> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var sink = new AggregateObserver<TSource, TAccumulate>(
            observer,
            _hasSeed,
            _seed,
            _accumulator
        );
        sink.SetSubscription(_source.Subscribe(sink));
        return sink;
    }
}

internal sealed class AggregateObserver<TSource, TAccumulate>(
    IObserver<TAccumulate> observer,
    bool hasValue,
    TAccumulate? value,
    Func<TAccumulate, TSource, TAccumulate> accumulator
) : OperatorObserver<TSource, TAccumulate>(observer)
{
    private bool _hasValue = hasValue;
    private TAccumulate? _value = value;

    public override void OnNext(TSource value)
    {
        if (IsStopped)
        {
            return;
        }

        if (!_hasValue)
        {
            _hasValue = true;
            _value = (TAccumulate)(object?)value!;
            return;
        }

        try
        {
            _value = accumulator(_value!, value);
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    public override void OnCompleted()
    {
        if (IsStopped)
        {
            return;
        }

        if (!_hasValue)
        {
            Fail(new InvalidOperationException("The observable sequence contains no elements."));
            return;
        }

        Observer.OnNext(_value!);
        base.OnCompleted();
    }
}
