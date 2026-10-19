using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableTimeExtensionsTests
{
    [TestMethod]
    public void Delay_DelaysEveryNotificationAndPreservesOrder()
    {
        var time = new ManualTimeProvider();
        var source = new ManualObservable<int>();
        var notifications = new List<string>();
        using var subscription = source.Delay(TimeSpan.FromSeconds(1), time).Subscribe(
            value => notifications.Add($"N:{value}"),
            error => notifications.Add($"E:{error.Message}"),
            () => notifications.Add("C")
        );

        source.Emit(1);
        time.Advance(TimeSpan.FromMilliseconds(500));
        source.Emit(2);
        source.Complete();

        time.Advance(TimeSpan.FromMilliseconds(500));
        CollectionAssert.AreEqual(new[] { "N:1" }, notifications);

        time.Advance(TimeSpan.FromMilliseconds(500));
        CollectionAssert.AreEqual(new[] { "N:1", "N:2", "C" }, notifications);
    }

    [TestMethod]
    public void Delay_DelaysErrorsAndSuppressesNotificationsAfterTerminal()
    {
        var time = new ManualTimeProvider();
        var source = new ManualObservable<int>();
        var error = new InvalidOperationException("failure");
        var notifications = new List<object>();
        source.Delay(TimeSpan.FromSeconds(1), time).Subscribe(
            value => notifications.Add(value),
            exception => notifications.Add(exception),
            () => notifications.Add("completed")
        );

        source.Emit(1);
        source.Fail(error);
        source.Emit(2);
        source.Complete();
        Assert.IsEmpty(notifications);

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.HasCount(2, notifications);
        Assert.AreEqual(1, notifications[0]);
        Assert.AreSame(error, notifications[1]);
    }

    [TestMethod]
    public void Delay_DisposesUpstreamImmediatelyWhenCompletionIsQueued()
    {
        var time = new ManualTimeProvider();
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnCompleted();
        });
        var notifications = new List<string>();
        source.Delay(TimeSpan.FromSeconds(1), time).Subscribe(
            value => notifications.Add($"N:{value}"),
            error => Assert.Fail(error.ToString()),
            () => notifications.Add("C")
        );

        Assert.AreEqual(1, source.DisposalCount);
        Assert.IsEmpty(notifications);

        time.Advance(TimeSpan.FromSeconds(1));
        CollectionAssert.AreEqual(new[] { "N:1", "C" }, notifications);
    }

    [TestMethod]
    public void Delay_DisposesUpstreamImmediatelyWhenErrorIsQueued()
    {
        var time = new ManualTimeProvider();
        var error = new InvalidOperationException("failure");
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnError(error);
        });
        var notifications = new List<object>();
        source.Delay(TimeSpan.FromSeconds(1), time).Subscribe(
            value => notifications.Add(value),
            exception => notifications.Add(exception)
        );

        Assert.AreEqual(1, source.DisposalCount);
        Assert.IsEmpty(notifications);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.HasCount(2, notifications);
        Assert.AreEqual(1, notifications[0]);
        Assert.AreSame(error, notifications[1]);
    }

    [TestMethod]
    public void Timeout_ResetsAfterEveryValueAndEventuallyFails()
    {
        var time = new ManualTimeProvider();
        var source = new ManualObservable<int>();
        var values = new List<int>();
        Exception? failure = null;
        source.Timeout(TimeSpan.FromSeconds(1), time).Subscribe(values.Add, error => failure = error);

        time.Advance(TimeSpan.FromMilliseconds(900));
        source.Emit(1);
        time.Advance(TimeSpan.FromMilliseconds(900));

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { 1 }, values);

        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.IsInstanceOfType<TimeoutException>(failure);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Timeout_AtomicallySwitchesToFallback()
    {
        var time = new ManualTimeProvider();
        var source = new ManualObservable<int>();
        var fallback = new ManualObservable<int>();
        var values = new List<int>();
        var completed = false;
        source.Timeout(TimeSpan.FromSeconds(1), fallback, time).Subscribe(
            values.Add,
            error => Assert.Fail(error.ToString()),
            () => completed = true
        );

        source.Emit(1);
        time.Advance(TimeSpan.FromSeconds(1));
        source.Emit(2);
        fallback.Emit(3);
        fallback.Complete();

        CollectionAssert.AreEqual(new[] { 1, 3 }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.AreEqual(1, fallback.DisposalCount);
    }

    [TestMethod]
    public void Sample_EmitsLatestValueOnTicksAndCompletesOnTickAfterSourceCompletion()
    {
        var time = new ManualTimeProvider();
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var completed = false;
        source.Sample(TimeSpan.FromSeconds(1), time).Subscribe(
            values.Add,
            error => Assert.Fail(error.ToString()),
            () => completed = true
        );

        source.Emit(1);
        source.Emit(2);
        time.Advance(TimeSpan.FromSeconds(1));
        CollectionAssert.AreEqual(new[] { 2 }, values);

        source.Emit(3);
        source.Complete();
        Assert.IsFalse(completed);

        time.Advance(TimeSpan.FromSeconds(1));
        CollectionAssert.AreEqual(new[] { 2, 3 }, values);
        Assert.IsTrue(completed);

        time.Advance(TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(new[] { 2, 3 }, values);
    }

    [TestMethod]
    public void Sample_ForwardsErrorsImmediatelyAndCancelsTicks()
    {
        var time = new ManualTimeProvider();
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var error = new InvalidOperationException("failure");
        Exception? observedError = null;
        source.Sample(TimeSpan.FromSeconds(1), time).Subscribe(values.Add, value => observedError = value);

        source.Emit(1);
        source.Fail(error);
        time.Advance(TimeSpan.FromSeconds(2));

        Assert.IsEmpty(values);
        Assert.AreSame(error, observedError);
    }

    [TestMethod]
    public void TimeOperators_ValidateSourcesProvidersFallbacksAndPositiveDurations()
    {
        var source = new ManualObservable<int>();
        var fallback = new ManualObservable<int>();
        var positive = TimeSpan.FromSeconds(1);
        IObservable<int>? nullSource = null;
        TimeProvider? nullProvider = null;
        IObservable<int>? nullFallback = null;

        Assert.ThrowsExactly<ArgumentNullException>(() => nullSource!.Delay(positive));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Delay(positive, nullProvider!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Delay(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => nullSource!.Timeout(positive));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Timeout(positive, nullProvider!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Timeout(positive, nullFallback!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Timeout(positive, fallback, nullProvider!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Timeout(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => nullSource!.Sample(positive));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Sample(positive, nullProvider!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Sample(TimeSpan.Zero));
    }

    [TestMethod]
    public void TimeOperators_HandleSynchronousTerminationAndDisposal()
    {
        var time = new ManualTimeProvider();
        var synchronous = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnCompleted();
        });
        var delayed = new List<int>();
        var completed = false;
        synchronous.Delay(TimeSpan.FromSeconds(1), time).Subscribe(
            delayed.Add,
            error => Assert.Fail(error.ToString()),
            () => completed = true
        );

        time.Advance(TimeSpan.FromSeconds(1));
        CollectionAssert.AreEqual(new[] { 1 }, delayed);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, synchronous.DisposalCount);

        var source = new ManualObservable<int>();
        var sampled = new List<int>();
        var subscription = source.Sample(TimeSpan.FromSeconds(1), time).Subscribe(sampled.Add);
        source.Emit(2);
        subscription.Dispose();
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.IsEmpty(sampled);
    }
}
