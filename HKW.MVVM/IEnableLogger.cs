using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVM;

/// <summary>Marks a type as supporting the <see cref="LoggerMixins.Log(IEnableLogger)"/> logging mixin.</summary>
/// <remarks>
/// This marker interface defines no members. Implement it and call <c>this.Log()</c> to obtain an
/// <see cref="ILogger"/> whose category is the runtime type's fully qualified name. Loggers are cached by runtime
/// type and reused until the logger factory changes.
/// </remarks>
[ComVisible(false)]
[SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "The Log() extension method intentionally uses this marker interface to opt types into logging."
)]
public interface IEnableLogger;

/// <summary>
/// Marks a type as providing its own <see cref="ILogger"/> instance.
/// </summary>
/// <remarks>
/// Calling <c>this.Log()</c> on an implementation returns <see cref="Logger"/> instead of resolving a logger from
/// the default <see cref="ILoggerFactory"/>.
/// </remarks>
public interface IEnableLoggerWithLogger : IEnableLogger
{
    /// <summary>Gets the logger owned by this instance.</summary>
    ILogger Logger { get; }
}

/// <summary>
/// Provides access to the logger factory used by <see cref="LoggerMixins.Log(IEnableLogger)"/>.
/// </summary>
public static class LogHost
{
    private const int LoggerCacheSize = 64;
    private static LoggerCache _loggerCache = new(NullLoggerFactory.Instance);
    private static ILoggerFactory? _loggerFactoryOverride;

    /// <summary>
    /// Gets or sets an explicit process-wide logger factory.
    /// </summary>
    /// <remarks>
    /// When no explicit factory is configured, the getter resolves <see cref="ILoggerFactory"/> from
    /// <see cref="Ioc.Default"/>. If the IoC container has not been configured or has no logger factory,
    /// <see cref="NullLoggerFactory.Instance"/> is returned.
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
    /// Removes the explicit logger factory override so subsequent logger requests use the factory registered in
    /// <see cref="Ioc.Default"/>.
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
        if (!ReferenceEquals(loggerCache.CacheLoggerFactory, loggerFactory))
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
            // CommunityToolkit's Ioc throws until ConfigureServices has been called.
            return NullLoggerFactory.Instance;
        }
    }

    private sealed class LoggerCache
    {
        private readonly MemoizingMRUCache<Type, ILogger> _loggers;

        public LoggerCache(ILoggerFactory loggerFactory)
        {
            CacheLoggerFactory = loggerFactory;
            _loggers = new MemoizingMRUCache<Type, ILogger>(CreateLogger, LoggerCacheSize);
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
/// Logging extensions for <see cref="IEnableLogger"/> implementations.
/// </summary>
public static class LoggerMixins
{
    /// <summary>
    /// Gets a logger categorized by the instance's runtime type.
    /// </summary>
    /// <param name="instance">The logger-enabled instance.</param>
    /// <returns>A cached logger created by <see cref="LogHost.LoggerFactory"/>.</returns>
    /// <remarks>
    /// The runtime type is used only as the logger category and is not inspected through reflection.
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

    /// <summary>Uses the specified logger on behalf of a logger-enabled instance.</summary>
    /// <param name="instance">The logger-enabled instance.</param>
    /// <param name="logger">The logger to use.</param>
    /// <returns><paramref name="logger"/>.</returns>
    public static ILogger Log(this IEnableLogger instance, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(logger);
        return logger;
    }
}
