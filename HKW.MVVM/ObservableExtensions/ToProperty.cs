using System.Linq.Expressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将可观察序列转换为针对由表达式选择的只读拥有者属性的,带调度的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="property">选择 <paramref name="owner"/> 上直接属性的表达式</param>
    /// <param name="scheduler">用于派发值变更和通知的调度器</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 会在创建辅助对象时捕获
    /// <see cref="SynchronizationContext.Current"/>,不存在上下文时回退到线程池
    /// <see cref="ObservableSchedulers.ThreadPool"/> 则始终将变更排队到线程池
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        ObservableSchedulers scheduler,
        bool deferSubscription = false
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        return ToProperty(
            source,
            owner,
            property.GetPropertyName(),
            default!,
            scheduler,
            deferSubscription
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对由表达式选择的只读拥有者属性的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="property">选择 <paramref name="owner"/> 上直接属性的表达式</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <param name="synchronizationContext">用于派发值变更和通知的可选上下文</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// 属性名称从表达式中提取.拥有者通知优先使用 <see cref="IPropertyChangeNotifier"/>,
    /// 否则通过 AOT 兼容的静态访问器调用 <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> 的受保护通知方法
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        var propertyName = property.GetPropertyName();
        return ToProperty(
            source,
            owner,
            propertyName,
            default!,
            deferSubscription,
            synchronizationContext
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对按名称标识的只读拥有者属性的,带调度的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="propertyName">变更通知中使用的拥有者属性名称</param>
    /// <param name="scheduler">用于派发值变更和通知的调度器</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 会在创建辅助对象时捕获
    /// <see cref="SynchronizationContext.Current"/>,不存在上下文时回退到线程池
    /// <see cref="ObservableSchedulers.ThreadPool"/> 则始终将变更排队到线程池
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        ObservableSchedulers scheduler,
        bool deferSubscription = false
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new ObservableAsPropertyHelper<TValue>(
            source,
            owner,
            propertyName,
            default!,
            deferSubscription,
            scheduler
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对按名称标识的只读拥有者属性的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="propertyName">变更通知中使用的拥有者属性名称</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <param name="synchronizationContext">用于派发值变更和通知的可选上下文</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// 拥有者通知优先使用 <see cref="IPropertyChangeNotifier"/>,
    /// 否则通过 AOT 兼容的静态访问器调用 <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> 的受保护通知方法
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new ObservableAsPropertyHelper<TValue>(
            source,
            owner,
            propertyName,
            default!,
            deferSubscription,
            synchronizationContext
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对由表达式选择的只读拥有者属性的,带调度的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="property">选择 <paramref name="owner"/> 上直接属性的表达式</param>
    /// <param name="scheduler">用于派发值变更和通知的调度器</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 会在创建辅助对象时捕获
    /// <see cref="SynchronizationContext.Current"/>,不存在上下文时回退到线程池
    /// <see cref="ObservableSchedulers.ThreadPool"/> 则始终将变更排队到线程池
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        TValue initialValue,
        ObservableSchedulers scheduler,
        bool deferSubscription = false
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        return ToProperty(
            source,
            owner,
            property.GetPropertyName(),
            initialValue,
            scheduler,
            deferSubscription
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对由表达式选择的只读拥有者属性的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="property">选择 <paramref name="owner"/> 上直接属性的表达式</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <param name="synchronizationContext">用于派发值变更和通知的可选上下文</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// 属性名称从表达式中提取.拥有者通知优先使用<see cref="IPropertyChangeNotifier"/>,
    /// 否则通过 AOT 兼容的静态访问器调用 <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> 的受保护通知方法
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        TValue initialValue,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        var propertyName = property.GetPropertyName();
        return ToProperty(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            synchronizationContext
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对按名称标识的只读拥有者属性的,带调度的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="propertyName">变更通知中使用的拥有者属性名称</param>
    /// <param name="scheduler">用于派发值变更和通知的调度器</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 会在创建辅助对象时捕获
    /// <see cref="SynchronizationContext.Current"/>,不存在上下文时回退到线程池
    /// <see cref="ObservableSchedulers.ThreadPool"/> 则始终将变更排队到线程池
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        TValue initialValue,
        ObservableSchedulers scheduler,
        bool deferSubscription = false
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new ObservableAsPropertyHelper<TValue>(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            scheduler
        );
    }

    /// <summary>
    /// 将可观察序列转换为针对按名称标识的只读拥有者属性的辅助对象
    /// </summary>
    /// <typeparam name="TOwner">CommunityToolkit 可观察对象拥有者的类型</typeparam>
    /// <typeparam name="TValue">属性的值类型</typeparam>
    /// <param name="source">提供属性值的序列</param>
    /// <param name="owner">拥有该只读属性的对象</param>
    /// <param name="propertyName">变更通知中使用的拥有者属性名称</param>
    /// <param name="initialValue">在源产生首个去重值之前所公开的值</param>
    /// <param name="deferSubscription">是否将源订阅推迟到首次读取该辅助对象的值时</param>
    /// <param name="synchronizationContext">用于派发值变更和通知的可选上下文</param>
    /// <returns>存储最新值并通知拥有者的可观察属性辅助对象</returns>
    /// <remarks>
    /// 拥有者通知优先使用 <see cref="IPropertyChangeNotifier"/>,
    /// 否则通过 AOT 兼容的静态访问器调用 <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> 的受保护通知方法
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        TValue initialValue,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new ObservableAsPropertyHelper<TValue>(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            synchronizationContext
        );
    }
}
