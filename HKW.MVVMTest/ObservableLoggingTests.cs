using HKW.MVVM;
using Microsoft.Extensions.Logging;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableLoggingTests
{
    [TestMethod]
    public void ForwardsAndLogsAllNotifications()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();
        var values = new List<int>();
        Exception? receivedError = null;
        using var subscription = source
            .Log(logger, "Search result")
            .Subscribe(values.Add, error => receivedError = error);
        var expectedError = new TestException("Search failed.");

        source.Emit(42);
        source.Fail(expectedError);

        CollectionAssert.AreEqual(new[] { 42 }, values);
        Assert.AreSame(expectedError, receivedError);
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level);
        StringAssert.Contains(logger.Entries[0].Message, "OnNext(42): Search result");
        Assert.AreEqual(LogLevel.Error, logger.Entries[1].Level);
        Assert.AreSame(expectedError, logger.Entries[1].Exception);
        StringAssert.Contains(logger.Entries[1].Message, "OnError(Search failed.): Search result");
    }

    [TestMethod]
    public void OnCompletion_LogsAndForwardsCompletion()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();
        var completed = false;
        using var subscription = source
            .Log(logger)
            .Subscribe(_ => Assert.Fail(), _ => Assert.Fail(), () => completed = true);

        source.Complete();

        Assert.IsTrue(completed);
        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level);
        Assert.AreEqual("Observable: OnCompleted()", logger.Entries[0].Message);
    }

    [TestMethod]
    public void WithLogLevel_UsesSpecifiedLevelForAllNotifications()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();
        Exception? receivedError = null;
        using var subscription = source
            .Log(logger, LogLevel.Warning, "Custom level")
            .Subscribe(_ => { }, error => receivedError = error);
        var expectedError = new TestException("Expected.");

        source.Emit(42);
        source.Fail(expectedError);

        Assert.AreSame(expectedError, receivedError);
        Assert.HasCount(2, logger.Entries);
        Assert.IsTrue(logger.Entries.All(entry => entry.Level == LogLevel.Warning));

        Assert.AreSame(expectedError, logger.Entries[1].Exception);
    }

    [TestMethod]
    public void IsColdUntilSubscribed()
    {
        var source = new ManualObservable<int>();
        var logger = new RecordingLogger();

        _ = source.Log(logger, "Cold");
        source.Emit(1);

        Assert.AreEqual(0, source.SubscriptionCount);
        Assert.IsEmpty(logger.Entries);
    }

    [TestMethod]
    public void FromWhenAnyValue_UsesConfiguredFactory()
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
            Assert.AreEqual(1, factory.CreateCount);
            Assert.HasCount(2, logger.Entries);
        }
        finally
        {
            LogHost.LoggerFactory = originalFactory;
        }
    }

    [TestMethod]
    public void FromWhenAnyValueWithOwnedLogger_UsesSpecifiedLevel()
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
    public void FromCombinedWhenAnyValue_PreservesLogger()
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
    public void FromWhenAny_PreservesLoggerThroughSelect()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        using var subscription = owner
            .WhenAny(x => x.Value, observation => observation.Value)
            .Log()
            .Subscribe(_ => { });

        owner.Value = 1;

        Assert.HasCount(2, logger.Entries);
    }

    [TestMethod]
    public void ProjectOperatorsPreserveLogger()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        var source = owner.WhenAnyValue(x => x.Value);
        var context = new SynchronizationContext();
        IObservable<int>[] results =
        [
            source.Select(value => value),
            source.Where(_ => true),
            source.DistinctUntilChanged(),
            source.StartWith(0),
            source.Skip(1),
            source.Take(1),
            source.Do(_ => { }),
            source.ObserveOn(context),
            source.ObserveOn(ObservableSchedulers.ThreadPool),
            source.SubscribeOn(context),
            source.SubscribeOn(ObservableSchedulers.ThreadPool),
            source.Throttle(TimeSpan.Zero),
            source.Throttle(TimeSpan.Zero, TimeProvider.System),
            source.Throttle(TimeSpan.Zero, ObservableSchedulers.ThreadPool),
            source.Catch(_ => ObservableExtensions.Empty<int>()),
            source.Log(),
        ];

        foreach (var result in results)
        {
            _ = result.Log();
        }
    }

    [TestMethod]
    public void WhenAnyValueResolvesLoggerLazilyAndOnlyOnce()
    {
        var owner = new CountingLoggerOwner(new RecordingLogger());
        var source = owner.WhenAnyValue(x => x.Value).Where(_ => true).Select(value => value);

        Assert.AreEqual(0, owner.LoggerAccessCount);

        _ = source.Log();
        _ = source.Log();

        Assert.AreEqual(1, owner.LoggerAccessCount);
    }

    [TestMethod]
    public void CatchUsesOriginalSourceLogger()
    {
        var sourceLogger = new RecordingLogger();
        var replacementLogger = new RecordingLogger();
        var sourceOwner = new LoggerOwnerWithLogger(sourceLogger);
        var replacementOwner = new LoggerOwnerWithLogger(replacementLogger);
        var observable = sourceOwner
            .WhenAnyValue(x => x.Value)
            .Catch(_ => replacementOwner.WhenAnyValue(x => x.Value));

        using var subscription = observable.Log().Take(1).Subscribe(_ => { });

        Assert.HasCount(1, sourceLogger.Entries);
        Assert.IsEmpty(replacementLogger.Entries);
    }

    [TestMethod]
    public void WithoutAutomaticLogger_Throws()
    {
        var source = new ManualObservable<int>();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => source.Log());

        StringAssert.Contains(exception.Message, "Pass an ILogger explicitly");
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
        : ObservableObjectEx,
            IEnableLoggerWithLogger
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

    private sealed class CountingLoggerOwner(ILogger logger)
        : ObservableObjectEx,
            IEnableLoggerWithLogger
    {
        public int LoggerAccessCount { get; private set; }

        public ILogger Logger
        {
            get
            {
                LoggerAccessCount++;
                return logger;
            }
        }

        public int Value
        {
            get => field;
            set => SetProperty(ref field, value);
        }
    }

    private sealed class SingleLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public string? CategoryName { get; private set; }

        public int CreateCount { get; private set; }

        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName)
        {
            CategoryName = categoryName;
            CreateCount++;
            return logger;
        }

        public void Dispose() { }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
