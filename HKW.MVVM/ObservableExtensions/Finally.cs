namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 在序列完成、出错、订阅失败或订阅被释放时恰好执行一次操作
    /// </summary>
    public static IObservable<TSource> Finally<TSource>(
        this IObservable<TSource> source,
        Action finallyAction
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(finallyAction);
        return PreserveLogger(source, new FinallyObservable<TSource>(source, finallyAction));
    }
}
