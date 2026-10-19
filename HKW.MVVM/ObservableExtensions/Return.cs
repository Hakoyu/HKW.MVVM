namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 创建一个发出单个值后即完成的可观察序列
    /// </summary>
    /// <typeparam name="TSource">发出的值类型</typeparam>
    /// <param name="value">要发出的单个值</param>
    /// <returns>仅包含一个值的可观察序列</returns>
    public static IObservable<TSource> Return<TSource>(TSource value) =>
        CreateCore<TSource>(observer =>
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return _emptyDisposable;
        });
}
