namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 创建一个不发出任何值即完成的可观察序列
    /// </summary>
    /// <typeparam name="TSource">序列值类型</typeparam>
    /// <returns>立即完成的空可观察序列</returns>
    public static IObservable<TSource> Empty<TSource>() =>
        Create<TSource>(observer =>
        {
            observer.OnCompleted();
            return _emptyDisposable;
        });
}
