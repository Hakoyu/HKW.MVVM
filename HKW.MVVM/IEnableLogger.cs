using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVM;

/// <summary>
/// Marks a type as supporting the <see cref="LoggerMixins.Log(IEnableLogger)"/> logging mixin.
/// </summary>
/// <remarks>
/// Implementing this interface adds no members to a type. Call <c>this.Log()</c> to obtain an
/// <see cref="ILogger"/> whose category is the runtime type's fully qualified name.
/// </remarks>
public interface IEnableLogger;

/// <summary>
/// Provides the logger factory used by <see cref="LoggerMixins.Log(IEnableLogger)"/>.
/// </summary>
public static class LogHost
{
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    /// <summary>
    /// Gets or sets the process-wide logger factory. The default is
    /// <see cref="NullLoggerFactory.Instance"/>.
    /// </summary>
    public static ILoggerFactory LoggerFactory
    {
        get => Volatile.Read(ref _loggerFactory);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref _loggerFactory, value);
        }
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
    /// <returns>A logger created by <see cref="LogHost.LoggerFactory"/>.</returns>
    public static ILogger Log(this IEnableLogger instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = instance.GetType();
        return LogHost.LoggerFactory.CreateLogger(type.FullName ?? type.Name);
    }
}