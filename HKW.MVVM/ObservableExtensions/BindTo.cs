using System.Linq.Expressions;

namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 使用调用方提供的赋值操作绑定可观察序列产生的值
    /// </summary>
    /// <typeparam name="TValue">源产生的值类型</typeparam>
    /// <typeparam name="TTarget">目标对象的类型</typeparam>
    /// <param name="source">提供值的可观察序列</param>
    /// <param name="target">传递给 <paramref name="assignment"/> 的目标对象</param>
    /// <param name="assignment">接收每个源值和目标对象的操作</param>
    /// <returns>可停止绑定的可释放对象</returns>
    public static IDisposable BindTo<TValue, TTarget>(
        this IObservable<TValue> source,
        TTarget target,
        Action<TValue, TTarget> assignment
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(assignment);
        return source.Subscribe(value => assignment(value, target));
    }

    /// <summary>
    /// 将可观察序列产生的值绑定到可写的目标属性
    /// </summary>
    /// <typeparam name="TTarget">目标对象的类型</typeparam>
    /// <typeparam name="TValue">源和目标的值的类型</typeparam>
    /// <param name="source">提供值的可观察序列</param>
    /// <param name="target">接收值的属性所属对象</param>
    /// <param name="targetProperty">以 <paramref name="target"/> 为根的,可写的属性路径</param>
    /// <returns>可停止绑定的可释放对象</returns>
    public static IDisposable BindTo<TTarget, TValue>(
        this IObservable<TValue> source,
        TTarget target,
        Expression<Func<TTarget, TValue>> targetProperty
    )
        where TTarget : class => BindTo(source, target, targetProperty, static value => value);

    /// <summary>
    /// 将可观察序列产生的,经过转换的值绑定到可写的目标属性
    /// </summary>
    /// <typeparam name="TSourceValue">源产生的值类型</typeparam>
    /// <typeparam name="TTarget">目标对象的类型</typeparam>
    /// <typeparam name="TTargetValue">目标属性的值类型</typeparam>
    /// <param name="source">提供值的可观察序列</param>
    /// <param name="target">接收值的属性所属对象</param>
    /// <param name="targetProperty">以 <paramref name="target"/> 为根的,可写的属性路径</param>
    /// <param name="converter">将源值转换为目标值的函数</param>
    /// <returns>可停止绑定的可释放对象</returns>
    public static IDisposable BindTo<TSourceValue, TTarget, TTargetValue>(
        this IObservable<TSourceValue> source,
        TTarget target,
        Expression<Func<TTarget, TTargetValue>> targetProperty,
        Func<TSourceValue, TTargetValue> converter
    )
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(converter);

        var setter = PropertySetter.Create(targetProperty);
        return source.Subscribe(value => setter(target, converter(value)));
    }
}
