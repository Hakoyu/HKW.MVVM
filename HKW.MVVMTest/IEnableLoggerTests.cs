using HKW.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
[DoNotParallelize]
public sealed class IEnableLoggerTests
{
    private ILoggerFactory _originalFactory = null!;

    [TestInitialize]
    public void Initialize() => _originalFactory = LogHost.LoggerFactory;

    [TestCleanup]
    public void Cleanup() => LogHost.LoggerFactory = _originalFactory;

    [TestMethod]
    public void Log_UsesRuntimeTypeFullNameAsCategory()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;
        IEnableLogger instance = new LoggerEnabledType();

        var logger = instance.Log();

        Assert.AreSame(factory.Logger, logger);
        Assert.AreEqual(typeof(LoggerEnabledType).FullName, factory.CategoryName);
    }

    [TestMethod]
    public void Log_UsesLatestConfiguredFactory()
    {
        var instance = new LoggerEnabledType();
        var firstFactory = new RecordingLoggerFactory();
        var secondFactory = new RecordingLoggerFactory();

        LogHost.LoggerFactory = firstFactory;
        var firstLogger = instance.Log();
        LogHost.LoggerFactory = secondFactory;
        var secondLogger = instance.Log();

        Assert.AreSame(firstFactory.Logger, firstLogger);
        Assert.AreSame(secondFactory.Logger, secondLogger);
    }

    [TestMethod]
    public void Log_WithNullInstance_ThrowsArgumentNullException()
    {
        IEnableLogger instance = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() => instance.Log());
    }

    [TestMethod]
    public void LoggerFactory_WithNullValue_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LogHost.LoggerFactory = null!);
    }

    [TestMethod]
    public void DefaultLoggerFactory_IsNullLoggerFactory()
    {
        LogHost.LoggerFactory = NullLoggerFactory.Instance;

        Assert.AreSame(NullLogger.Instance, new LoggerEnabledType().Log());
    }

    private sealed class LoggerEnabledType : IEnableLogger;

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public ILogger Logger { get; } = new RecordingLogger();

        public string? CategoryName { get; private set; }

        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName)
        {
            CategoryName = categoryName;
            return Logger;
        }

        public void Dispose() { }
    }

    private sealed class RecordingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}