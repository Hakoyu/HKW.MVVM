using Microsoft.Extensions.Logging;

namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 以 <see cref="LogLevel.Trace"/> 记录携带日志记录器的序列通知
    /// </summary>
    public static IObservable<TSource> Trace<TSource>(
        this IObservable<TSource> source,
        string? message = null
    ) => Log(source, LogLevel.Trace, message);

    /// <summary>
    /// 以 <see cref="LogLevel.Trace"/> 使用指定日志记录器记录序列通知
    /// </summary>
    public static IObservable<TSource> Trace<TSource>(
        this IObservable<TSource> source,
        ILogger logger,
        string? message = null
    ) => Log(source, logger, LogLevel.Trace, message);

    /// <summary>
    /// 使用可观察序列携带的日志记录器记录每个通知,并原样转发该序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">携带日志记录器的可观察序列,例如由实现 <see cref="IEnableLogger"/> 的对象通过 <c>WhenAnyValue</c> 创建并经内置操作符转换的序列</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <exception cref="InvalidOperationException">序列未携带日志记录器</exception>
    /// <remarks>
    /// 值与成功完成以 <see cref="LogLevel.Debug"/> 级别记录,
    /// 错误以 <see cref="LogLevel.Error"/> 级别记录并保留原始异常
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        string? message = null
    ) => Log(source, GetLogger(source), LogLevel.Debug, LogLevel.Error, message);

    /// <summary>
    /// 使用可观察序列携带的日志记录器,以指定级别记录每个通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">携带日志记录器的可观察序列,例如由实现 <see cref="IEnableLogger"/> 的对象通过 <c>WhenAnyValue</c> 创建并经内置操作符转换的序列</param>
    /// <param name="logLevel">用于记录每个序列通知的级别</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <exception cref="InvalidOperationException">序列未携带日志记录器</exception>
    /// <remarks>
    /// 提供 <paramref name="message"/> 时使用通知优先格式;未提供时保留原格式
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        LogLevel logLevel,
        string? message = null
    ) => Log(source, GetLogger(source), logLevel, logLevel, message);

    /// <summary>
    /// 使用指定的日志记录器记录可观察序列的每个通知,并原样转发
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要记录日志的可观察序列</param>
    /// <param name="logger">接收序列通知的日志记录器</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <remarks>
    /// 值与成功完成以 <see cref="LogLevel.Debug"/> 级别记录,
    /// 错误以 <see cref="LogLevel.Error"/> 级别记录并保留原始异常
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        ILogger logger,
        string? message = null
    )
    {
        return Log(source, logger, LogLevel.Debug, LogLevel.Error, message);
    }

    /// <summary>
    /// 以指定级别记录可观察序列的每个通知,并原样转发
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要记录日志的可观察序列</param>
    /// <param name="logger">接收序列通知的日志记录器</param>
    /// <param name="logLevel">用于记录每个序列通知的级别</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <remarks>
    /// 提供的级别用于值,错误和成功完成.错误会保留原始异常
    /// 提供 <paramref name="message"/> 时使用通知优先格式;未提供时保留原格式
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        ILogger logger,
        LogLevel logLevel,
        string? message = null
    )
    {
        return Log(source, logger, logLevel, logLevel, message);
    }

    private static ILogger GetLogger<TSource>(IObservable<TSource> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source is IObservableWithLogger observableWithLogger
            ? observableWithLogger.GetLogger()
            : throw new InvalidOperationException(
                "The observable does not provide a logger. Pass an ILogger explicitly."
            );
    }

    private static IObservable<TSource> Log<TSource>(
        IObservable<TSource> source,
        ILogger logger,
        LogLevel notificationLogLevel,
        LogLevel errorLogLevel,
        string? message
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(logger);
        var hasMessage = string.IsNullOrWhiteSpace(message) is false;

        return CreateCore(
            source,
            observer =>
                source.Subscribe(
                    value =>
                    {
                        if (hasMessage)
                        {
                            logger.Log(
                                notificationLogLevel,
                                "OnNext({Value}): {Message}",
                                value,
                                message
                            );
                        }
                        else
                        {
                            logger.Log(notificationLogLevel, "Observable: OnNext({Value})", value);
                        }
                        observer.OnNext(value);
                    },
                    error =>
                    {
                        if (hasMessage)
                        {
                            logger.Log(
                                errorLogLevel,
                                error,
                                "OnError({ErrorMessage}): {Message}",
                                error.Message,
                                message
                            );
                        }
                        else
                        {
                            logger.Log(
                                errorLogLevel,
                                error,
                                "Observable: OnError({ErrorMessage})",
                                error.Message
                            );
                        }
                        observer.OnError(error);
                    },
                    () =>
                    {
                        if (hasMessage)
                        {
                            logger.Log(notificationLogLevel, "OnCompleted(): {Message}", message);
                        }
                        else
                        {
                            logger.Log(notificationLogLevel, "Observable: OnCompleted()");
                        }
                        observer.OnCompleted();
                    }
                )
        );
    }
}
