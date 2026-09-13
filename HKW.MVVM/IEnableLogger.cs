using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVM;

/// <summary>
/// 将类型标记为支持 <see cref="LoggerMixins.Log(IEnableLogger)"/> 日志混入.
/// </summary>
/// <remarks>
/// 该标记接口不定义任何成员.实现它并调用 <c>this.Log()</c> 即可获得一个
/// <see cref="ILogger"/>,其类别为运行时类型的完全限定名称.日志记录器按运行时类型
/// 缓存,并在日志记录器工厂发生变化前重复使用.
/// </remarks>
[ComVisible(false)]
public interface IEnableLogger;

/// <summary>
/// 将类型标记为可提供自身的 <see cref="ILogger"/> 实例.
/// </summary>
/// <remarks>
/// 在实现类上调用 <c>this.Log()</c> 会返回 <see cref="Logger"/>,而不会从
/// 默认的 <see cref="ILoggerFactory"/> 解析日志记录器.
/// </remarks>
public interface IEnableLoggerWithLogger : IEnableLogger
{
    /// <summary>
    /// 获取由此实例拥有的日志记录器.
    /// </summary>
    ILogger Logger { get; }
}

/// <summary>
/// 提供对 <see cref="LoggerMixins.Log(IEnableLogger)"/> 所使用的日志记录器工厂的访问.
/// </summary>
public static class LogHost
{
    private const int LoggerCacheSize = 64;
    private static LoggerCache _loggerCache = new(NullLoggerFactory.Instance);
    private static ILoggerFactory? _loggerFactoryOverride;

    /// <summary>
    /// 获取与 <see cref="LogHost"/> 关联的默认 <see cref="ILogger"/>.
    /// </summary>
    /// <remarks>
    /// 该日志记录器由 <see cref="LoggerFactory"/> 创建,并使用
    /// <see cref="LogHost"/> 的完全限定名称作为其类别.
    /// </remarks>
#pragma warning disable S6669
    public static ILogger Default => GetLogger(typeof(LogHost));
#pragma warning restore S6669

    /// <summary>
    /// 获取或设置显式的进程级日志记录器工厂.
    /// </summary>
    /// <remarks>
    /// 未配置显式工厂时,getter 会从 <see cref="Ioc.Default"/> 解析 <see cref="ILoggerFactory"/>.
    /// 如果 IoC 容器尚未配置或其中没有日志记录器工厂,
    /// 则返回 <see cref="NullLoggerFactory.Instance"/>.
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
    /// 移除显式的日志记录器工厂替代设置,使后续的日志记录器请求使用
    /// <see cref="Ioc.Default"/> 中注册的工厂.
    /// </summary>
    public static void UseDefaultLoggerFactory()
    {
        Volatile.Write(ref _loggerFactoryOverride, null);
        Interlocked.Exchange(ref _loggerCache, new LoggerCache(GetDefaultLoggerFactory()));
    }

    internal static ILogger GetLogger(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var loggerFactory = LoggerFactory;
        var loggerCache = Volatile.Read(ref _loggerCache);
        if (ReferenceEquals(loggerCache.CacheLoggerFactory, loggerFactory) is false)
        {
            loggerCache = new LoggerCache(loggerFactory);
            Interlocked.Exchange(ref _loggerCache, loggerCache);
        }

        return loggerCache.CacheGetLogger(type);
    }

    private static ILoggerFactory GetDefaultLoggerFactory()
    {
        try
        {
            return Ioc.Default.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        }
        catch (InvalidOperationException)
        {
            // 在调用 ConfigureServices 之前, Ioc 会抛出异常.
            return NullLoggerFactory.Instance;
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
/// 面向 <see cref="IEnableLogger"/> 实现的日志扩展.
/// </summary>
public static class LoggerMixins
{
    /// <summary>
    /// 获取以实例运行时类型为类别的日志记录器.
    /// </summary>
    /// <param name="instance">启用了日志记录器的实例.</param>
    /// <returns>由 <see cref="LogHost.LoggerFactory"/> 创建的缓存日志记录器.</returns>
    /// <remarks>
    /// 运行时类型仅用作日志记录器类别,不会通过反射进行检查.
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
    /// 代表启用了日志记录器的实例,使用指定的日志记录器.
    /// </summary>
    /// <param name="instance">启用了日志记录器的实例.</param>
    /// <param name="logger">要使用的日志记录器.</param>
    /// <returns><paramref name="logger"/>.</returns>
    public static ILogger Log(this IEnableLogger instance, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(logger);
        return logger;
    }
}
