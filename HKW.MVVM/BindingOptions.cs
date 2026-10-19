namespace HKW.MVVM;

/// <summary>
/// 指定双向绑定在订阅后执行的初始同步方向
/// </summary>
public enum BindingInitialSyncDirection
{
    /// <summary>
    /// 不执行初始同步
    /// </summary>
    None,

    /// <summary>
    /// 将源的当前值复制到目标这是默认行为
    /// </summary>
    SourceToTarget,

    /// <summary>
    /// 将目标的当前值复制到源
    /// </summary>
    TargetToSource,
}

/// <summary>
/// 表示一个可显式配置的绑定值, 并区分"未配置"和类型默认值
/// </summary>
public readonly struct BindingValue<T>
{
    /// <summary>
    /// 使用指定值创建配置
    /// </summary>
    public BindingValue(T value)
    {
        Value = value;
        HasValue = true;
    }

    /// <summary>
    /// 获取是否配置了值
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// 获取配置的值
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// 允许将类型安全的值直接用作绑定值配置
    /// </summary>
    public static implicit operator BindingValue<T>(T value) => new(value);
}

/// <summary>
/// 配置单向绑定的调度、去重和替代值行为
/// </summary>
public class BindingOptions<TValue>
{
    /// <summary>
    /// 用于抑制连续重复目标值的比较器
    /// </summary>
    public IEqualityComparer<TValue> Comparer { get; init; } = EqualityComparer<TValue>.Default;

    /// <summary>
    /// 用于执行转换和目标赋值的调度器
    /// </summary>
    public IObservableScheduler Scheduler { get; init; } = ObservableSchedulers.Immediate;

    /// <summary>
    /// 源、转换器或赋值器失败前可写入目标的可选回退值
    /// </summary>
    public BindingValue<TValue> FallbackValue { get; init; }

    /// <summary>源值为 <see langword="null"/> 时写入目标的可选替代值</summary>
    public BindingValue<TValue> TargetNullValue { get; init; }
}

/// <summary>
/// 配置双向绑定的初始同步、调度、去重和路径替代值
/// </summary>
public sealed class TwoWayBindingOptions<TSourceValue, TTargetValue>
{
    /// <summary>
    /// 源属性值比较器
    /// </summary>
    public IEqualityComparer<TSourceValue> SourceComparer { get; init; } =
        EqualityComparer<TSourceValue>.Default;

    /// <summary>
    /// 目标属性值比较器
    /// </summary>
    public IEqualityComparer<TTargetValue> TargetComparer { get; init; } =
        EqualityComparer<TTargetValue>.Default;

    /// <summary>
    /// 用于执行转换和赋值的调度器
    /// </summary>
    public IObservableScheduler Scheduler { get; init; } = ObservableSchedulers.Immediate;

    /// <summary>
    /// 初始同步方向
    /// </summary>
    public BindingInitialSyncDirection InitialSyncDirection { get; init; } =
        BindingInitialSyncDirection.SourceToTarget;

    /// <summary>
    /// 源路径不可用时发出的可选值
    /// </summary>
    public BindingValue<TSourceValue> SourceFallbackValue { get; init; }

    /// <summary>
    /// 目标路径不可用时发出的可选值
    /// </summary>
    public BindingValue<TTargetValue> TargetFallbackValue { get; init; }

    /// <summary>向源写入 <see langword="null"/> 时使用的可选替代值</summary>
    public BindingValue<TSourceValue> SourceNullValue { get; init; }

    /// <summary>向目标写入 <see langword="null"/> 时使用的可选替代值</summary>
    public BindingValue<TTargetValue> TargetNullValue { get; init; }
}

/// <summary>
/// 表示活动绑定及其非抛出式错误通道
/// </summary>
public sealed class BindingResult : IDisposable
{
    private readonly SingleAssignmentDisposable _subscription = new();
    private readonly ExceptionSubject _exceptions = new(replayLatest: true);
    private int _stopped;
    private int _disposed;

    /// <summary>
    /// 接收最多一个终止绑定的异常; 释放结果时完成
    /// </summary>
    public IObservable<Exception> ThrownExceptions => _exceptions;

    internal bool IsStopped => Volatile.Read(ref _stopped) != 0;

    internal void SetSubscription(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        _subscription.Disposable = subscription;
    }

    internal void Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _exceptions.OnNext(exception);
        _subscription.Dispose();
    }

    /// <summary>
    /// 停止绑定, 并完成 <see cref="ThrownExceptions"/>
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _stopped, 1);
        _subscription.Dispose();
        _exceptions.Dispose();
    }
}

/// <summary>
/// 配置 <see cref="ObservableAsPropertyHelper{T}"/>
/// </summary>
public sealed class ObservableAsPropertyOptions<T>
{
    /// <summary>
    /// 通知调度器
    /// </summary>
    public IObservableScheduler Scheduler { get; init; } = ObservableSchedulers.Immediate;

    /// <summary>
    /// 用于抑制连续重复值的比较器
    /// </summary>
    public IEqualityComparer<T> Comparer { get; init; } = EqualityComparer<T>.Default;

    /// <summary>
    /// 是否推迟到首次读取 Value 时订阅
    /// </summary>
    public bool DeferSubscription { get; init; }
}
