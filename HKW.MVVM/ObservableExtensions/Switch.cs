namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 转发最近生成的内部可观察序列中的值
    /// </summary>
    public static IObservable<T> Switch<T>(this IObservable<IObservable<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return PreserveLogger(
            source,
            new AnonymousObservable<T>(observer =>
            {
                var subscription = new SwitchSubscription<T>(observer);
                subscription.Subscribe(source);
                return subscription;
            })
        );
    }
}
