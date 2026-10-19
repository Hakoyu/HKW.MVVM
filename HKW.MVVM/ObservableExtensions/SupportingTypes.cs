using Microsoft.Extensions.Logging;

namespace HKW.MVVM;

/// <summary>
/// 指定可观察操作应在何处调度
/// </summary>
public enum ObservableSchedulers
{
    /// <summary>
    /// 使用操作符捕获的 <see cref="SynchronizationContext.Current"/>
    /// </summary>
    Current,

    /// <summary>
    /// 使用 .NET 线程池 <see cref="System.Threading.ThreadPool"/>
    /// </summary>
    ThreadPool,
}

internal interface IObservableWithLogger
{
    ILogger GetLogger();
}

internal sealed class ObservableWithLogger<T> : IObservable<T>, IObservableWithLogger
{
    private readonly IObservable<T> _source;
    private readonly Lazy<ILogger> _logger;

    public ObservableWithLogger(IObservable<T> source, Func<ILogger> loggerFactory)
    {
        _source = source;
        _logger = new Lazy<ILogger>(loggerFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ILogger GetLogger() => _logger.Value;

    public IDisposable Subscribe(IObserver<T> observer) => _source.Subscribe(observer);
}
