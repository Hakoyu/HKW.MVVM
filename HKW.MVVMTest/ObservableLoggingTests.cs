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
    public void Log_WithValueMessageFactory_UsesFactoryForValues()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        using var subscription = owner
            .WhenAnyValue(x => x.Value)
            .Log(value => $"Value changed to {value}")
            .Subscribe(_ => { });

        owner.Value = 42;

        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual("Value changed to 0", logger.Entries[0].Message);
        Assert.AreEqual("Value changed to 42", logger.Entries[1].Message);
    }

    [TestMethod]
    public void Log_WithValueMessageFactoryAndLevel_UsesSpecifiedLevel()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        using var subscription = owner
            .WhenAnyValue(x => x.Value)
            .Take(1)
            .Log((int value) => $"Value: {value}", LogLevel.Warning)
            .Subscribe(_ => { });

        Assert.HasCount(2, logger.Entries);
        Assert.IsTrue(logger.Entries.All(entry => entry.Level == LogLevel.Warning));
        Assert.AreEqual("Value: 0", logger.Entries[0].Message);
        Assert.AreEqual("Observable: OnCompleted()", logger.Entries[1].Message);
    }

    [TestMethod]
    public void Log_WithNotificationMessageFactory_ReceivesEveryNotification()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        var notifications = new List<ObservableLogNotification<int>>();
        string CreateMessage(ObservableLogNotification<int> notification)
        {
            notifications.Add(notification);
            return notification.Action.ToString();
        }

        using var completedSubscription = owner
            .WhenAnyValue(x => x.Value)
            .Take(1)
            .LogNotifications(CreateMessage)
            .Subscribe(_ => { });
        var expectedError = new TestException("Expected.");
        using var errorSubscription = owner
            .WhenAnyValue(x => x.Value)
            .Select<int, int>(_ => throw expectedError)
            .LogNotifications(CreateMessage)
            .Subscribe(_ => { }, _ => { });

        Assert.HasCount(3, notifications);
        Assert.AreEqual(ObservableLogAction.OnNext, notifications[0].Action);
        Assert.AreEqual(0, notifications[0].Value);
        Assert.AreEqual(ObservableLogAction.OnCompleted, notifications[1].Action);
        Assert.AreEqual(ObservableLogAction.OnError, notifications[2].Action);
        Assert.AreSame(expectedError, notifications[2].Exception);
        CollectionAssert.AreEqual(
            new[] { "OnNext", "OnCompleted", "OnError" },
            logger.Entries.Select(entry => entry.Message).ToArray()
        );
        Assert.AreEqual(LogLevel.Error, logger.Entries[2].Level);
        Assert.AreSame(expectedError, logger.Entries[2].Exception);
    }

    [TestMethod]
    public void Log_MessageFactoryFailureTerminatesOnlyOnce()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        var expectedError = new TestException("Factory failed.");
        var factoryCalls = 0;
        var errors = new List<Exception>();
        using var subscription = owner
            .WhenAnyValue(x => x.Value)
            .Log((int _) =>
            {
                factoryCalls++;
                throw expectedError;
            })
            .Subscribe(_ => Assert.Fail(), errors.Add);

        owner.Value = 1;

        Assert.AreEqual(1, factoryCalls);
        Assert.HasCount(1, errors);
        Assert.AreSame(expectedError, errors[0]);
        Assert.IsEmpty(logger.Entries);
    }

    [TestMethod]
    public void Log_NotificationFactoryFailureReplacesSourceError()
    {
        var logger = new RecordingLogger();
        var owner = new LoggerOwnerWithLogger(logger);
        var sourceError = new TestException("Source failed.");
        var factoryError = new TestException("Factory failed.");
        Exception? receivedError = null;
        using var subscription = owner
            .WhenAnyValue(x => x.Value)
            .Select<int, int>(_ => throw sourceError)
            .LogNotifications(notification =>
                notification.Action == ObservableLogAction.OnError
                    ? throw factoryError
                    : notification.Action.ToString()
            )
            .Subscribe(_ => Assert.Fail(), error => receivedError = error);

        Assert.AreSame(factoryError, receivedError);
        Assert.IsEmpty(logger.Entries);
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
            Assert.AreEqual(1, factory.CreateCount);
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
    public void Log_FromWhenAny_PreservesLoggerThroughSelect()
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
    public void Log_ProjectOperatorsPreserveLogger()
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
    public void Log_WhenAnyValueResolvesLoggerLazilyAndOnlyOnce()
    {
        var owner = new CountingLoggerOwner(new RecordingLogger());
        var source = owner
            .WhenAnyValue(x => x.Value)
            .Where(_ => true)
            .Select(value => value);

        Assert.AreEqual(0, owner.LoggerAccessCount);

        _ = source.Log();
        _ = source.Log();

        Assert.AreEqual(1, owner.LoggerAccessCount);
    }

    [TestMethod]
    public void Log_CatchUsesOriginalSourceLogger()
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
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new LoggerOwnerWithLogger(logger)
                .WhenAnyValue(x => x.Value)
                .Log((Func<int, string>)null!)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new LoggerOwnerWithLogger(logger)
                .WhenAnyValue(x => x.Value)
                .LogNotifications(null!)
        );
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

    private sealed class CountingLoggerOwner(ILogger logger)
        : ObservableObjectEx, IEnableLoggerWithLogger
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