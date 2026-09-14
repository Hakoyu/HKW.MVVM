using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace HKW.MVVM;

#pragma warning disable S2436
/// <summary>
/// 属性观察结果,包含对象,属性名称和当前值.
/// </summary>
public readonly record struct PropertyObservation<TSender, TValue>(
    TSender Sender,
    string PropertyName,
    TValue Value
);

/// <summary>
/// 将 <see cref="INotifyPropertyChanged"/> 属性转换为冷可观察序列.
/// </summary>
public static class WhenAnyExtensions
{
    /// <summary>
    /// 观察一条属性路径,在订阅时发出其当前值,随后发出去重后的变更值.
    /// 当中间对象发生变更时会重新绑定嵌套路径;路径不可用期间将被抑制,直到其恢复.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="TValue">最终属性的值类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property">以 <paramref name="source"/> 为根的属性路径,例如 <c>x =&gt; x.Address.City</c>.</param>
    /// <returns>由最终属性值构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TValue> WhenAnyValue<TSource, TValue>(
        this TSource source,
        Expression<Func<TSource, TValue>> property
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(property);

        // 直接属性只需 INotifyPropertyChanged.
        // 让嵌套路径继续则重新绑定实现,但对占绝大多数的单属性情形避免反射以及事件处理程序的重建.
        if (PropertyPath.TryGetDirectProperty(property, out var directProperty))
        {
            return new DirectPropertyObservable<TSource, TValue>(
                source,
                DirectPropertyGetterCache<TSource, TValue>.Getters.Get(directProperty),
                directProperty.Name
            );
        }

        return new PropertyPathObservable<TSource, TValue>(source, property);
    }

    /// <summary>
    /// 观察两个属性,并在任一最终值变更时以元组形式发出二者的最新值.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <returns>由包含最新属性值的元组构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<(T1, T2)> WhenAnyValue<TSource, T1, T2>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            static (value1, value2) => (value1, value2)
        );

    /// <summary>
    /// 观察两个属性,并在任一最终值变更时对二者的最新值进行投影.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="TResult">投影结果的类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="selector">用于组合各项最新属性值的函数.</param>
    /// <returns>由投影结果构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Func<T1, T2, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(source.WhenAnyValue(property1), source.WhenAnyValue(property2), selector);
    }

    /// <summary>
    /// 观察三个属性,并在任一最终值变更时以元组形式发出它们的最新值.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="T3">第三个属性的值类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="property3">第三条属性路径.</param>
    /// <returns>由包含最新属性值的元组构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<(T1, T2, T3)> WhenAnyValue<TSource, T1, T2, T3>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            static (value1, value2, value3) => (value1, value2, value3)
        );

    /// <summary>
    /// 观察三个属性,并在任一最终值变更时对它们的最新值进行投影.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="T3">第三个属性的值类型.</typeparam>
    /// <typeparam name="TResult">投影结果的类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="property3">第三条属性路径.</param>
    /// <param name="selector">用于组合各项最新属性值的函数.</param>
    /// <returns>由投影结果构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, T3, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Func<T1, T2, T3, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            selector
        );
    }

    /// <summary>
    /// 观察四个属性,并在任一最终值变更时以元组形式发出它们的最新值.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="T3">第三个属性的值类型.</typeparam>
    /// <typeparam name="T4">第四个属性的值类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="property3">第三条属性路径.</param>
    /// <param name="property4">第四条属性路径.</param>
    /// <returns>由包含最新属性值的元组构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<(T1, T2, T3, T4)> WhenAnyValue<TSource, T1, T2, T3, T4>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Expression<Func<TSource, T4>> property4
    )
        where TSource : class, INotifyPropertyChanged =>
        Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            source.WhenAnyValue(property4),
            static (value1, value2, value3, value4) => (value1, value2, value3, value4)
        );

    /// <summary>
    /// 观察四个属性,并在任一最终值变更时对它们的最新值进行投影.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="T3">第三个属性的值类型.</typeparam>
    /// <typeparam name="T4">第四个属性的值类型.</typeparam>
    /// <typeparam name="TResult">投影结果的类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="property3">第三条属性路径.</param>
    /// <param name="property4">第四条属性路径.</param>
    /// <param name="selector">用于组合各项最新属性值的函数.</param>
    /// <returns>由投影结果构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TResult> WhenAnyValue<TSource, T1, T2, T3, T4, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Expression<Func<TSource, T4>> property4,
        Func<T1, T2, T3, T4, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            source.WhenAnyValue(property4),
            selector
        );
    }

    /// <summary>
    /// 观察一条属性路径,并投影出包含发送方,最终属性名称和值的观察结果.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="TValue">最终属性的值类型.</typeparam>
    /// <typeparam name="TResult">投影结果的类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property">要观察的属性路径.</param>
    /// <param name="selector">用于投影每个属性观察结果的函数.</param>
    /// <returns>由投影后的观察结果构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TResult> WhenAny<TSource, TValue, TResult>(
        this TSource source,
        Expression<Func<TSource, TValue>> property,
        Func<PropertyObservation<TSource, TValue>, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        var propertyName = property.GetPropertyName();
        return ObservableExtensions.Select(
            source.WhenAnyValue(property),
            value => selector(new PropertyObservation<TSource, TValue>(source, propertyName, value))
        );
    }

    /// <summary>
    /// 观察两个属性,并在任一最终值变更时对它们的观察结果进行投影.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="TResult">投影结果的类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="selector">用于组合两个属性观察结果的函数.</param>
    /// <returns>由投影后的观察结果构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TResult> WhenAny<TSource, T1, T2, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Func<PropertyObservation<TSource, T1>, PropertyObservation<TSource, T2>, TResult> selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        var propertyName1 = property1.GetPropertyName();
        var propertyName2 = property2.GetPropertyName();
        return Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            (value1, value2) =>
                selector(
                    new PropertyObservation<TSource, T1>(source, propertyName1, value1),
                    new PropertyObservation<TSource, T2>(source, propertyName2, value2)
                )
        );
    }

    /// <summary>
    /// 观察三个属性,并在任一最终值变更时对它们的观察结果进行投影.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="T3">第三个属性的值类型.</typeparam>
    /// <typeparam name="TResult">投影结果的类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="property3">第三条属性路径.</param>
    /// <param name="selector">用于组合三个属性观察结果的函数.</param>
    /// <returns>由投影后的观察结果构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TResult> WhenAny<TSource, T1, T2, T3, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Func<
            PropertyObservation<TSource, T1>,
            PropertyObservation<TSource, T2>,
            PropertyObservation<TSource, T3>,
            TResult
        > selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        var propertyName1 = property1.GetPropertyName();
        var propertyName2 = property2.GetPropertyName();
        var propertyName3 = property3.GetPropertyName();
        return Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            (value1, value2, value3) =>
                selector(
                    new PropertyObservation<TSource, T1>(source, propertyName1, value1),
                    new PropertyObservation<TSource, T2>(source, propertyName2, value2),
                    new PropertyObservation<TSource, T3>(source, propertyName3, value3)
                )
        );
    }

    /// <summary>
    /// 观察四个属性,并在任一最终值变更时对它们的观察结果进行投影.
    /// </summary>
    /// <typeparam name="TSource">发出通知的源类型.</typeparam>
    /// <typeparam name="T1">第一个属性的值类型.</typeparam>
    /// <typeparam name="T2">第二个属性的值类型.</typeparam>
    /// <typeparam name="T3">第三个属性的值类型.</typeparam>
    /// <typeparam name="T4">第四个属性的值类型.</typeparam>
    /// <typeparam name="TResult">投影结果的类型.</typeparam>
    /// <param name="source">要观察其属性的源对象.</param>
    /// <param name="property1">第一条属性路径.</param>
    /// <param name="property2">第二条属性路径.</param>
    /// <param name="property3">第三条属性路径.</param>
    /// <param name="property4">第四条属性路径.</param>
    /// <param name="selector">用于组合四个属性观察结果的函数.</param>
    /// <returns>由投影后的观察结果构成的冷可观察序列.</returns>
    /// <remarks>
    /// <b>反射: 仅嵌套属性.</b>
    /// </remarks>
    public static IObservable<TResult> WhenAny<TSource, T1, T2, T3, T4, TResult>(
        this TSource source,
        Expression<Func<TSource, T1>> property1,
        Expression<Func<TSource, T2>> property2,
        Expression<Func<TSource, T3>> property3,
        Expression<Func<TSource, T4>> property4,
        Func<
            PropertyObservation<TSource, T1>,
            PropertyObservation<TSource, T2>,
            PropertyObservation<TSource, T3>,
            PropertyObservation<TSource, T4>,
            TResult
        > selector
    )
        where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(selector);
        var propertyName1 = property1.GetPropertyName();
        var propertyName2 = property2.GetPropertyName();
        var propertyName3 = property3.GetPropertyName();
        var propertyName4 = property4.GetPropertyName();
        return Combine(
            source.WhenAnyValue(property1),
            source.WhenAnyValue(property2),
            source.WhenAnyValue(property3),
            source.WhenAnyValue(property4),
            (value1, value2, value3, value4) =>
                selector(
                    new PropertyObservation<TSource, T1>(source, propertyName1, value1),
                    new PropertyObservation<TSource, T2>(source, propertyName2, value2),
                    new PropertyObservation<TSource, T3>(source, propertyName3, value3),
                    new PropertyObservation<TSource, T4>(source, propertyName4, value4)
                )
        );
    }

    private static IObservable<TResult> Combine<T1, T2, TResult>(
        IObservable<T1> source1,
        IObservable<T2> source2,
        Func<T1, T2, TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
        {
            var gate = new Lock();
            var value1 = default(T1)!;
            var value2 = default(T2)!;
            var hasValue1 = false;
            var hasValue2 = false;
            var stopped = false;
            var completedSources = 0;
            var subscriptions = new MultipleDisposable();

            void Publish()
            {
                T1 current1;
                T2 current2;
                lock (gate)
                {
                    if (stopped || hasValue1 is false || hasValue2 is false)
                    {
                        return;
                    }

                    current1 = value1;
                    current2 = value2;
                }

                try
                {
                    observer.OnNext(selector(current1, current2));
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            }

            void Fail(Exception error)
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                }

                observer.OnError(error);
                subscriptions.Dispose();
            }

            void Complete()
            {
                var shouldComplete = false;
                lock (gate)
                {
                    if (stopped)
                        return;
                    completedSources++;
                    shouldComplete = completedSources == 2;
                    if (shouldComplete)
                        stopped = true;
                }
                if (shouldComplete)
                {
                    observer.OnCompleted();
                    subscriptions.Dispose();
                }
            }
            subscriptions.Add(
                source1.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value1 = value;
                            hasValue1 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source2.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value2 = value;
                            hasValue2 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );

            return subscriptions;
        });

    private static IObservable<TResult> Combine<T1, T2, T3, TResult>(
        IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        Func<T1, T2, T3, TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
        {
            var gate = new Lock();
            var value1 = default(T1)!;
            var value2 = default(T2)!;
            var value3 = default(T3)!;
            var hasValue1 = false;
            var hasValue2 = false;
            var hasValue3 = false;
            var stopped = false;
            var completedSources = 0;
            var subscriptions = new MultipleDisposable();

            void Publish()
            {
                T1 current1;
                T2 current2;
                T3 current3;
                lock (gate)
                {
                    if (stopped || hasValue1 is false || hasValue2 is false || hasValue3 is false)
                    {
                        return;
                    }

                    current1 = value1;
                    current2 = value2;
                    current3 = value3;
                }

                try
                {
                    observer.OnNext(selector(current1, current2, current3));
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            }

            void Fail(Exception error)
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                }

                observer.OnError(error);
                subscriptions.Dispose();
            }

            void Complete()
            {
                var shouldComplete = false;
                lock (gate)
                {
                    if (stopped)
                        return;
                    completedSources++;
                    shouldComplete = completedSources == 3;
                    if (shouldComplete)
                        stopped = true;
                }
                if (shouldComplete)
                {
                    observer.OnCompleted();
                    subscriptions.Dispose();
                }
            }
            subscriptions.Add(
                source1.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value1 = value;
                            hasValue1 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source2.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value2 = value;
                            hasValue2 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source3.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value3 = value;
                            hasValue3 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );

            return subscriptions;
        });

    private static IObservable<TResult> Combine<T1, T2, T3, T4, TResult>(
        IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        IObservable<T4> source4,
        Func<T1, T2, T3, T4, TResult> selector
    ) =>
        new AnonymousObservable<TResult>(observer =>
        {
            var gate = new Lock();
            var value1 = default(T1)!;
            var value2 = default(T2)!;
            var value3 = default(T3)!;
            var value4 = default(T4)!;
            var hasValue1 = false;
            var hasValue2 = false;
            var hasValue3 = false;
            var hasValue4 = false;
            var stopped = false;
            var completedSources = 0;
            var subscriptions = new MultipleDisposable();

            void Publish()
            {
                T1 current1;
                T2 current2;
                T3 current3;
                T4 current4;
                lock (gate)
                {
                    if (
                        stopped
                        || hasValue1 is false
                        || hasValue2 is false
                        || hasValue3 is false
                        || hasValue4 is false
                    )
                    {
                        return;
                    }

                    current1 = value1;
                    current2 = value2;
                    current3 = value3;
                    current4 = value4;
                }

                try
                {
                    observer.OnNext(selector(current1, current2, current3, current4));
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            }

            void Fail(Exception error)
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                }

                observer.OnError(error);
                subscriptions.Dispose();
            }

            void Complete()
            {
                var shouldComplete = false;
                lock (gate)
                {
                    if (stopped)
                        return;
                    completedSources++;
                    shouldComplete = completedSources == 4;
                    if (shouldComplete)
                        stopped = true;
                }
                if (shouldComplete)
                {
                    observer.OnCompleted();
                    subscriptions.Dispose();
                }
            }
            subscriptions.Add(
                source1.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value1 = value;
                            hasValue1 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source2.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value2 = value;
                            hasValue2 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source3.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value3 = value;
                            hasValue3 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );
            subscriptions.Add(
                source4.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            value4 = value;
                            hasValue4 = true;
                        }

                        Publish();
                    },
                    Fail,
                    Complete
                )
            );

            return subscriptions;
        });

    private static class DirectPropertyGetterCache<TSource, TValue>
    {
        public static readonly MemoizingLRUCache<PropertyInfo, Func<TSource, TValue>> Getters = new(
            Compile,
            64
        );

        private static Func<TSource, TValue> Compile(PropertyInfo property)
        {
            var source = Expression.Parameter(typeof(TSource), "source");
            Expression body = Expression.Property(source, property);
            if (body.Type != typeof(TValue))
            {
                body = Expression.Convert(body, typeof(TValue));
            }

            return Expression.Lambda<Func<TSource, TValue>>(body, source).Compile();
        }
    }

    private static class PropertyPathLeafGetterCache<TValue>
    {
        public static readonly MemoizingLRUCache<PropertyInfo, Func<object, TValue>> Getters = new(
            Compile,
            64
        );

        private static Func<object, TValue> Compile(PropertyInfo property)
        {
            var owner = Expression.Parameter(typeof(object), "owner");
            Expression body = Expression.Property(
                Expression.Convert(owner, property.DeclaringType!),
                property
            );
            if (body.Type != typeof(TValue))
            {
                body = Expression.Convert(body, typeof(TValue));
            }

            return Expression.Lambda<Func<object, TValue>>(body, owner).Compile();
        }
    }

    private static class PropertyPathGetterCache
    {
        public static readonly MemoizingLRUCache<PropertyInfo, Func<object, object?>> Getters = new(
            Compile,
            64
        );

        private static Func<object, object?> Compile(PropertyInfo property)
        {
            var owner = Expression.Parameter(typeof(object), "owner");
            Expression body = Expression.Property(
                Expression.Convert(owner, property.DeclaringType!),
                property
            );
            if (body.Type.IsValueType)
            {
                body = Expression.Convert(body, typeof(object));
            }

            return Expression.Lambda<Func<object, object?>>(body, owner).Compile();
        }
    }

    private sealed class DirectPropertyObservable<TSource, TValue>(
        TSource source,
        Func<TSource, TValue> getter,
        string propertyName
    ) : IObservable<TValue>
        where TSource : class, INotifyPropertyChanged
    {
        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            return new DirectPropertySubscription<TSource, TValue>(
                source,
                getter,
                propertyName,
                observer
            );
        }
    }

    private sealed class DirectPropertySubscription<TSource, TValue> : IDisposable
        where TSource : class, INotifyPropertyChanged
    {
        private readonly TSource _source;
        private readonly Func<TSource, TValue> _getter;
        private readonly string _propertyName;
        private readonly IObserver<TValue> _observer;
        private bool _hasValue;
        private TValue? _lastValue;
        private int _disposed;

        public DirectPropertySubscription(
            TSource source,
            Func<TSource, TValue> getter,
            string propertyName,
            IObserver<TValue> observer
        )
        {
            _source = source;
            _getter = getter;
            _propertyName = propertyName;
            _observer = observer;
            _source.PropertyChanged += OnPropertyChanged;
            PublishCurrentValue();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _source.PropertyChanged -= OnPropertyChanged;
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == _propertyName
            )
            {
                PublishCurrentValue();
            }
        }

        private void PublishCurrentValue()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            TValue value;
            try
            {
                value = _getter(_source);
            }
            catch (Exception exception)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _source.PropertyChanged -= OnPropertyChanged;
                    _observer.OnError(exception);
                }

                return;
            }

            if (_hasValue && EqualityComparer<TValue>.Default.Equals(_lastValue!, value))
            {
                return;
            }

            _hasValue = true;
            _lastValue = value;
            _observer.OnNext(value);
        }
    }

    private sealed class PropertyPathObservable<TSource, TValue> : IObservable<TValue>
        where TSource : class, INotifyPropertyChanged
    {
        private readonly TSource _source;
        private readonly PropertyInfo[] _path;
        private readonly Func<object, object?>[] _pathGetters;
        private readonly Func<object, TValue> _leafGetter;

        public PropertyPathObservable(TSource source, Expression<Func<TSource, TValue>> expression)
        {
            _source = source;
            _path = PropertyPath.Parse(expression);
            _pathGetters = new Func<object, object?>[_path.Length - 1];
            for (var index = 0; index < _pathGetters.Length; index++)
            {
                _pathGetters[index] = PropertyPathGetterCache.Getters.Get(_path[index]);
            }
            _leafGetter = PropertyPathLeafGetterCache<TValue>.Getters.Get(_path[^1]);
        }

        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            return new PropertyPathSubscription<TSource, TValue>(
                _source,
                _path,
                _pathGetters,
                _leafGetter,
                observer
            );
        }
    }

    private sealed class PropertyPathSubscription<TSource, TValue> : IDisposable
        where TSource : class, INotifyPropertyChanged
    {
        private readonly Lock _gate = new();
        private readonly TSource _source;
        private readonly PropertyInfo[] _path;
        private readonly Func<object, object?>[] _pathGetters;
        private readonly Func<object, TValue> _leafGetter;
        private readonly IObserver<TValue> _observer;
        private readonly object?[] _owners;
        private readonly INotifyPropertyChanged?[] _notifiers;
        private readonly PropertyChangedEventHandler[] _handlers;
        private readonly int _pathLength;
        private readonly int _lastPathIndex;
        private readonly IEqualityComparer<TValue> _valueComparer =
            EqualityComparer<TValue>.Default;
        private bool _hasValue;
        private TValue? _lastValue;
        private bool _disposed;

        public PropertyPathSubscription(
            TSource source,
            PropertyInfo[] path,
            Func<object, object?>[] pathGetters,
            Func<object, TValue> leafGetter,
            IObserver<TValue> observer
        )
        {
            _source = source;
            _path = path;
            _pathGetters = pathGetters;
            _pathLength = path.Length;
            _lastPathIndex = _pathLength - 1;
            _leafGetter = leafGetter;
            _observer = observer;
            _owners = new object?[_pathLength];
            _notifiers = new INotifyPropertyChanged?[_pathLength];
            _handlers = new PropertyChangedEventHandler[_pathLength];
            for (var index = 0; index < _pathLength; index++)
            {
                var capturedIndex = index;
                _handlers[index] = (sender, eventArgs) =>
                    OnPropertyChanged(capturedIndex, eventArgs);
            }

            RebuildAndPublish(0);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                DetachHandlersFrom(0);
            }
        }

        private void OnPropertyChanged(int pathIndex, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == _path[pathIndex].Name
            )
            {
                RebuildAndPublish(pathIndex);
            }
        }

        private void RebuildAndPublish(int changedPathIndex)
        {
            TValue? value = default;
            Exception? error = null;
            var publish = false;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    object? owner;
                    if (changedPathIndex == 0 && _owners[0] is null)
                    {
                        owner = _source;
                    }
                    else
                    {
                        owner = _owners[changedPathIndex];
                    }

                    DetachHandlersFrom(changedPathIndex + 1);
                    for (var index = changedPathIndex; index < _pathLength; index++)
                    {
                        if (owner is null)
                        {
                            return;
                        }

                        if (index > changedPathIndex || _owners[index] is null)
                        {
                            _owners[index] = owner;
                            if (owner is INotifyPropertyChanged notifier)
                            {
                                notifier.PropertyChanged += _handlers[index];
                                _notifiers[index] = notifier;
                            }
                        }

                        if (index == _lastPathIndex)
                        {
                            value = _leafGetter(owner);
                        }
                        else
                        {
                            owner = _pathGetters[index](owner);
                        }
                    }

                    if (_hasValue is false || _valueComparer.Equals(_lastValue!, value!) is false)
                    {
                        _hasValue = true;
                        _lastValue = value;
                        publish = true;
                    }
                }
                catch (TargetInvocationException exception)
                {
                    error = exception.InnerException ?? exception;
                    _disposed = true;
                    DetachHandlersFrom(0);
                }
                catch (Exception exception)
                {
                    error = exception;
                    _disposed = true;
                    DetachHandlersFrom(0);
                }
            }

            if (error is not null)
            {
                _observer.OnError(error);
            }
            else if (publish)
            {
                _observer.OnNext(value!);
            }
        }

        private void DetachHandlersFrom(int startIndex)
        {
            for (var index = startIndex; index < _pathLength; index++)
            {
                var notifier = _notifiers[index];
                if (notifier is not null)
                {
                    notifier.PropertyChanged -= _handlers[index];
                    _notifiers[index] = null;
                }

                _owners[index] = null;
            }
        }
    }

    private static class PropertyPath
    {
        public static bool TryGetDirectProperty(
            LambdaExpression expression,
            out PropertyInfo property
        )
        {
            Expression body = expression.Body;
            if (
                body is UnaryExpression
                {
                    NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
                } unary
            )
            {
                body = unary.Operand;
            }

            if (
                body
                    is MemberExpression
                    {
                        Member: PropertyInfo { GetMethod: not null } directProperty,
                        Expression: ParameterExpression parameter,
                    }
                && parameter == expression.Parameters[0]
            )
            {
                property = directProperty;
                return true;
            }

            property = null!;
            return false;
        }

        public static PropertyInfo[] Parse<TSource, TValue>(
            Expression<Func<TSource, TValue>> expression
        )
        {
            ArgumentNullException.ThrowIfNull(expression);
            Expression current = expression.Body;
            if (
                current is UnaryExpression
                {
                    NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
                } conversion
            )
            {
                current = conversion.Operand;
            }

            var path = new Stack<PropertyInfo>();
            while (current is MemberExpression memberExpression)
            {
                if (
                    memberExpression.Member is not PropertyInfo property
                    || property.GetMethod is null
                )
                {
                    throw new ArgumentException(
                        "The expression must contain readable properties only.",
                        nameof(expression)
                    );
                }

                path.Push(property);
                current = memberExpression.Expression!;
            }

            if (current != expression.Parameters[0] || path.Count == 0)
            {
                throw new ArgumentException(
                    "The expression must be a property path rooted at its parameter, for example x => x.Customer.Name.",
                    nameof(expression)
                );
            }

            return path.ToArray();
        }
    }
}
#pragma warning restore S2436
