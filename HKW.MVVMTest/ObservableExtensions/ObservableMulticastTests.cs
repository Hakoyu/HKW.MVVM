using CommunityToolkit.Mvvm.ComponentModel;
using HKW.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableMulticastTests
{
    [TestMethod]
    public void Publish_IsCovariantColdAndSharesOneConnection()
    {
        var source = new ManualObservable<string>();
        IConnectableObservable<string> published = source.Publish();
        IConnectableObservable<object> covariant = published;
        var first = new List<object>();
        var second = new List<object>();
        using var firstSubscription = covariant.Subscribe(first.Add);
        using var secondSubscription = covariant.Subscribe(second.Add);

        source.Emit("before");
        Assert.AreEqual(0, source.SubscriptionCount);

        using var connection = published.Connect();
        source.Emit("value");

        CollectionAssert.AreEqual(new object[] { "value" }, first);
        CollectionAssert.AreEqual(new object[] { "value" }, second);
        Assert.AreEqual(1, source.SubscriptionCount);
        Assert.AreSame(connection, published.Connect());
    }

    [TestMethod]
    public void Publish_ConcurrentConnectCreatesOneSourceSubscription()
    {
        var source = new ManualObservable<int>();
        var published = source.Publish();
        var connections = new IDisposable[32];

        Parallel.For(0, connections.Length, index => connections[index] = published.Connect());

        Assert.AreEqual(1, source.SubscriptionCount);
        Assert.IsTrue(connections.All(connection => ReferenceEquals(connections[0], connection)));
        connections[0].Dispose();
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Publish_DisconnectsAndReconnectsExistingSubscribers()
    {
        var source = new ManualObservable<int>();
        var published = source.Publish();
        var values = new List<int>();
        using var subscription = published.Subscribe(values.Add);

        var firstConnection = published.Connect();
        source.Emit(1);
        firstConnection.Dispose();
        source.Emit(2);
        using var secondConnection = published.Connect();
        source.Emit(3);

        CollectionAssert.AreEqual(new[] { 1, 3 }, values);
        Assert.AreEqual(2, source.SubscriptionCount);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void ReplayOne_ReplaysLatestValueAndTerminalNotification()
    {
        var source = new ManualObservable<int>();
        var replayed = source.Replay(1);
        using var connection = replayed.Connect();
        source.Emit(1);
        source.Emit(2);
        source.Complete();
        var values = new List<int>();
        var completed = false;

        using var subscription = replayed.Subscribe(
            values.Add,
            _ => Assert.Fail(),
            () => completed = true
        );

        CollectionAssert.AreEqual(new[] { 2 }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
        _ = replayed.Connect();
        Assert.AreEqual(1, source.SubscriptionCount);
    }

    [TestMethod]
    public void Replay_UsesBoundedBufferAndPreservesError()
    {
        var source = new ManualObservable<int>();
        var replayed = source.Replay(2);
        using var connection = replayed.Connect();
        source.Emit(1);
        source.Emit(2);
        source.Emit(3);
        var expected = new InvalidOperationException("failed");
        source.Fail(expected);
        var values = new List<int>();
        Exception? error = null;

        using var subscription = replayed.Subscribe(values.Add, exception => error = exception);

        CollectionAssert.AreEqual(new[] { 2, 3 }, values);
        Assert.AreSame(expected, error);
    }

    [TestMethod]
    public void RefCount_ConnectsFirstDisconnectsLastAndReconnects()
    {
        var source = new ManualObservable<int>();
        var shared = source.Publish().RefCount();
        var firstValues = new List<int>();
        var secondValues = new List<int>();

        var first = shared.Subscribe(firstValues.Add);
        var second = shared.Subscribe(secondValues.Add);
        Assert.AreEqual(1, source.SubscriptionCount);

        first.Dispose();
        Assert.AreEqual(0, source.DisposalCount);
        second.Dispose();
        Assert.AreEqual(1, source.DisposalCount);

        using var third = shared.Subscribe(firstValues.Add);
        source.Emit(7);

        Assert.AreEqual(2, source.SubscriptionCount);
        CollectionAssert.AreEqual(new[] { 7 }, firstValues);
    }

    [TestMethod]
    public void ObserverCallbacksCanReenterWithoutDeadlock()
    {
        var source = new ManualObservable<int>();
        var published = source.Publish();
        IDisposable? subscription = null;
        subscription = published.Subscribe(_ => subscription!.Dispose());
        using var connection = published.Connect();

        var task = Task.Run(() => source.Emit(1));

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void MulticastOperatorsPreserveLogger()
    {
        var owner = new LoggerOwner();
        var published = owner.WhenAnyValue(x => x.Value).Publish();

        _ = published.Log();
        _ = published.RefCount().Log();
    }

    [TestMethod]
    public void ReplayRejectsNegativeBufferSize()
    {
        var source = new ManualObservable<int>();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Replay(-1));
    }

    private sealed class LoggerOwner : ObservableObject, IEnableLoggerWithLogger
    {
        private int _value;

        public ILogger Logger => NullLogger.Instance;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
