using System.Runtime.CompilerServices;
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

/// <summary>
/// 指定可观察序列日志通知的类型
/// </summary>
public enum ObservableLogAction
{
    /// <summary>
    /// 序列发出了一个值
    /// </summary>
    OnNext,

    /// <summary>
    /// 序列因错误而终止
    /// </summary>
    OnError,

    /// <summary>
    /// 序列成功完成
    /// </summary>
    OnCompleted,
}

/// <summary>
/// 表示传递给可观察序列日志消息工厂的通知
/// </summary>
/// <typeparam name="T">序列值类型</typeparam>
/// <param name="Action">通知类型</param>
/// <param name="Value">当 <paramref name="Action"/> 为 <see cref="ObservableLogAction.OnNext"/> 时的序列值;其他通知为默认值</param>
/// <param name="Exception">当 <paramref name="Action"/> 为 <see cref="ObservableLogAction.OnError"/> 时的源异常;其他通知为 <see langword="null"/></param>
public readonly record struct ObservableLogNotification<T>(
    ObservableLogAction Action,
    T? Value,
    Exception? Exception
);

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

/// <summary>
/// 常用可观察操作符
/// </summary>
public static class ObservableExtensions
{
    private static readonly IDisposable _emptyDisposable = new ActionDisposable(static () => { });

    /// <summary>
    /// 使用可观察序列携带的日志记录器记录每个通知,并原样转发该序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">携带日志记录器的可观察序列,例如由实现 <see cref="IEnableLogger"/> 的对象通过 <c>WhenAnyValue</c> 创建并经内置操作符转换的序列</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <param name="sourceExpression">由编译器捕获的源表达式;调用方通常不应显式提供</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <exception cref="InvalidOperationException">序列未携带日志记录器</exception>
    /// <remarks>
    /// 值与成功完成以 <see cref="LogLevel.Debug"/> 级别记录,并以源表达式中的
    /// 点式操作符调用链作为消息前缀.若通过局部变量调用,仅能显示变量名
    /// 提供 <paramref name="message"/> 时使用 <c>OnNext(value): message</c> 等通知优先格式;
    /// 未提供时保留 <c>Observable: OnNext(value)</c> 格式
    /// 错误以 <see cref="LogLevel.Error"/> 级别记录并保留原始异常,且始终显示调用链
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        string? message = null,
        [CallerArgumentExpression(nameof(source))] string? sourceExpression = null
    ) => Log(source, GetLogger(source), LogLevel.Debug, LogLevel.Error, message, sourceExpression);

    /// <summary>
    /// 使用可观察序列携带的日志记录器,以指定级别记录每个通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">携带日志记录器的可观察序列,例如由实现 <see cref="IEnableLogger"/> 的对象通过 <c>WhenAnyValue</c> 创建并经内置操作符转换的序列</param>
    /// <param name="logLevel">用于记录每个序列通知的级别</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <param name="sourceExpression">由编译器捕获的源表达式;调用方通常不应显式提供</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <exception cref="InvalidOperationException">序列未携带日志记录器</exception>
    /// <remarks>
    /// Trace,Debug,Error 和 Critical 级别的值与完成通知添加调用链前缀;
    /// 错误通知无论级别为何都添加调用链前缀
    /// 提供 <paramref name="message"/> 时使用通知优先格式;未提供时保留原格式
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        LogLevel logLevel,
        string? message = null,
        [CallerArgumentExpression(nameof(source))] string? sourceExpression = null
    ) => Log(source, GetLogger(source), logLevel, logLevel, message, sourceExpression);

    /// <summary>
    /// 使用可观察序列携带的日志记录器记录通知,并使用工厂生成每个值的完整日志消息
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">携带日志记录器的可观察序列</param>
    /// <param name="messageFactory">根据每个源值生成完整日志消息的函数</param>
    /// <param name="sourceExpression">由编译器捕获的源表达式;调用方通常不应显式提供</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <remarks>
    /// 值与成功完成以 <see cref="LogLevel.Debug"/> 级别记录并添加调用链前缀,错误以
    /// <see cref="LogLevel.Error"/> 级别记录且始终添加调用链前缀.错误和完成使用标准消息格式
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        Func<TSource, string> messageFactory,
        [CallerArgumentExpression(nameof(source))] string? sourceExpression = null
    )
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        return LogWithMessageFactory(
            source,
            GetLogger(source),
            notification =>
                notification.Action == ObservableLogAction.OnNext
                    ? messageFactory(notification.Value!)
                    : GetStandardLogMessage(notification),
            LogLevel.Debug,
            LogLevel.Error,
            sourceExpression
        );
    }

    /// <summary>
    /// 使用可观察序列携带的日志记录器和指定级别记录通知,
    /// 并使用工厂生成每个值的完整日志消息
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">携带日志记录器的可观察序列</param>
    /// <param name="messageFactory">根据每个源值生成完整日志消息的函数</param>
    /// <param name="logLevel">用于记录值,错误和完成通知的级别</param>
    /// <param name="sourceExpression">由编译器捕获的源表达式;调用方通常不应显式提供</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <remarks>
    /// Trace,Debug,Error 和 Critical 级别的值与完成通知添加调用链前缀;
    /// 错误通知无论级别为何都添加调用链前缀
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        Func<TSource, string> messageFactory,
        LogLevel logLevel,
        [CallerArgumentExpression(nameof(source))] string? sourceExpression = null
    )
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        return LogWithMessageFactory(
            source,
            GetLogger(source),
            notification =>
                notification.Action == ObservableLogAction.OnNext
                    ? messageFactory(notification.Value!)
                    : GetStandardLogMessage(notification),
            logLevel,
            logLevel,
            sourceExpression
        );
    }

    /// <summary>
    /// 使用可观察序列携带的日志记录器记录通知,
    /// 并使用工厂为值,错误和完成通知生成完整日志消息
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">携带日志记录器的可观察序列</param>
    /// <param name="messageFactory">根据通知上下文生成完整日志消息的函数</param>
    /// <param name="sourceExpression">由编译器捕获的源表达式;调用方通常不应显式提供</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <remarks>Debug 值与完成通知添加调用链前缀;错误通知始终添加调用链前缀</remarks>
    public static IObservable<TSource> LogNotifications<TSource>(
        this IObservable<TSource> source,
        Func<ObservableLogNotification<TSource>, string> messageFactory,
        [CallerArgumentExpression(nameof(source))] string? sourceExpression = null
    ) =>
        LogWithMessageFactory(
            source,
            GetLogger(source),
            messageFactory,
            LogLevel.Debug,
            LogLevel.Error,
            sourceExpression
        );

    /// <summary>
    /// 使用指定的日志记录器记录可观察序列的每个通知,并原样转发
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要记录日志的可观察序列</param>
    /// <param name="logger">接收序列通知的日志记录器</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <param name="sourceExpression">由编译器捕获的源表达式;调用方通常不应显式提供</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <remarks>
    /// 值与成功完成以 <see cref="LogLevel.Debug"/> 级别记录并添加调用链前缀
    /// 提供 <paramref name="message"/> 时使用通知优先格式;未提供时保留原格式
    /// 错误以<see cref="LogLevel.Error"/> 级别记录并保留原始异常,且始终显示调用链
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        ILogger logger,
        string? message = null,
        [CallerArgumentExpression(nameof(source))] string? sourceExpression = null
    )
    {
        return Log(source, logger, LogLevel.Debug, LogLevel.Error, message, sourceExpression);
    }

    /// <summary>
    /// 以指定级别记录可 observable 序列的每个通知,并原样转发
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要记录日志的可观察序列</param>
    /// <param name="logger">接收序列通知的日志记录器</param>
    /// <param name="logLevel">用于记录每个序列通知的级别</param>
    /// <param name="message">用于在日志条目中标识该序列的标签</param>
    /// <param name="sourceExpression">由编译器捕获的源表达式;调用方通常不应显式提供</param>
    /// <returns>记录并转发每个源通知的冷可观察序列</returns>
    /// <remarks>
    /// 提供的级别用于值,错误和成功完成.错误会保留原始异常
    /// Trace,Debug,Error 和 Critical 级别的值与完成通知添加调用链前缀;
    /// 错误通知无论级别为何都添加调用链前缀
    /// 提供 <paramref name="message"/> 时使用通知优先格式;未提供时保留原格式
    /// </remarks>
    public static IObservable<TSource> Log<TSource>(
        this IObservable<TSource> source,
        ILogger logger,
        LogLevel logLevel,
        string? message = null,
        [CallerArgumentExpression(nameof(source))] string? sourceExpression = null
    )
    {
        return Log(source, logger, logLevel, logLevel, message, sourceExpression);
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

    internal static IObservable<T> WithLogger<T>(IObservable<T> observable, object source) =>
        source is IEnableLogger loggerOwner
            ? new ObservableWithLogger<T>(observable, () => loggerOwner.Log())
            : observable;

    internal static IObservable<TResult> PreserveLogger<TSource, TResult>(
        IObservable<TSource> source,
        IObservable<TResult> result
    ) =>
        source is IObservableWithLogger observableWithLogger
            ? new ObservableWithLogger<TResult>(result, observableWithLogger.GetLogger)
            : result;

    private static string GetStandardLogMessage<TSource>(
        ObservableLogNotification<TSource> notification
    ) =>
        notification.Action switch
        {
            ObservableLogAction.OnError =>
                $"Observable: OnError({notification.Exception!.Message})",
            ObservableLogAction.OnCompleted => "Observable: OnCompleted()",
            _ => $"Observable: OnNext({notification.Value})",
        };

    private static IObservable<TSource> LogWithMessageFactory<TSource>(
        IObservable<TSource> source,
        ILogger logger,
        Func<ObservableLogNotification<TSource>, string> messageFactory,
        LogLevel notificationLogLevel,
        LogLevel errorLogLevel,
        string? sourceExpression
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(messageFactory);
        var callChain = new Lazy<string>(
            () => FormatObservableCallChain(sourceExpression),
            LazyThreadSafetyMode.ExecutionAndPublication
        );

        return Create(
            source,
            observer =>
            {
                var stopped = 0;
                var subscription = new SingleAssignmentDisposable();

                void Fail(Exception error)
                {
                    if (Interlocked.Exchange(ref stopped, 1) != 0)
                    {
                        return;
                    }

                    observer.OnError(error);
                    subscription.Dispose();
                }

                bool TryCreateMessage(
                    ObservableLogNotification<TSource> notification,
                    out string message
                )
                {
                    try
                    {
                        message = messageFactory(notification);
                        return true;
                    }
                    catch (Exception exception)
                    {
                        message = string.Empty;
                        Fail(exception);
                        return false;
                    }
                }

                subscription.Disposable = source.Subscribe(
                    value =>
                    {
                        if (Volatile.Read(ref stopped) != 0)
                        {
                            return;
                        }

                        var notification = new ObservableLogNotification<TSource>(
                            ObservableLogAction.OnNext,
                            value,
                            null
                        );
                        if (TryCreateMessage(notification, out var message) is false)
                        {
                            return;
                        }

                        LogObservableMessage(
                            logger,
                            notificationLogLevel,
                            null,
                            callChain,
                            message,
                            forceCallChain: false
                        );
                        observer.OnNext(value);
                    },
                    error =>
                    {
                        if (Volatile.Read(ref stopped) != 0)
                        {
                            return;
                        }

                        var notification = new ObservableLogNotification<TSource>(
                            ObservableLogAction.OnError,
                            default,
                            error
                        );
                        if (TryCreateMessage(notification, out var message) is false)
                        {
                            return;
                        }

                        if (Interlocked.Exchange(ref stopped, 1) == 0)
                        {
                            LogObservableMessage(
                                logger,
                                errorLogLevel,
                                error,
                                callChain,
                                message,
                                forceCallChain: true
                            );
                            observer.OnError(error);
                        }
                    },
                    () =>
                    {
                        if (Volatile.Read(ref stopped) != 0)
                        {
                            return;
                        }

                        var notification = new ObservableLogNotification<TSource>(
                            ObservableLogAction.OnCompleted,
                            default,
                            null
                        );
                        if (TryCreateMessage(notification, out var message) is false)
                        {
                            return;
                        }

                        if (Interlocked.Exchange(ref stopped, 1) == 0)
                        {
                            LogObservableMessage(
                                logger,
                                notificationLogLevel,
                                null,
                                callChain,
                                message,
                                forceCallChain: false
                            );
                            observer.OnCompleted();
                        }
                    }
                );
                return subscription;
            }
        );
    }

    private static void LogObservableMessage(
        ILogger logger,
        LogLevel logLevel,
        Exception? exception,
        Lazy<string> callChain,
        string message,
        bool forceCallChain
    )
    {
        if (forceCallChain || ShouldIncludeCallChain(logLevel))
        {
            logger.Log(
                logLevel,
                exception,
                "{ObservableCallChain} | {Message}",
                callChain.Value,
                message
            );
        }
        else
        {
            logger.Log(logLevel, exception, "{Message}", message);
        }
    }

    private static bool ShouldIncludeCallChain(LogLevel logLevel) =>
        logLevel is LogLevel.Trace or LogLevel.Debug or LogLevel.Error or LogLevel.Critical;

    private static string FormatObservableCallChain(string? sourceExpression)
    {
        if (string.IsNullOrWhiteSpace(sourceExpression))
        {
            return "Log(this)";
        }

        var operators = new List<string>();
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var index = 0; index < sourceExpression.Length; index++)
        {
            var character = sourceExpression[index];
            if (character == '/' && index + 1 < sourceExpression.Length)
            {
                if (sourceExpression[index + 1] == '/')
                {
                    index = sourceExpression.IndexOf('\n', index + 2);
                    if (index < 0)
                    {
                        break;
                    }
                    continue;
                }

                if (sourceExpression[index + 1] == '*')
                {
                    var commentEnd = sourceExpression.IndexOf(
                        "*/",
                        index + 2,
                        StringComparison.Ordinal
                    );
                    if (commentEnd < 0)
                    {
                        break;
                    }
                    index = commentEnd + 1;
                    continue;
                }
            }

            if (character == '"')
            {
                var quoteCount = 1;
                while (
                    index + quoteCount < sourceExpression.Length
                    && sourceExpression[index + quoteCount] == '"'
                )
                {
                    quoteCount++;
                }

                if (quoteCount >= 3)
                {
                    var delimiter = new string('"', quoteCount);
                    var rawStringEnd = sourceExpression.IndexOf(
                        delimiter,
                        index + quoteCount,
                        StringComparison.Ordinal
                    );
                    if (rawStringEnd < 0)
                    {
                        break;
                    }
                    index = rawStringEnd + quoteCount - 1;
                    continue;
                }

                var verbatim = index > 0 && sourceExpression[index - 1] == '@';
                while (++index < sourceExpression.Length)
                {
                    if (sourceExpression[index] != '"')
                    {
                        if (!verbatim && sourceExpression[index] == '\\')
                        {
                            index++;
                        }
                        continue;
                    }

                    if (
                        verbatim
                        && index + 1 < sourceExpression.Length
                        && sourceExpression[index + 1] == '"'
                    )
                    {
                        index++;
                        continue;
                    }
                    break;
                }
                continue;
            }

            if (character == '\'')
            {
                while (++index < sourceExpression.Length)
                {
                    if (sourceExpression[index] == '\\')
                    {
                        index++;
                    }
                    else if (sourceExpression[index] == '\'')
                    {
                        break;
                    }
                }
                continue;
            }

            switch (character)
            {
                case '(':
                    parenthesisDepth++;
                    continue;
                case ')':
                    parenthesisDepth--;
                    continue;
                case '[':
                    bracketDepth++;
                    continue;
                case ']':
                    bracketDepth--;
                    continue;
                case '{':
                    braceDepth++;
                    continue;
                case '}':
                    braceDepth--;
                    continue;
            }

            if (character != '.' || parenthesisDepth != 0 || bracketDepth != 0 || braceDepth != 0)
            {
                continue;
            }

            var nameStart = index + 1;
            while (
                nameStart < sourceExpression.Length
                && char.IsWhiteSpace(sourceExpression[nameStart])
            )
            {
                nameStart++;
            }
            if (nameStart < sourceExpression.Length && sourceExpression[nameStart] == '@')
            {
                nameStart++;
            }

            var nameEnd = nameStart;
            while (
                nameEnd < sourceExpression.Length
                && (
                    char.IsLetterOrDigit(sourceExpression[nameEnd])
                    || sourceExpression[nameEnd] == '_'
                )
            )
            {
                nameEnd++;
            }
            if (nameEnd == nameStart)
            {
                continue;
            }

            var invocationStart = nameEnd;
            while (
                invocationStart < sourceExpression.Length
                && char.IsWhiteSpace(sourceExpression[invocationStart])
            )
            {
                invocationStart++;
            }

            if (
                invocationStart < sourceExpression.Length
                && sourceExpression[invocationStart] == '<'
            )
            {
                var genericDepth = 0;
                do
                {
                    genericDepth += sourceExpression[invocationStart] switch
                    {
                        '<' => 1,
                        '>' => -1,
                        _ => 0,
                    };
                    invocationStart++;
                } while (invocationStart < sourceExpression.Length && genericDepth > 0);

                while (
                    invocationStart < sourceExpression.Length
                    && char.IsWhiteSpace(sourceExpression[invocationStart])
                )
                {
                    invocationStart++;
                }
            }

            if (
                invocationStart < sourceExpression.Length
                && sourceExpression[invocationStart] == '('
            )
            {
                operators.Add(sourceExpression[nameStart..nameEnd]);
            }
        }

        if (operators.Count == 0)
        {
            var receiver = string.Join(
                " ",
                sourceExpression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            );
            return $"{receiver}.Log(this)";
        }

        return $"{string.Join("().", operators)}().Log(this)";
    }

    private static IObservable<TSource> Log<TSource>(
        IObservable<TSource> source,
        ILogger logger,
        LogLevel notificationLogLevel,
        LogLevel errorLogLevel,
        string? message,
        string? sourceExpression
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(logger);
        var hasMessage = string.IsNullOrWhiteSpace(message) is false;
        var callChain = new Lazy<string>(
            () => FormatObservableCallChain(sourceExpression),
            LazyThreadSafetyMode.ExecutionAndPublication
        );

        return Create(
            source,
            observer =>
                source.Subscribe(
                    value =>
                    {
                        if (hasMessage)
                        {
                            if (ShouldIncludeCallChain(notificationLogLevel))
                            {
                                logger.Log(
                                    notificationLogLevel,
                                    "{ObservableCallChain} | OnNext({Value}): {Message}",
                                    callChain.Value,
                                    value,
                                    message
                                );
                            }
                            else
                            {
                                logger.Log(
                                    notificationLogLevel,
                                    "OnNext({Value}): {Message}",
                                    value,
                                    message
                                );
                            }
                        }
                        else if (ShouldIncludeCallChain(notificationLogLevel))
                        {
                            logger.Log(
                                notificationLogLevel,
                                "{ObservableCallChain} | Observable: OnNext({Value})",
                                callChain.Value,
                                value
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
                                "{ObservableCallChain} | OnError({ErrorMessage}): {Message}",
                                callChain.Value,
                                error.Message,
                                message
                            );
                        }
                        else
                        {
                            logger.Log(
                                errorLogLevel,
                                error,
                                "{ObservableCallChain} | Observable: OnError({ErrorMessage})",
                                callChain.Value,
                                error.Message
                            );
                        }
                        observer.OnError(error);
                    },
                    () =>
                    {
                        if (hasMessage)
                        {
                            if (ShouldIncludeCallChain(notificationLogLevel))
                            {
                                logger.Log(
                                    notificationLogLevel,
                                    "{ObservableCallChain} | OnCompleted(): {Message}",
                                    callChain.Value,
                                    message
                                );
                            }
                            else
                            {
                                logger.Log(
                                    notificationLogLevel,
                                    "OnCompleted(): {Message}",
                                    message
                                );
                            }
                        }
                        else if (ShouldIncludeCallChain(notificationLogLevel))
                        {
                            logger.Log(
                                notificationLogLevel,
                                "{ObservableCallChain} | Observable: OnCompleted()",
                                callChain.Value
                            );
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

    /// <summary>
    /// 将每个源值投影为新形式
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <typeparam name="TResult">投影后的值类型</typeparam>
    /// <param name="source">要转换的可观察序列</param>
    /// <param name="selector">应用于每个值的投影</param>
    /// <returns>包含投影值的可观察序列</returns>
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return PreserveLogger(source, new SelectObservable<TSource, TResult>(source, selector));
    }

    /// <summary>
    /// 使用谓词筛选可 observable 序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要筛选的可观察序列</param>
    /// <param name="predicate">用于确定是否发出某个值的函数</param>
    /// <returns>仅包含谓词接受的值的可观察序列</returns>
    public static IObservable<TSource> Where<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return PreserveLogger(source, new WhereObservable<TSource>(source, predicate));
    }

    /// <summary>
    /// 使用默认相等比较器抑制连续的重复值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要比较相邻值的可观察序列</param>
    /// <returns>不含连续重复值的可观察序列</returns>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source
    ) => DistinctUntilChanged(source, EqualityComparer<TSource>.Default);

    /// <summary>
    /// 使用指定的相等比较器抑制连续的重复值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要比较相邻值的可观察序列</param>
    /// <param name="comparer">用于判定相邻值是否相等的比较器</param>
    /// <returns>不含连续重复值的可观察序列</returns>
    public static IObservable<TSource> DistinctUntilChanged<TSource>(
        this IObservable<TSource> source,
        IEqualityComparer<TSource> comparer
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);
        return PreserveLogger(
            source,
            new DistinctUntilChangedObservable<TSource>(source, comparer)
        );
    }

    /// <summary>
    /// 在可观察序列前插入一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要置于其前的可观察序列</param>
    /// <param name="value">在订阅源之前发出的值</param>
    /// <returns>以 <paramref name="value"/> 开头的可观察序列</returns>
    public static IObservable<TSource> StartWith<TSource>(
        this IObservable<TSource> source,
        TSource value
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create(
            source,
            observer =>
            {
                observer.OnNext(value);
                return source.Subscribe(observer);
            }
        );
    }

    /// <summary>
    /// 跳过指定数量的源值,然后发出其余值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要跳过其值的可观察序列</param>
    /// <param name="count">要跳过的起始值数量</param>
    /// <returns>包含跳过前缀之后剩余值的可观察序列</returns>
    public static IObservable<TSource> Skip<TSource>(this IObservable<TSource> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return Create(
            source,
            observer =>
            {
                var remaining = count;
                return source.Subscribe(
                    value =>
                    {
                        if (remaining > 0)
                        {
                            remaining--;
                        }
                        else
                        {
                            observer.OnNext(value);
                        }
                    },
                    observer.OnError,
                    observer.OnCompleted
                );
            }
        );
    }

    /// <summary>
    /// 最多发出指定数量的源值,然后完成
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要从中取值的可观察序列</param>
    /// <param name="count">要发出的最大数量</param>
    /// <returns>最多包含 <paramref name="count"/> 个值的可观察序列</returns>
    public static IObservable<TSource> Take<TSource>(this IObservable<TSource> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return Create(
                source,
                observer =>
                {
                    observer.OnCompleted();
                    return _emptyDisposable;
                }
            );
        }

        return Create(
            source,
            observer =>
            {
                var remaining = count;
                var stopped = false;
                var subscription = new SingleAssignmentDisposable();
                subscription.Disposable = source.Subscribe(
                    value =>
                    {
                        if (stopped)
                        {
                            return;
                        }

                        observer.OnNext(value);
                        remaining--;
                        if (remaining == 0)
                        {
                            stopped = true;
                            observer.OnCompleted();
                            subscription.Dispose();
                        }
                    },
                    error => ForwardError(observer, subscription, ref stopped, error),
                    () => ForwardCompletion(observer, subscription, ref stopped)
                );
                return subscription;
            }
        );
    }

    /// <summary>
    /// 在转发每个值之前对其调用一个操作,并原样转发该值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要检查的可观察序列</param>
    /// <param name="onNext">对每个值调用的副作用操作</param>
    /// <returns>镜像源的可观察序列</returns>
    public static IObservable<TSource> Do<TSource>(
        this IObservable<TSource> source,
        Action<TSource> onNext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return Create(
            source,
            observer =>
            {
                var stopped = false;
                var subscription = new SingleAssignmentDisposable();
                subscription.Disposable = source.Subscribe(
                    value =>
                    {
                        if (stopped)
                        {
                            return;
                        }

                        try
                        {
                            onNext(value);
                        }
                        catch (Exception exception)
                        {
                            ForwardError(observer, subscription, ref stopped, exception);
                            return;
                        }

                        observer.OnNext(value);
                    },
                    error => ForwardError(observer, subscription, ref stopped, error),
                    () => ForwardCompletion(observer, subscription, ref stopped)
                );
                return subscription;
            }
        );
    }

    /// <summary>
    /// 通过同步上下文派发源的值,错误和完成通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要派发其通知的可观察序列</param>
    /// <param name="synchronizationContext">接收通知的同步上下文</param>
    /// <returns>通知被投递到该上下文的可观察序列</returns>
    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return Create(
            source,
            observer =>
            {
                var dispatcher = new NotificationDispatcher<TSource>(
                    observer,
                    synchronizationContext
                );
                var subscription = source.Subscribe(
                    dispatcher.OnNext,
                    dispatcher.OnError,
                    dispatcher.OnCompleted
                );
                dispatcher.SetSubscription(subscription);
                return dispatcher;
            }
        );
    }

    /// <summary>
    /// 通过选定的调度器派发源通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要派发其通知的可观察序列</param>
    /// <param name="scheduler">用于观察者通知的调度器</param>
    /// <returns>通知在选定调度器上进行调度的可观察序列</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 在创建该操作符时捕获
    /// <see cref="SynchronizationContext.Current"/>. 如果没有可用的上下文,
    /// 则回退到线程池.通知按顺序排队
    /// </remarks>
    public static IObservable<TSource> ObserveOn<TSource>(
        this IObservable<TSource> source,
        ObservableSchedulers scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var synchronizationContext = GetSynchronizationContext(scheduler);

        return Create(
            source,
            observer =>
            {
                var dispatcher = new NotificationDispatcher<TSource>(
                    observer,
                    synchronizationContext
                );
                var subscription = source.Subscribe(
                    dispatcher.OnNext,
                    dispatcher.OnError,
                    dispatcher.OnCompleted
                );
                dispatcher.SetSubscription(subscription);
                return dispatcher;
            }
        );
    }

    /// <summary>
    /// 在同步上下文上调度对源的订阅
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要订阅的可观察序列</param>
    /// <param name="synchronizationContext">用于订阅操作的同步上下文</param>
    /// <returns>其源订阅被投递到该上下文的可观察序列</returns>
    public static IObservable<TSource> SubscribeOn<TSource>(
        this IObservable<TSource> source,
        SynchronizationContext synchronizationContext
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return SubscribeOnCore(source, synchronizationContext);
    }

    /// <summary>
    /// 在选定的调度器上调度对源的订阅
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要订阅的可观察序列</param>
    /// <param name="scheduler">用于订阅操作的调度器</param>
    /// <returns>其源订阅被调度的可观察序列</returns>
    /// <remarks>
    /// <see cref="ObservableSchedulers.Current"/> 在创建该操作符时捕获
    /// <see cref="SynchronizationContext.Current"/>.如果没有可用的上下文,
    /// 则回退到线程池. 在计划的操作执行前释放,可避免对源进行订阅
    /// </remarks>
    public static IObservable<TSource> SubscribeOn<TSource>(
        this IObservable<TSource> source,
        ObservableSchedulers scheduler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var synchronizationContext = GetSynchronizationContext(scheduler);
        return SubscribeOnCore(source, synchronizationContext);
    }

    /// <summary>
    /// 仅当源在指定时长内保持静默后,才发出最近的一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要限流的可观察序列</param>
    /// <param name="dueTime">所需的静默时长</param>
    /// <returns>使用 <see cref="TimeProvider.System"/> 的限流可观察序列</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime
    ) => Throttle(source, dueTime, TimeProvider.System);

    /// <summary>
    /// 仅当源在指定时长内保持静默后,才发出最近的一个值,
    /// 并通过选定的调度器派发通知
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要限流的可观察序列</param>
    /// <param name="dueTime">所需的静默时长</param>
    /// <param name="scheduler">用于限流通知的调度器</param>
    /// <returns>通知使用选定调度器的限流可观察序列</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        ObservableSchedulers scheduler
    ) => Throttle(source, dueTime, TimeProvider.System).ObserveOn(scheduler);

    /// <summary>
    /// 使用提供的时间提供程序,仅当源在指定时长内保持静默后,才发出最近的一个值
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要限流的可观察序列</param>
    /// <param name="dueTime">所需的静默时长</param>
    /// <param name="timeProvider">用于创建计时器的时间提供程序</param>
    /// <returns>限流的可观察序列</returns>
    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);

        return Create(
            source,
            observer => new ThrottleSubscription<TSource>(source, observer, dueTime, timeProvider)
        );
    }

    /// <summary>
    /// 当源以错误终止时,继续使用替换的可观察序列
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">要监视错误的可观察序列</param>
    /// <param name="handler">将源错误映射为替换序列的函数</param>
    /// <returns>镜像源,或在发生错误后镜像其替换序列的可观察序列</returns>
    public static IObservable<TSource> Catch<TSource>(
        this IObservable<TSource> source,
        Func<Exception, IObservable<TSource>> handler
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);
        return Create(
            source,
            observer =>
            {
                var subscriptions = new MultipleDisposable();
                var stopped = 0;

                void OnCompleted()
                {
                    if (Interlocked.Exchange(ref stopped, 1) != 0)
                    {
                        return;
                    }

                    observer.OnCompleted();
                }

                void OnError(Exception exception)
                {
                    if (Interlocked.Exchange(ref stopped, 1) != 0)
                    {
                        return;
                    }

                    observer.OnError(exception);
                }

                subscriptions.Add(
                    source.Subscribe(
                        value =>
                        {
                            if (Volatile.Read(ref stopped) != 0)
                            {
                                return;
                            }

                            observer.OnNext(value);
                        },
                        error =>
                        {
                            if (Volatile.Read(ref stopped) != 0)
                            {
                                return;
                            }

                            IObservable<TSource> replacement;
                            try
                            {
                                replacement =
                                    handler(error)
                                    ?? throw new InvalidOperationException(
                                        "The catch handler returned null."
                                    );
                            }
                            catch (Exception exception)
                            {
                                OnError(exception);
                                return;
                            }

                            try
                            {
                                subscriptions.Add(
                                    replacement.Subscribe(
                                        value =>
                                        {
                                            if (Volatile.Read(ref stopped) != 0)
                                            {
                                                return;
                                            }

                                            observer.OnNext(value);
                                        },
                                        OnError,
                                        OnCompleted
                                    )
                                );
                            }
                            catch (Exception exception)
                            {
                                OnError(exception);
                            }
                        },
                        OnCompleted
                    )
                );

                return subscriptions;
            }
        );
    }

    /// <summary>
    /// 创建一个发出单个值后即完成的可观察序列
    /// </summary>
    /// <typeparam name="TSource">发出的值类型</typeparam>
    /// <param name="value">要发出的单个值</param>
    /// <returns>仅包含一个值的可观察序列</returns>
    public static IObservable<TSource> Return<TSource>(TSource value) =>
        Create<TSource>(observer =>
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return _emptyDisposable;
        });

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

    private static IObservable<T> Create<T>(Func<IObserver<T>, IDisposable> subscribe) =>
        new AnonymousObservable<T>(subscribe);

    private static IObservable<TSource> Create<TSource>(
        IObservable<TSource> source,
        Func<IObserver<TSource>, IDisposable> subscribe
    ) => PreserveLogger(source, Create(subscribe));

    private static IObservable<TSource> SubscribeOnCore<TSource>(
        IObservable<TSource> source,
        SynchronizationContext? synchronizationContext
    ) =>
        Create(
            source,
            observer =>
            {
                var scheduledSubscription = new ScheduledSubscription();
                Schedule(
                    synchronizationContext,
                    () =>
                    {
                        if (scheduledSubscription.IsDisposed)
                        {
                            return;
                        }

                        try
                        {
                            var subscription = source.Subscribe(observer);
                            scheduledSubscription.SetSubscription(subscription);
                        }
                        catch (Exception exception)
                        {
                            if (scheduledSubscription.IsDisposed is false)
                            {
                                observer.OnError(exception);
                            }

                            scheduledSubscription.Dispose();
                        }
                    }
                );
                return scheduledSubscription;
            }
        );

    private static void Post(SynchronizationContext context, Action action) =>
        context.Post(static state => ((Action)state!).Invoke(), action);

    private static SynchronizationContext? GetSynchronizationContext(
        ObservableSchedulers scheduler
    ) =>
        scheduler switch
        {
            ObservableSchedulers.Current => SynchronizationContext.Current,
            ObservableSchedulers.ThreadPool => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(scheduler),
                scheduler,
                "Unknown observable scheduler."
            ),
        };

    private static void Schedule(SynchronizationContext? synchronizationContext, Action action)
    {
        if (synchronizationContext is not null)
        {
            Post(synchronizationContext, action);
            return;
        }

        ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), action);
    }

    private sealed class NotificationDispatcher<T> : IDisposable
    {
        private readonly IObserver<T> _observer;
        private readonly object _gate = new();
        private readonly SerialActionQueue _queue;
        private IDisposable? _subscription;
        private bool _stopped;
        private bool _disposed;

        public NotificationDispatcher(IObserver<T> observer, SynchronizationContext? context)
        {
            _observer = observer;
            _queue = new SerialActionQueue(action => Schedule(context, action));
        }

        public void SetSubscription(IDisposable subscription)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    subscription.Dispose();
                    return;
                }

                _subscription = subscription;
            }
        }

        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_stopped || _disposed)
                {
                    return;
                }
            }

            _queue.Enqueue(() =>
            {
                lock (_gate)
                {
                    if (_disposed)
                        return;
                }
                _observer.OnNext(value);
            });
        }

        public void OnError(Exception error) => ScheduleTerminal(() => _observer.OnError(error));

        public void OnCompleted() => ScheduleTerminal(_observer.OnCompleted);

        public void Dispose()
        {
            IDisposable? subscription;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                subscription = _subscription;
                _subscription = null;
            }

            subscription?.Dispose();
            _queue.Dispose();
        }

        private void ScheduleTerminal(Action terminal)
        {
            lock (_gate)
            {
                if (_stopped || _disposed)
                {
                    return;
                }

                _stopped = true;
            }

            _queue.Enqueue(() =>
            {
                lock (_gate)
                {
                    if (_disposed)
                        return;
                }
                terminal();
                Dispose();
            });
        }
    }

    private sealed class ScheduledSubscription : IDisposable
    {
        private readonly object _gate = new();
        private IDisposable? _subscription;
        private bool _disposed;

        public bool IsDisposed
        {
            get
            {
                lock (_gate)
                {
                    return _disposed;
                }
            }
        }

        public void SetSubscription(IDisposable subscription)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    subscription.Dispose();
                    return;
                }

                _subscription = subscription;
            }
        }

        public void Dispose()
        {
            IDisposable? subscription;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                subscription = _subscription;
                _subscription = null;
            }

            subscription?.Dispose();
        }
    }

    private static void ForwardError<T>(
        IObserver<T> observer,
        IDisposable subscription,
        ref bool stopped,
        Exception error
    )
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        observer.OnError(error);
        subscription.Dispose();
    }

    private static void ForwardCompletion<T>(
        IObserver<T> observer,
        IDisposable subscription,
        ref bool stopped
    )
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        observer.OnCompleted();
        subscription.Dispose();
    }
}
