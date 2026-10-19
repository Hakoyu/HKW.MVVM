namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>将源的每个通知延迟 <paramref name="dueTime"/></summary>
    public static IObservable<TSource> Delay<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime
    ) => Delay(source, dueTime, TimeProvider.System);

    /// <summary>使用提供的时间提供程序延迟源的每个通知</summary>
    public static IObservable<TSource> Delay<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        TimeProvider timeProvider
    )
    {
        ValidateTimeArguments(source, dueTime, timeProvider);
        return Create(
            source,
            observer => new DelaySubscription<TSource>(source, observer, dueTime, timeProvider)
        );
    }
}
