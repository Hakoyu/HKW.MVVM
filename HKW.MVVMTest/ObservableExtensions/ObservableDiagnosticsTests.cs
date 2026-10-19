using HKW.MVVM;
using Microsoft.Extensions.Logging;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableDiagnosticsTests
{
    [TestMethod]
    public void Do_InvokesAllLifecycleHooksOnce()
    {
        var source = new ManualObservable<int>();
        var events = new List<string>();
        var observable = source.Do(
            value => events.Add($"next:{value}"),
            error => events.Add($"error:{error.Message}"),
            () => events.Add("completed"),
            () => events.Add("subscribed"),
            () => events.Add("disposed")
        );
        using var subscription = observable.Subscribe(_ => { });

        source.Emit(1);
        source.Complete();
        subscription.Dispose();

        CollectionAssert.AreEqual(
            new[] { "subscribed", "next:1", "completed", "disposed" },
            events
        );
    }

    [TestMethod]
    public void Do_ErrorHookRunsBeforeForwardingError()
    {
        var source = new ManualObservable<int>();
        var events = new List<string>();
        using var subscription = source
            .Do(
                onNext: null,
                onError: error => events.Add($"hook:{error.Message}"),
                onCompleted: null,
                onDispose: () => events.Add("disposed")
            )
            .Subscribe(_ => { }, error => events.Add($"observer:{error.Message}"));

        source.Fail(new TestException("failed"));

        CollectionAssert.AreEqual(
            new[] { "hook:failed", "observer:failed", "disposed" },
            events
        );
    }

    [TestMethod]
    public void Trace_DelegatesToLogAtTraceLevel()
    {
        var source = new ManualObservable<int>();
        var logger = new DiagnosticLogger();
        using var subscription = source.Trace(logger, "diagnostic").Subscribe(_ => { });

        source.Emit(42);

        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(LogLevel.Trace, logger.Entries[0].Level);
        StringAssert.Contains(logger.Entries[0].Message, "diagnostic");
    }

    [TestMethod]
    public void ToProperty_OptionsRetainDeferredDistinctScheduledNotifications()
    {
        var source = new ManualObservable<string>();
        var owner = new TrackingPropertyOwner();
        var context = new QueuedSynchronizationContext();
        using var helper = source.ToProperty(
            owner,
            value => value.Result,
            "initial",
            new ObservableAsPropertyOptions<string>
            {
                DeferSubscription = true,
                Comparer = StringComparer.OrdinalIgnoreCase,
                Scheduler = ObservableSchedulers.FromSynchronizationContext(context),
            }
        );
        var notifications = 0;
        helper.PropertyChanged += (_, _) => notifications++;

        Assert.IsFalse(helper.IsSubscribed);
        Assert.AreEqual("initial", helper.Value);
        source.Emit("VALUE");
        source.Emit("value");
        Assert.AreEqual("initial", helper.Value);

        context.RunAll();

        Assert.AreEqual("VALUE", helper.Value);
        Assert.AreEqual(1, notifications);
        Assert.AreEqual(1, owner.ChangingNotificationCount);
        Assert.AreEqual(1, owner.ChangedNotificationCount);
    }

    private sealed class DiagnosticLogger : ILogger
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
        ) => Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
