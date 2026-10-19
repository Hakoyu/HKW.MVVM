using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableEventTimeTests
{
    [TestMethod]
    public void FromEventPattern_ForwardsEventsAndUnsubscribesOnDisposal()
    {
        var source = new EventSource();
        var notifications = new List<EventPattern<MessageEventArgs>>();
        using var subscription = ObservableExtensions
            .FromEventPattern<MessageEventArgs>(
                handler => source.Message += handler,
                handler => source.Message -= handler
            )
            .Subscribe(notifications.Add);

        source.RaiseMessage("first");
        subscription.Dispose();
        source.RaiseMessage("second");

        Assert.HasCount(1, notifications);
        Assert.AreSame(source, notifications[0].Sender);
        Assert.AreEqual("first", notifications[0].EventArgs.Message);
        Assert.AreEqual(0, source.MessageHandlerCount);
    }

    [TestMethod]
    public void FromEventPattern_UsesAotSafeExplicitDelegateConversion()
    {
        var source = new EventSource();
        EventPattern<MessageEventArgs>? notification = null;
        using var subscription = ObservableExtensions
            .FromEventPattern<MessageHandler, MessageEventArgs>(
                handler => (sender, eventArgs) => handler(sender, eventArgs),
                handler => source.CustomMessage += handler,
                handler => source.CustomMessage -= handler
            )
            .Subscribe(value => notification = value);

        source.RaiseCustomMessage("converted");

        Assert.IsNotNull(notification);
        Assert.AreSame(source, notification.Sender);
        Assert.AreEqual("converted", notification.EventArgs.Message);
    }

    [TestMethod]
    public void FromEventPattern_RemovesHandlerWhenObserverThrows()
    {
        var source = new EventSource();
        using var subscription = ObservableExtensions
            .FromEventPattern<MessageEventArgs>(
                handler => source.Message += handler,
                handler => source.Message -= handler
            )
            .Subscribe(_ => throw new TestException("observer"));

        Assert.ThrowsExactly<TestException>(() => source.RaiseMessage("failure"));
        Assert.AreEqual(0, source.MessageHandlerCount);
    }

    [TestMethod]
    public void Timer_WithManualTimeProvider_FiresOnceAndCompletes()
    {
        var time = new ManualTimeProvider();
        var values = new List<long>();
        var completed = false;
        using var subscription = ObservableExtensions
            .Timer(TimeSpan.FromSeconds(2), time)
            .Subscribe(values.Add, error => Assert.Fail(error.ToString()), () => completed = true);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.IsEmpty(values);
        Assert.IsFalse(completed);

        time.Advance(TimeSpan.FromSeconds(1));
        CollectionAssert.AreEqual(new long[] { 0 }, values);
        Assert.IsTrue(completed);

        time.Advance(TimeSpan.FromSeconds(10));
        CollectionAssert.AreEqual(new long[] { 0 }, values);
    }

    [TestMethod]
    public void Timer_DisposalFromOnNextSuppressesCompletion()
    {
        var time = new ManualTimeProvider();
        var completed = false;
        IDisposable? subscription = null;
        subscription = ObservableExtensions
            .Timer(TimeSpan.FromSeconds(1), time)
            .Subscribe(
                _ => subscription!.Dispose(),
                error => Assert.Fail(error.ToString()),
                () => completed = true
            );

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.IsFalse(completed);
    }

    [TestMethod]
    public void Timer_WithPeriod_UsesIndependentCountersAndStopsAfterDisposal()
    {
        var time = new ManualTimeProvider();
        var first = new List<long>();
        var second = new List<long>();
        var observable = ObservableExtensions.Timer(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            time
        );
        var firstSubscription = observable.Subscribe(first.Add);
        using var secondSubscription = observable.Subscribe(second.Add);

        time.Advance(TimeSpan.FromSeconds(3));
        firstSubscription.Dispose();
        time.Advance(TimeSpan.FromSeconds(4));

        CollectionAssert.AreEqual(new long[] { 0, 1 }, first);
        CollectionAssert.AreEqual(new long[] { 0, 1, 2, 3 }, second);
    }

    [TestMethod]
    public void AbsoluteTimer_UsesTimeProviderClockAndClampsPastDueTime()
    {
        var start = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new ManualTimeProvider(start);
        var future = new List<long>();
        var past = new List<long>();
        var periodic = new List<long>();
        ObservableExtensions.Timer(start.AddSeconds(5), time).Subscribe(future.Add);
        ObservableExtensions.Timer(start.AddSeconds(-1), time).Subscribe(past.Add);
        using var periodicSubscription = ObservableExtensions
            .Timer(start.AddSeconds(2), TimeSpan.FromSeconds(2), time)
            .Subscribe(periodic.Add);

        time.Advance(TimeSpan.Zero);
        CollectionAssert.AreEqual(new long[] { 0 }, past);
        Assert.IsEmpty(future);

        time.Advance(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new long[] { 0 }, future);
        CollectionAssert.AreEqual(new long[] { 0, 1 }, periodic);
    }

    [TestMethod]
    public void Interval_EmitsAtPeriodAndDisposalCancelsFutureTicks()
    {
        var time = new ManualTimeProvider();
        var values = new List<long>();
        var subscription = ObservableExtensions
            .Interval(TimeSpan.FromMilliseconds(250), time)
            .Subscribe(values.Add);

        time.Advance(TimeSpan.FromSeconds(1));
        subscription.Dispose();
        time.Advance(TimeSpan.FromSeconds(1));

        CollectionAssert.AreEqual(new long[] { 0, 1, 2, 3 }, values);
    }

    [TestMethod]
    public void EventAndTimeFactories_ValidateArguments()
    {
        TimeProvider? nullProvider = null;
        Action<EventHandler<MessageEventArgs>>? nullEventAction = null;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ObservableExtensions.FromEventPattern<MessageEventArgs>(
                nullEventAction!,
                _ => { }
            )
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ObservableExtensions.FromEventPattern<MessageEventArgs>(
                _ => { },
                nullEventAction!
            )
        );
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ObservableExtensions.Timer(TimeSpan.FromMilliseconds(-2))
        );
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ObservableExtensions.Timer(TimeSpan.Zero, TimeSpan.Zero)
        );
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ObservableExtensions.Interval(TimeSpan.Zero)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ObservableExtensions.Interval(TimeSpan.FromSeconds(1), nullProvider!)
        );
    }

    private delegate void MessageHandler(EventSource sender, MessageEventArgs eventArgs);

    private sealed class MessageEventArgs(string message) : EventArgs
    {
        public string Message { get; } = message;
    }

    private sealed class EventSource
    {
        private EventHandler<MessageEventArgs>? _message;

        public event EventHandler<MessageEventArgs> Message
        {
            add
            {
                _message += value;
                MessageHandlerCount++;
            }
            remove
            {
                _message -= value;
                MessageHandlerCount--;
            }
        }

        public event MessageHandler? CustomMessage;

        public int MessageHandlerCount { get; private set; }

        public void RaiseMessage(string message) =>
            _message?.Invoke(this, new MessageEventArgs(message));

        public void RaiseCustomMessage(string message) =>
            CustomMessage?.Invoke(this, new MessageEventArgs(message));
    }
}
