using HKW.MVVM;
using Microsoft.Extensions.Logging;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableLoggingTests
{
    [TestMethod]
    public void Log_ForwardsAndLogsAllNotifications()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();
        var values = new List<int>();
        Exception? receivedError = null;
        using var subscription = source.Log(logger, "Search result").Subscribe(
            values.Add,
            error => receivedError = error);
        var expectedError = new TestException("Search failed.");

        source.Emit(42);
        source.Fail(expectedError);

        CollectionAssert.AreEqual(new[] { 42 }, values);
        Assert.AreSame(expectedError, receivedError);
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level);
        StringAssert.Contains(logger.Entries[0].Message, "Search result: OnNext(42)");
        Assert.AreEqual(LogLevel.Error, logger.Entries[1].Level);
        Assert.AreSame(expectedError, logger.Entries[1].Exception);
        StringAssert.Contains(logger.Entries[1].Message, "Search result: OnError(Search failed.)");
    }

    [TestMethod]
    public void Log_OnCompletion_LogsAndForwardsCompletion()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();
        var completed = false;
        using var subscription = source.Log(logger).Subscribe(
            _ => Assert.Fail(),
            _ => Assert.Fail(),
            () => completed = true);

        source.Complete();

        Assert.IsTrue(completed);
        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level);
        Assert.AreEqual("Observable: OnCompleted()", logger.Entries[0].Message);
    }

    [TestMethod]
    public void Log_WithLogLevel_UsesSpecifiedLevelForAllNotifications()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();
        Exception? receivedError = null;
        using var subscription = source.Log(logger, LogLevel.Warning, "Custom level").Subscribe(
            _ => { },
            error => receivedError = error);
        var expectedError = new TestException("Expected.");

        source.Emit(42);
        source.Fail(expectedError);

        Assert.AreSame(expectedError, receivedError);
        Assert.HasCount(2, logger.Entries);
        Assert.IsTrue(logger.Entries.All(entry => entry.Level == LogLevel.Warning));
        Assert.AreSame(expectedError, logger.Entries[1].Exception);
    }

    [TestMethod]
    public void Log_IsColdUntilSubscribed()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();

        _ = source.Log(logger, "Cold");
        source.Emit(1);

        Assert.AreEqual(0, source.SubscriptionCount);
        Assert.IsEmpty(logger.Entries);
    }

    [TestMethod]
    public void Log_FromWhenAnyValue_UsesConfiguredFactory()
    {
        var originalFactory = LogHost.LoggerFactory;
        var logger = new RecordingLogger();
        var factory = new SingleLoggerFactory(logger);
        try
        {
            LogHost.LoggerFactory = factory;
            var owner = new LoggerOwner();
            using var subscription = owner
                .WhenAnyValue(x => x.Value)
                .Log("Owned")
                .Subscribe(_ => { });

            owner.Value = 1;

            Assert.AreEqual(typeof(LoggerOwner).FullName, factory.CategoryName);
            Assert.HasCount(2, logger.Entries);
        }
        finally
        {
            LogHost.LoggerFactory = originalFactory;
        }
    }

    [TestMethod]
    public void Log_FromWhenAnyValueWithOwnedLogger_UsesSpecifiedLevel()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        using var subscription = owner
            .WhenAnyValue(x => x.Value)
            .Log(LogLevel.Trace)
            .Subscribe(_ => { });

        owner.Value = 1;

        Assert.HasCount(2, logger.Entries);
        Assert.IsTrue(logger.Entries.All(entry => entry.Level == LogLevel.Trace));
    }

    [TestMethod]
    public void Log_FromCombinedWhenAnyValue_PreservesLogger()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        using var subscription = owner
            .WhenAnyValue(x => x.Value, x => x.OtherValue)
            .Log()
            .Subscribe(_ => { });

        owner.OtherValue = 1;

        Assert.HasCount(2, logger.Entries);
    }

    [TestMethod]
    public void Log_WithoutAutomaticLogger_Throws()
    {
        var source = new ManualObservable<int>();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => source.Log());

        StringAssert.Contains(exception.Message, "Pass an ILogger explicitly");
    }

    [TestMethod]
    public void Log_ValidatesArguments()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ObservableExtensions.Log<int>(null!, logger));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.Log((ILogger)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ObservableExtensions.Log<int>(null!));
    }

    private sealed class LoggerOwner : ObservableObjectEx, IEnableLogger
    {
        public int Value
        {
            get => field;
            set => SetProperty(ref field, value);
        }
    }

    private sealed class LoggerOwnerWithLogger(ILogger logger)
        : ObservableObjectEx, IEnableLoggerWithLogger
    {
        public ILogger Logger { get; } = logger;

        public int Value
        {
            get => field;
            set => SetProperty(ref field, value);
        }

        public int OtherValue
        {
            get => field;
            set => SetProperty(ref field, value);
        }
    }

    private sealed class SingleLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public string? CategoryName { get; private set; }

        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName)
        {
            CategoryName = categoryName;
            return logger;
        }

        public void Dispose() { }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}