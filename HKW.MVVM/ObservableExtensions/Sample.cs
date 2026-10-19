namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>定期发出源的最新值</summary>
    public static IObservable<TSource> Sample<TSource>(
        this IObservable<TSource> source,
        TimeSpan interval
    ) => Sample(source, interval, TimeProvider.System);

    /// <summary>使用提供的时间提供程序定期发出源的最新值</summary>
    public static IObservable<TSource> Sample<TSource>(
        this IObservable<TSource> source,
        TimeSpan interval,
        TimeProvider timeProvider
    )
    {
        ValidateTimeArguments(source, interval, timeProvider);
        return Create(
            source,
            observer => new SampleSubscription<TSource>(source, observer, interval, timeProvider)
        );
    }
}
