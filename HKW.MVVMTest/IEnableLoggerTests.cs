using CommunityToolkit.Mvvm.DependencyInjection;
using HKW.MVVM;
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
    public void UsesRuntimeTypeFullNameAsCategory()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;
        IEnableLogger instance = new LoggerEnabledType();

        var logger = instance.Log();

        Assert.AreSame(factory.Logger, logger);
        Assert.AreEqual(typeof(LoggerEnabledType).FullName, factory.CategoryName);
    }

    [TestMethod]
    public void UsesLatestConfiguredFactory()
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
    public void ForSameRuntimeType_ReusesCachedLogger()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;

        var firstLogger = new LoggerEnabledType().Log();
        var secondLogger = new LoggerEnabledType().Log();

        Assert.AreSame(firstLogger, secondLogger);
        Assert.AreEqual(1, factory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void ForDifferentRuntimeTypes_CreatesDifferentCategories()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;

        _ = new LoggerEnabledType().Log();
        _ = new AnotherLoggerEnabledType().Log();

        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(LoggerEnabledType).FullName!,
                typeof(AnotherLoggerEnabledType).FullName!,
            },
            factory.CategoryNames
        );
    }

    [TestMethod]
    public void WhenCalledConcurrently_CreatesLoggerOnce()
    {
        var factory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = factory;
        var instances = Enumerable.Range(0, 32).Select(_ => new LoggerEnabledType()).ToArray();

        var loggers = instances.AsParallel().Select(instance => instance.Log()).ToArray();

        Assert.IsTrue(loggers.All(logger => ReferenceEquals(factory.Logger, logger)));
        Assert.AreEqual(1, factory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void WithDomainForRuntimeType_UsesDomainFactory()
    {
        var domainFactory = new RecordingLoggerFactory();
        using var domain = LogHost.RegisterDomain<LoggerEnabledType>(domainFactory);

        var logger = new LoggerEnabledType().Log();

        Assert.AreSame(domainFactory.Logger, logger);
        Assert.AreEqual(typeof(LoggerEnabledType).FullName, domainFactory.CategoryName);
    }

    [TestMethod]
    public void WithDomainForBaseType_UsesDomainFactoryForDerivedType()
    {
        var domainFactory = new RecordingLoggerFactory();
        using var domain = LogHost.RegisterDomain<DomainBase>(domainFactory);

        var logger = new DomainDerived().Log();

        Assert.AreSame(domainFactory.Logger, logger);
        Assert.AreEqual(typeof(DomainDerived).FullName, domainFactory.CategoryName);
    }

    [TestMethod]
    public void WithMultipleClassDomains_UsesNearestDomain()
    {
        var baseFactory = new RecordingLoggerFactory();
        var middleFactory = new RecordingLoggerFactory();
        using var baseDomain = LogHost.RegisterDomain<DomainBase>(baseFactory);
        using var middleDomain = LogHost.RegisterDomain<DomainMiddle>(middleFactory);

        var logger = new DomainDerived().Log();

        Assert.AreSame(middleFactory.Logger, logger);
        Assert.AreEqual(0, baseFactory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void WithClassAndInterfaceDomains_UsesClassDomain()
    {
        var classFactory = new RecordingLoggerFactory();
        var interfaceFactory = new RecordingLoggerFactory();
        using var classDomain = LogHost.RegisterDomain<DomainBase>(classFactory);
        using var interfaceDomain = LogHost.RegisterDomain<ILoggerDomain>(interfaceFactory);

        var logger = new DomainDerived().Log();

        Assert.AreSame(classFactory.Logger, logger);
        Assert.AreEqual(0, interfaceFactory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void WithMultipleInterfaceDomains_UsesMostRecentlyRegisteredDomain()
    {
        var firstFactory = new RecordingLoggerFactory();
        var secondFactory = new RecordingLoggerFactory();
        using var firstDomain = LogHost.RegisterDomain<ILoggerDomain>(firstFactory);
        using var secondDomain = LogHost.RegisterDomain<ISecondaryLoggerDomain>(secondFactory);

        var logger = new MultiInterfaceLoggerEnabledType().Log();

        Assert.AreSame(secondFactory.Logger, logger);
        Assert.AreEqual(0, firstFactory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void WithNestedDomainsForSameType_DisposeRestoresPreviousDomain()
    {
        var firstFactory = new RecordingLoggerFactory();
        var secondFactory = new RecordingLoggerFactory();
        using var firstDomain = LogHost.RegisterDomain<LoggerEnabledType>(firstFactory);
        var secondDomain = LogHost.RegisterDomain<LoggerEnabledType>(secondFactory);

        Assert.AreSame(secondFactory.Logger, new LoggerEnabledType().Log());

        secondDomain.Dispose();

        Assert.AreSame(firstFactory.Logger, new LoggerEnabledType().Log());
    }

    [TestMethod]
    public void WhenDomainsAreDisposedOutOfOrder_RemainingDomainStaysActive()
    {
        var globalFactory = new RecordingLoggerFactory();
        var firstFactory = new RecordingLoggerFactory();
        var secondFactory = new RecordingLoggerFactory();
        LogHost.LoggerFactory = globalFactory;
        var firstDomain = LogHost.RegisterDomain<LoggerEnabledType>(firstFactory);
        var secondDomain = LogHost.RegisterDomain<LoggerEnabledType>(secondFactory);

        firstDomain.Dispose();
        Assert.AreSame(secondFactory.Logger, new LoggerEnabledType().Log());

        secondDomain.Dispose();
        secondDomain.Dispose();

        Assert.AreSame(globalFactory.Logger, new LoggerEnabledType().Log());
    }

    [TestMethod]
    public void WithinDomain_ForSameRuntimeType_ReusesCachedLogger()
    {
        var domainFactory = new RecordingLoggerFactory();
        using var domain = LogHost.RegisterDomain<DomainBase>(domainFactory);

        var firstLogger = new DomainDerived().Log();
        var secondLogger = new DomainDerived().Log();

        Assert.AreSame(firstLogger, secondLogger);
        Assert.AreEqual(1, domainFactory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void WithinDomain_WhenCalledConcurrently_CreatesLoggerOnce()
    {
        var domainFactory = new RecordingLoggerFactory();
        using var domain = LogHost.RegisterDomain<DomainBase>(domainFactory);
        var instances = Enumerable.Range(0, 32).Select(_ => new DomainDerived()).ToArray();

        var loggers = instances.AsParallel().Select(instance => instance.Log()).ToArray();

        Assert.IsTrue(loggers.All(logger => ReferenceEquals(domainFactory.Logger, logger)));
        Assert.AreEqual(1, domainFactory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void LogIfEnabled_WithMatchingDomain_UsesDomainFactory()
    {
        var domainFactory = new RecordingLoggerFactory();
        using var domain = LogHost.RegisterDomain<DomainBase>(domainFactory);

        var log = new DomainDerived().LogIfEnabled(LogLevel.Debug);

        Assert.IsNotNull(log);
        Assert.AreSame(domainFactory.Logger, log.Value.logger);
    }

    [TestMethod]
    public void WithOwnedLoggerAndMatchingDomain_ReturnsLoggerProperty()
    {
        var domainFactory = new RecordingLoggerFactory();
        var ownedLogger = new RecordingLogger();
        using var domain = LogHost.RegisterDomain<IEnableLogger>(domainFactory);

        var logger = new LoggerOwningType(ownedLogger).Log();

        Assert.AreSame(ownedLogger, logger);
        Assert.AreEqual(0, domainFactory.CreateLoggerCallCount);
    }

    [TestMethod]
    public void RegisterDomain_WithNullFactory_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            LogHost.RegisterDomain<LoggerEnabledType>(null!)
        );
    }

    [TestMethod]
    public void WithLogger_ReturnsSpecifiedLogger()
    {
        var instance = new LoggerEnabledType();
        var logger = new RecordingLogger();

        var result = instance.Log(logger);

        Assert.AreSame(logger, result);
    }

    [TestMethod]
    public void WithOwnedLogger_ReturnsLoggerProperty()
    {
        var logger = new RecordingLogger();
        var instance = new LoggerOwningType(logger);

        var result = instance.Log();

        Assert.AreSame(logger, result);
    }

    [TestMethod]
    public void WithOwnedLoggerThroughBaseInterface_ReturnsLoggerProperty()
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
    public void WithNullInstance_ThrowsArgumentNullException()
    {
        IEnableLogger instance = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() => instance.Log());
    }

    [TestMethod]
    public void WithNullLogger_ThrowsArgumentNullException()
    {
        var instance = new LoggerEnabledType();

        Assert.ThrowsExactly<ArgumentNullException>(() => instance.Log(null!));
    }

    [TestMethod]
    public void WithNullOwnedLogger_ThrowsArgumentNullException()
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

    private interface ILoggerDomain;

    private interface ISecondaryLoggerDomain;

    private abstract class DomainBase : IEnableLogger, ILoggerDomain;

    private class DomainMiddle : DomainBase;

    private sealed class DomainDerived : DomainMiddle;

    private sealed class MultiInterfaceLoggerEnabledType
        : IEnableLogger,
            ILoggerDomain,
            ISecondaryLoggerDomain;

    private sealed class LoggerOwningType(ILogger logger) : IEnableLoggerWithLogger
    {
        public ILogger Logger { get; } = logger;
    }

    private sealed class LoggerFactoryServiceProvider(ILoggerFactory loggerFactory)
        : IServiceProvider
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
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) { }
    }
}
