using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableFoundationTests
{
    [TestMethod]
    public void SerialDisposable_ReplacesAndDisposesCurrentResource()
    {
        var serial = new SerialDisposable();
        var first = new TrackingDisposable();
        var second = new TrackingDisposable();

        serial.Disposable = first;
        serial.Disposable = second;
        serial.Dispose();
        serial.Dispose();

        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
        Assert.IsTrue(serial.IsDisposed);
    }

    [TestMethod]
    public void SerialDisposable_AssignmentAfterDisposalIsDisposedImmediately()
    {
        var serial = new SerialDisposable();
        serial.Dispose();
        var resource = new TrackingDisposable();

        serial.Disposable = resource;

        Assert.AreEqual(1, resource.DisposeCount);
        Assert.IsNull(serial.Disposable);
    }

    [TestMethod]
    public void SerialDisposable_ConcurrentReplacementAndDisposalDisposesEveryResourceOnce()
    {
        var serial = new SerialDisposable();
        var resources = Enumerable.Range(0, 100).Select(_ => new TrackingDisposable()).ToArray();
        var racingResource = new TrackingDisposable();

        Parallel.ForEach(resources, resource => serial.Disposable = resource);
        Parallel.Invoke(serial.Dispose, () => serial.Disposable = racingResource);

        Assert.IsTrue(resources.All(resource => resource.DisposeCount == 1));
        Assert.AreEqual(1, racingResource.DisposeCount);
        Assert.IsTrue(serial.IsDisposed);
    }

    [TestMethod]
    public void SerialAndSingleAssignmentDisposables_HandleSynchronousReplacement()
    {
        var serial = new SerialDisposable();
        var firstSlot = new SingleAssignmentDisposable();
        var nextSlot = new SingleAssignmentDisposable();
        var firstSubscription = new TrackingDisposable();
        var nextSubscription = new TrackingDisposable();

        serial.Disposable = firstSlot;
        serial.Disposable = nextSlot;
        firstSlot.Disposable = firstSubscription;
        nextSlot.Disposable = nextSubscription;

        Assert.AreEqual(1, firstSubscription.DisposeCount);
        Assert.AreEqual(0, nextSubscription.DisposeCount);

        serial.Dispose();
        Assert.AreEqual(1, nextSubscription.DisposeCount);
    }

    [TestMethod]
    public void ManualTimeProvider_AdvancesTimersDeterministically()
    {
        var time = new ManualTimeProvider();
        var callbacks = new List<string>();
        using var later = time.CreateTimer(_ => callbacks.Add("later"), null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        using var earlier = time.CreateTimer(_ => callbacks.Add("earlier"), null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.IsEmpty(callbacks);

        time.Advance(TimeSpan.FromMilliseconds(1001));

        CollectionAssert.AreEqual(new[] { "earlier", "later" }, callbacks);
        Assert.AreEqual(DateTimeOffset.UnixEpoch.AddSeconds(2), time.GetUtcNow());
        Assert.AreEqual(TimeSpan.FromSeconds(2).Ticks, time.GetTimestamp());
    }

    [TestMethod]
    public void ManualTimeProvider_SupportsPeriodicChangeAndDisposal()
    {
        var time = new ManualTimeProvider();
        var callbackCount = 0;
        var timer = time.CreateTimer(_ => callbackCount++, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        time.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(3, callbackCount);

        Assert.IsTrue(timer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan));
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.AreEqual(4, callbackCount);

        timer.Dispose();
        Assert.IsFalse(timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan));
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(4, callbackCount);
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
