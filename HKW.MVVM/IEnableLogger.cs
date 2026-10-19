using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVM;

/// <summary>
/// 将类型标记为支持 <see cref="LoggerMixins.Log(IEnableLogger)"/> 日志混入
/// </summary>
/// <remarks>
/// 该标记接口不定义任何成员.实现它并调用 <c>this.Log()</c> 即可获得一个
/// <see cref="ILogger"/>,其类别为运行时类型的完全限定名称.日志记录器按运行时类型
/// 缓存,并在日志记录器工厂发生变化前重复使用
/// </remarks>
[ComVisible(false)]
public interface IEnableLogger;

/// <summary>
/// 将类型标记为可提供自身的 <see cref="ILogger"/> 实例
/// </summary>
/// <remarks>
/// 在实现类上调用 <c>this.Log()</c> 会返回 <see cref="Logger"/>,而不会从
/// 默认的 <see cref="ILoggerFactory"/> 解析日志记录器
/// </remarks>
public interface IEnableLoggerWithLogger : IEnableLogger
{
    /// <summary>
    /// 获取由此实例拥有的日志记录器
    /// </summary>
    ILogger Logger { get; }
}

/// <summary>
/// 提供对 <see cref="LoggerMixins.Log(IEnableLogger)"/> 所使用的日志记录器工厂和日志域的访问
/// </summary>
public static class LogHost
{
    private const int LoggerCacheSize = 64;
    private static readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    DomainGate = new();
    private static LoggerCache _loggerCache = new(NullLoggerFactory.Instance);
    private static ILoggerFactory? _loggerFactoryOverride;
    private static DomainRegistrationSnapshot _domainSnapshot = DomainRegistrationSnapshot.Empty;
    private static long _nextDomainRegistrationId;

    /// <summary>
    /// 获取与 <see cref="LogHost"/> 关联的默认 <see cref="ILogger"/>
    /// </summary>
    /// <remarks>
    /// 该日志记录器由 <see cref="LoggerFactory"/> 创建,并使用
    /// <see cref="LogHost"/> 的完全限定名称作为其类别
    /// </remarks>
#pragma warning disable S6669
    public static ILogger Default => GetLogger(typeof(LogHost));
#pragma warning restore S6669

    /// <summary>
    /// 获取或设置显式的进程级日志记录器工厂
    /// </summary>
    /// <remarks>
    /// 未配置显式工厂时,getter 会从 <see cref="Ioc.Default"/> 解析 <see cref="ILoggerFactory"/>
    /// 如果 IoC 容器尚未配置或其中没有日志记录器工厂,
    /// 则返回 <see cref="NullLoggerFactory.Instance"/>
    /// </remarks>
    public static ILoggerFactory LoggerFactory
    {
        get => Volatile.Read(ref _loggerFactoryOverride) ?? GetDefaultLoggerFactory();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref _loggerFactoryOverride, value);
            Interlocked.Exchange(ref _loggerCache, new LoggerCache(value));
        }
    }

    /// <summary>
    /// 为 <typeparamref name="T"/> 及其派生类型注册专用的日志记录器工厂
    /// </summary>
    /// <typeparam name="T">要注册日志域的类或接口</typeparam>
    /// <param name="loggerFactory">日志域使用的日志记录器工厂</param>
    /// <returns>移除本次日志域注册的可释放对象</returns>
    /// <remarks>
    /// 类域优先于接口域,多个类域使用继承链上最接近运行时类型的域;
    /// 多个接口域使用最近注册且尚未释放的域.日志类别仍为对象运行时类型的完全限定名称.
    /// 释放返回的对象不会释放 <paramref name="loggerFactory"/>.
    /// </remarks>
    public static IDisposable RegisterDomain<T>(ILoggerFactory loggerFactory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var registration = new DomainRegistration(
            Interlocked.Increment(ref _nextDomainRegistrationId),
            typeof(T),
            loggerFactory
        );
        lock (DomainGate)
        {
            var snapshot = Volatile.Read(ref _domainSnapshot);
            Volatile.Write(
                ref _domainSnapshot,
                new DomainRegistrationSnapshot([.. snapshot.Registrations, registration])
            );
        }

        return new DomainRegistrationHandle(registration.Id);
    }

    /// <summary>
    /// 移除显式的日志记录器工厂替代设置,使后续的日志记录器请求使用
    /// <see cref="Ioc.Default"/> 中注册的工厂
    /// </summary>
    public static void UseDefaultLoggerFactory()
    {
        Volatile.Write(ref _loggerFactoryOverride, null);
        Interlocked.Exchange(ref _loggerCache, new LoggerCache(GetDefaultLoggerFactory()));
    }

    internal static ILogger GetLogger(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var domainRegistration = FindDomainRegistration(type);
        if (domainRegistration is not null)
        {
            return domainRegistration.LoggerCache.CacheGetLogger(type);
        }

        var loggerFactory = LoggerFactory;
        var loggerCache = Volatile.Read(ref _loggerCache);
        if (ReferenceEquals(loggerCache.CacheLoggerFactory, loggerFactory) is false)
        {
            loggerCache = new LoggerCache(loggerFactory);
            Interlocked.Exchange(ref _loggerCache, loggerCache);
        }

        return loggerCache.CacheGetLogger(type);
    }

    private static DomainRegistration? FindDomainRegistration(Type type)
    {
        var snapshot = Volatile.Read(ref _domainSnapshot);
        for (var currentType = type; currentType is not null; currentType = currentType.BaseType)
        {
            if (snapshot.ClassRegistrations.TryGetValue(currentType, out var registration))
            {
                return registration;
            }
        }

        var interfaceRegistrations = snapshot.InterfaceRegistrations;
        for (var index = interfaceRegistrations.Length - 1; index >= 0; index--)
        {
            var registration = interfaceRegistrations[index];
            if (registration.DomainType.IsAssignableFrom(type))
            {
                return registration;
            }
        }

        return null;
    }

    private static ILoggerFactory GetDefaultLoggerFactory()
    {
        try
        {
            return Ioc.Default.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        }
        catch (InvalidOperationException)
        {
            // 在调用 ConfigureServices 之前, Ioc 会抛出异常
            return NullLoggerFactory.Instance;
        }
    }

    private sealed class DomainRegistration(long id, Type domainType, ILoggerFactory loggerFactory)
    {
        public long Id { get; } = id;

        public Type DomainType { get; } = domainType;

        public LoggerCache LoggerCache { get; } = new(loggerFactory);
    }

    private sealed class DomainRegistrationSnapshot
    {
        public static DomainRegistrationSnapshot Empty { get; } = new([]);

        public DomainRegistrationSnapshot(DomainRegistration[] registrations)
        {
            Registrations = registrations;
            ClassRegistrations = new Dictionary<Type, DomainRegistration>(registrations.Length);
            var interfaceRegistrations = new List<DomainRegistration>();
            foreach (var registration in registrations)
            {
                if (registration.DomainType.IsClass)
                {
                    ClassRegistrations[registration.DomainType] = registration;
                }
                else
                {
                    interfaceRegistrations.Add(registration);
                }
            }

            InterfaceRegistrations = [.. interfaceRegistrations];
        }

        public DomainRegistration[] Registrations { get; }

        public Dictionary<Type, DomainRegistration> ClassRegistrations { get; }

        public DomainRegistration[] InterfaceRegistrations { get; }
    }

    private sealed class DomainRegistrationHandle(long registrationId) : IDisposable
    {
        private int _isDisposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                RemoveDomainRegistration(registrationId);
            }
        }

        private static void RemoveDomainRegistration(long id)
        {
            lock (DomainGate)
            {
                var snapshot = Volatile.Read(ref _domainSnapshot);
                var registrations = snapshot.Registrations;
                var index = Array.FindIndex(registrations, registration => registration.Id == id);
                if (index < 0)
                {
                    return;
                }

                var updatedRegistrations = new DomainRegistration[registrations.Length - 1];
                Array.Copy(registrations, updatedRegistrations, index);
                Array.Copy(
                    registrations,
                    index + 1,
                    updatedRegistrations,
                    index,
                    registrations.Length - index - 1
                );
                Volatile.Write(
                    ref _domainSnapshot,
                    new DomainRegistrationSnapshot(updatedRegistrations)
                );
            }
        }
    }

    private sealed class LoggerCache
    {
        private readonly MemoizingLRUCache<Type, ILogger> _loggers;

        public LoggerCache(ILoggerFactory loggerFactory)
        {
            CacheLoggerFactory = loggerFactory;
            _loggers = new MemoizingLRUCache<Type, ILogger>(CreateLogger, LoggerCacheSize);
        }

        public ILoggerFactory CacheLoggerFactory { get; }

        public ILogger CacheGetLogger(Type type) => _loggers.Get(type);

        private ILogger CreateLogger(Type type) =>
            CacheLoggerFactory.CreateLogger(type.FullName ?? type.Name)
            ?? throw new InvalidOperationException(
                "The configured logger factory returned a null logger."
            );
    }
}

/// <summary>
/// 面向 <see cref="IEnableLogger"/> 实现的日志扩展
/// </summary>
public static class LoggerMixins
{
    /// <summary>
    /// 获取以实例运行时类型为类别的日志记录器
    /// </summary>
    /// <param name="instance">启用了日志记录器的实例</param>
    /// <returns>由匹配的日志域或 <see cref="LogHost.LoggerFactory"/> 创建的缓存日志记录器</returns>
    /// <remarks>
    /// 运行时类型用作日志记录器类别和日志域匹配,不会检查该类型是否实现标记接口
    /// </remarks>
    public static ILogger Log(this IEnableLogger instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance is IEnableLoggerWithLogger loggerOwner)
        {
            return instance.Log(loggerOwner.Logger);
        }

        return LogHost.GetLogger(instance.GetType());
    }

    /// <summary>
    /// 代表启用了日志记录器的实例,使用指定的日志记录器
    /// </summary>
    /// <param name="instance">启用了日志记录器的实例</param>
    /// <param name="logger">要使用的日志记录器</param>
    /// <returns><paramref name="logger"/></returns>
    public static ILogger Log(this IEnableLogger instance, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(logger);
        return logger;
    }

    /// <summary>
    /// 代表启用了日志记录器的实例, 使用指定的日志记录器检查日志等级是否启用
    /// </summary>
    /// <param name="instance">启用了日志记录器的实例</param>
    /// <param name="logLevel">启用的日志等级</param>
    /// <returns>
    /// 日志等级启用时, 返回日志记录器及日志等级; 否则返回 <see langword="null"/>
    /// </returns>
    public static (ILogger logger, LogLevel logLevel)? LogIfEnabled(
        this IEnableLogger instance,
        LogLevel logLevel
    )
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance is IEnableLoggerWithLogger loggerOwner)
        {
            return loggerOwner.Logger.IsEnabled(logLevel)
                ? (instance.Log(loggerOwner.Logger), logLevel)
                : null;
        }
        else
        {
            var logger = LogHost.GetLogger(instance.GetType());

            return logger.IsEnabled(logLevel) ? (logger, logLevel) : null;
        }
    }

    /// <summary>
    /// 代表启用了日志记录器的实例, 使用指定的日志记录器检查日志等级是否启用
    /// </summary>
    /// <param name="instance">启用了日志记录器的实例</param>
    /// <param name="logger">要使用的日志记录器</param>
    /// <param name="logLevel">要检查的日志等级</param>
    /// <returns>
    /// 日志等级启用时, 返回日志记录器及日志等级; 否则返回 <see langword="null"/>
    /// </returns>
    public static (ILogger logger, LogLevel logLevel)? LogIfEnabled(
        this IEnableLogger instance,
        ILogger logger,
        LogLevel logLevel
    )
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(logger);

        return logger.IsEnabled(logLevel) ? (logger, logLevel) : null;
    }

    /// <summary>
    /// 使用元组中指定的日志等级格式化并写入日志消息
    /// </summary>
    /// <param name="log">日志记录器及其日志等级</param>
    /// <param name="message">日志消息的格式字符串</param>
    /// <param name="args">用于格式化日志消息的参数</param>
    public static void Log(
        this (ILogger logger, LogLevel logLevel) log,
        string? message,
        params object?[] args
    )
    {
        log.logger.Log(log.logLevel, message, args);
    }

    /// <summary>
    /// 使用元组中指定的日志等级格式化并写入日志消息
    /// </summary>
    /// <param name="log">日志记录器及其日志等级</param>
    /// <param name="eventId">与日志关联的事件标识</param>
    /// <param name="message">日志消息的格式字符串</param>
    /// <param name="args">用于格式化日志消息的参数</param>
    public static void Log(
        this (ILogger logger, LogLevel logLevel) log,
        EventId eventId,
        string? message,
        params object?[] args
    )
    {
        log.logger.Log(log.logLevel, eventId, message, args);
    }

    /// <summary>
    /// 使用元组中指定的日志等级格式化并写入日志消息
    /// </summary>
    /// <param name="log">日志记录器及其日志等级</param>
    /// <param name="exception">要记录的异常</param>
    /// <param name="message">日志消息的格式字符串</param>
    /// <param name="args">用于格式化日志消息的参数</param>
    public static void Log(
        this (ILogger logger, LogLevel logLevel) log,
        Exception? exception,
        string? message,
        params object?[] args
    )
    {
        log.logger.Log(log.logLevel, exception, message, args);
    }

    /// <summary>
    /// 使用元组中指定的日志等级格式化并写入日志消息
    /// </summary>
    /// <param name="log">日志记录器及其日志等级</param>
    /// <param name="eventId">与日志关联的事件标识</param>
    /// <param name="exception">要记录的异常</param>
    /// <param name="message">日志消息的格式字符串</param>
    /// <param name="args">用于格式化日志消息的参数</param>
    public static void Log(
        this (ILogger logger, LogLevel logLevel) log,
        EventId eventId,
        Exception? exception,
        string? message,
        params object?[] args
    )
    {
        log.logger.Log(log.logLevel, eventId, exception, message, args);
    }
}
