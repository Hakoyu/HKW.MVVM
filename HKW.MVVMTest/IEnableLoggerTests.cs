using HKW.MVVM;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
[DoNotParallelize]
public sealed class IEnableLoggerTests
{
    private static readonly RecordingLoggerFactory IocLoggerFactory = new();
    private ILoggerFactory _originalFactory = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) =>
        Ioc.Default.ConfigureServices(new LoggerFactoryServiceProvider(IocLoggerFactory));

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
    public void Log_ForSameRuntimeType_ReusesCachedLogger()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;

        var firstLogger = new LoggerEnabledType().Log();
        var secondLogger = new LoggerEnabledType().Log();

        Assert.AreSame(firstLogger, secondLogger);
        Assert.AreEqual(1, factory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void Log_ForDifferentRuntimeTypes_CreatesDifferentCategories()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;

        _ = new LoggerEnabledType().Log();
        _ = new AnotherLoggerEnabledType().Log();

        CollectionAssert.AreEquivalent(
            new[] { typeof(LoggerEnabledType).FullName!, typeof(AnotherLoggerEnabledType).FullName! },
            factory.CategoryNames);
    }

    [TestMethod]
    public void Log_WhenCalledConcurrently_CreatesLoggerOnce()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;
        var instances = Enumerable.Range(0, 32).Select(_ => new LoggerEnabledType()).ToArray();

        var loggers = instances.AsParallel().Select(instance => instance.Log()).ToArray();

        Assert.IsTrue(loggers.All(logger => ReferenceEquals(factory.Logger, logger)));
        Assert.AreEqual(1, factory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void Log_WithLogger_ReturnsSpecifiedLogger()
    {
        var instance = new LoggerEnabledType();
        var logger = new RecordingLogger();

        var result = instance.Log(logger);

        Assert.AreSame(logger, result);
    }

    [TestMethod]
    public void Log_WithOwnedLogger_ReturnsLoggerProperty()
    {
        var logger = new RecordingLogger();
        var instance = new LoggerOwningType(logger);

        var result = instance.Log();

        Assert.AreSame(logger, result);
    }

    [TestMethod]
    public void Log_WithOwnedLoggerThroughBaseInterface_ReturnsLoggerProperty()
    {
        var logger = new RecordingLogger();
        IEnableLogger instance = new LoggerOwningType(logger);

        var result = instance.Log();

        Assert.AreSame(logger, result);
    }

    [TestMethod]
    public void UseDefaultLoggerFactory_UsesIocLoggerFactory()
    {
        var initialCallCount = IocLoggerFactory.CreateLoggerCallCount;
        LogHost.UseDefaultLoggerFactory();

        var logger = new LoggerEnabledType().Log();

        Assert.AreSame(IocLoggerFactory.Logger, logger);
        Assert.AreEqual(initialCallCount + 1, IocLoggerFactory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void Log_WithNullInstance_ThrowsArgumentNullException()
    {
        IEnableLogger instance = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() => instance.Log());
    }

    [TestMethod]
    public void Log_WithNullLogger_ThrowsArgumentNullException()
    {
        var instance = new LoggerEnabledType();

        Assert.ThrowsExactly<ArgumentNullException>(() => instance.Log(null!));
    }

    [TestMethod]
    public void Log_WithNullOwnedLogger_ThrowsArgumentNullException()
    {
        var instance = new LoggerOwningType(null!);

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

    private sealed class AnotherLoggerEnabledType : IEnableLogger;

    private sealed class LoggerOwningType(ILogger logger) : IEnableLoggerWithLogger
    {
        public ILogger Logger { get; } = logger;
    }

    private sealed class LoggerFactoryServiceProvider(ILoggerFactory loggerFactory) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ILoggerFactory) ? loggerFactory : null;
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public ILogger Logger { get; } = new RecordingLogger();

        public string? CategoryName { get; private set; }

        public List<string> CategoryNames { get; } = [];

        public int CreateLoggerCallCount { get; private set; }

        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName)
        {
            CategoryName = categoryName;
            CategoryNames.Add(categoryName);
            CreateLoggerCallCount++;
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
            Func<TState, Exception?, string> formatter
        )
        { }
    }
}