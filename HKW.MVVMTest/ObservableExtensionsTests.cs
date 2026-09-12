using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableExtensionsTests
{
    private sealed class ThrowingComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => throw new InvalidOperationException("Comparer failed.");

        public int GetHashCode(int obj) => obj;
    }

    [TestMethod]
    public void WhereAndSelect_FilterAndTransformValues()
    {
        var source = new ManualObservable<int>();
        var values = new List<string>();
        using var subscription = source
            .Where(value => value % 2 == 0)
            .Select(value => $"Value:{value}")
            .Subscribe(values.Add);

        source.Emit(1);
        source.Emit(2);
        source.Emit(4);

        CollectionAssert.AreEqual(new[] { "Value:2", "Value:4" }, values);
    }

    [TestMethod]
    public void Select_WhenSelectorThrows_ForwardsErrorAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        Exception? received = null;
        using var subscription = source
            .Select<int, int>(_ => throw new TestException("Selector failed."))
            .Subscribe(_ => Assert.Fail(), error => received = error);

        source.Emit(1);

        Assert.IsInstanceOfType<TestException>(received);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Where_WhenPredicateThrows_ForwardsErrorAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        Exception? received = null;
        using var subscription = source
            .Where(_ => throw new TestException("Predicate failed."))
            .Subscribe(_ => Assert.Fail(), error => received = error);

        source.Emit(1);

        Assert.IsInstanceOfType<TestException>(received);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void DistinctUntilChanged_UsesDefaultComparer()
    {
        var source = new ManualObservable<string>();
        var values = new List<string>();
        using var subscription = source.DistinctUntilChanged().Subscribe(values.Add);

        source.Emit("A");
        source.Emit("A");
        source.Emit("B");
        source.Emit("B");
        source.Emit("A");

        CollectionAssert.AreEqual(new[] { "A", "B", "A" }, values);
    }

    [TestMethod]
    public void DistinctUntilChanged_UsesCustomComparer()
    {
        var source = new ManualObservable<string>();
        var values = new List<string>();
        using var subscription = source
            .DistinctUntilChanged(StringComparer.OrdinalIgnoreCase)
            .Subscribe(values.Add);

        source.Emit("A");
        source.Emit("a");
        source.Emit("B");

        CollectionAssert.AreEqual(new[] { "A", "B" }, values);
    }

    [TestMethod]
    public void DistinctUntilChanged_WhenComparerThrows_TerminatesAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        var received = 0;
        Exception? error = null;
        using var subscription = source
            .DistinctUntilChanged(new ThrowingComparer())
            .Subscribe(_ => received++, exception => error = exception);

        source.Emit(1);
        source.Emit(2);
        source.Emit(3);

        Assert.AreEqual(1, received);
        Assert.IsInstanceOfType<InvalidOperationException>(error);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void CoreOperatorPipeline_WhenSourceCompletesSynchronously_DisposesSourceSubscription()
    {
        var values = new List<int>();
        var completed = false;
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(2);
            observer.OnNext(2);
            observer.OnCompleted();
        });

        using var subscription = source
            .Where(value => value % 2 == 0)
            .Select(value => value * 2)
            .DistinctUntilChanged()
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        CollectionAssert.AreEqual(new[] { 4 }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Select_WhenSelectorThrowsSynchronously_DisposesSourceSubscription()
    {
        Exception? received = null;
        var source = new SynchronousObservable<int>(observer => observer.OnNext(1));

        using var subscription = source
            .Select<int, int>(_ => throw new TestException("Selector failed."))
            .Subscribe(_ => Assert.Fail(), error => received = error);

        Assert.IsInstanceOfType<TestException>(received);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void StartWith_EmitsInitialValueBeforeSourceValues()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        using var subscription = source.StartWith(0).Subscribe(values.Add);

        source.Emit(1);

        CollectionAssert.AreEqual(new[] { 0, 1 }, values);
    }

    [TestMethod]
    public void SkipAndTake_SelectExpectedValuesAndDisposeEarly()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var completed = false;
        using var subscription = source
            .Skip(2)
            .Take(2)
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        source.Emit(1);
        source.Emit(2);
        source.Emit(3);
        source.Emit(4);
        source.Emit(5);

        CollectionAssert.AreEqual(new[] { 3, 4 }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void TakeZero_CompletesWithoutSubscribingToSource()
    {
        var source = new ManualObservable<int>();
        var completed = false;
        using var subscription = source.Take(0).Subscribe(_ => Assert.Fail(), _ => Assert.Fail(), () => completed = true);

        Assert.IsTrue(completed);
        Assert.AreEqual(0, source.SubscriptionCount);
    }

    [TestMethod]
    public void Do_PerformsSideEffectBeforeForwardingValue()
    {
        var source = new ManualObservable<int>();
        var events = new List<string>();
        using var subscription = source
            .Do(value => events.Add($"Do:{value}"))
            .Subscribe(value => events.Add($"Next:{value}"));

        source.Emit(1);

        CollectionAssert.AreEqual(new[] { "Do:1", "Next:1" }, events);
    }

    [TestMethod]
    public void Do_WhenSideEffectThrows_TerminatesAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        Exception? error = null;
        var values = new List<int>();
        using var subscription = source
            .Do(_ => throw new TestException("Side effect failed."))
            .Subscribe(values.Add, exception => error = exception);

        source.Emit(1);
        source.Emit(2);

        Assert.IsEmpty(values);
        Assert.IsInstanceOfType<TestException>(error);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void ObserveOn_PostsAllNotificationsToSynchronizationContext()
    {
        var source = new ManualObservable<int>();
        var context = new QueuedSynchronizationContext();
        var events = new List<string>();
        using var subscription = source.ObserveOn(context).Subscribe(
            value => events.Add($"Next:{value}"),
            _ => Assert.Fail(),
            () => events.Add("Completed"));

        source.Emit(1);
        source.Complete();

        Assert.AreEqual(2, context.PendingCount);
        Assert.IsEmpty(events);
        context.RunAll();
        CollectionAssert.AreEqual(new[] { "Next:1", "Completed" }, events);
    }

    [TestMethod]
    public void ObserveOn_CurrentSynchronizationContextUsesCapturedContext()
    {
        var source = new ManualObservable<int>();
        var context = new QueuedSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var values = new List<int>();
            using var subscription = source
                .ObserveOn(ObservableSchedulers.Current)
                .Subscribe(values.Add);

            source.Emit(1);

            Assert.IsEmpty(values);
            Assert.AreEqual(1, context.PendingCount);
            context.RunAll();
            CollectionAssert.AreEqual(new[] { 1 }, values);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [TestMethod]
    public async Task ObserveOn_ThreadPoolForwardsNotificationOffCallerThread()
    {
        var source = new ManualObservable<int>();
        var notification = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = source
            .ObserveOn(ObservableSchedulers.ThreadPool)
            .Subscribe(value => notification.TrySetResult(value));

        source.Emit(1);

        Assert.AreEqual(1, await notification.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void SubscribeOn_CurrentSynchronizationContextDelaysSubscription()
    {
        var source = new ManualObservable<int>();
        var context = new QueuedSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            using var subscription = source
                .SubscribeOn(ObservableSchedulers.Current)
                .Subscribe(_ => Assert.Fail());

            Assert.AreEqual(0, source.SubscriptionCount);
            Assert.AreEqual(1, context.PendingCount);
            context.RunAll();
            Assert.AreEqual(1, source.SubscriptionCount);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [TestMethod]
    public void SubscribeOn_SynchronizationContextDelaysSubscription()
    {
        var source = new ManualObservable<int>();
        var context = new QueuedSynchronizationContext();

        using var subscription = source.SubscribeOn(context).Subscribe(_ => Assert.Fail());

        Assert.AreEqual(0, source.SubscriptionCount);
        Assert.AreEqual(1, context.PendingCount);
        context.RunAll();
        Assert.AreEqual(1, source.SubscriptionCount);
    }

    [TestMethod]
    public void SubscribeOn_SynchronizationContextCanBeDisposedBeforeSubscriptionRuns()
    {
        var source = new ManualObservable<int>();
        var context = new QueuedSynchronizationContext();
        var subscription = source.SubscribeOn(context).Subscribe(_ => Assert.Fail());

        subscription.Dispose();
        context.RunAll();

        Assert.AreEqual(0, source.SubscriptionCount);
    }

    [TestMethod]
    public async Task Throttle_EmitsOnlyLatestValueAfterQuietPeriod()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        using var subscription = source.Throttle(TimeSpan.FromMilliseconds(40)).Subscribe(values.Add);

        source.Emit(1);
        await Task.Delay(20);
        source.Emit(2);
        await Task.Delay(80);

        CollectionAssert.AreEqual(new[] { 2 }, values);
    }

    [TestMethod]
    public void Throttle_OnCompletion_FlushesPendingValueBeforeCompleting()
    {
        var source = new ManualObservable<int>();
        var events = new List<string>();
        using var subscription = source.Throttle(TimeSpan.FromMinutes(1)).Subscribe(
            value => events.Add($"Next:{value}"),
            _ => Assert.Fail(),
            () => events.Add("Completed"));

        source.Emit(1);
        source.Complete();

        CollectionAssert.AreEqual(new[] { "Next:1", "Completed" }, events);
    }

    [TestMethod]
    public void Throttle_SynchronousCompletion_DisposesSourceSubscription()
    {
        var source = new SynchronousObservable<int>(observer => observer.OnCompleted());
        var completed = false;

        using var subscription = source.Throttle(TimeSpan.Zero).Subscribe(
            _ => Assert.Fail(),
            _ => Assert.Fail(),
            () => completed = true);

        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Throttle_SynchronousError_DisposesSourceSubscription()
    {
        var source = new SynchronousObservable<int>(observer => observer.OnError(new TestException("Expected.")));
        Exception? error = null;

        using var subscription = source
            .Throttle(TimeSpan.Zero)
            .Subscribe(_ => Assert.Fail(), received => error = received);

        Assert.IsInstanceOfType<TestException>(error);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Throttle_CurrentSynchronizationContextSchedulesValueAndCompletion()
    {
        var source = new ManualObservable<int>();
        var context = new QueuedSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var events = new List<string>();
            using var subscription = source
                .Throttle(TimeSpan.FromMinutes(1), ObservableSchedulers.Current)
                .Subscribe(
                    value => events.Add($"Next:{value}"),
                    _ => Assert.Fail(),
                    () => events.Add("Completed"));

            source.Emit(1);
            source.Complete();

            Assert.IsEmpty(events);
            Assert.AreEqual(2, context.PendingCount);
            context.RunAll();
            CollectionAssert.AreEqual(new[] { "Next:1", "Completed" }, events);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [TestMethod]
    public async Task Throttle_ThreadPoolSchedulesNotification()
    {
        var source = new ManualObservable<int>();
        var notification = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = source
            .Throttle(TimeSpan.Zero, ObservableSchedulers.ThreadPool)
            .Subscribe(value => notification.TrySetResult(value));

        source.Emit(1);

        Assert.AreEqual(1, await notification.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void Catch_ReplacesFailedObservable()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var completed = false;
        using var subscription = source
            .Catch(_ => ObservableExtensions.Return(42))
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        source.Emit(1);
        source.Fail(new TestException("Expected."));

        CollectionAssert.AreEqual(new[] { 1, 42 }, values);
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void Catch_WhenHandlerThrows_ForwardsHandlerError()
    {
        var source = new ManualObservable<int>();
        Exception? received = null;
        using var subscription = source
            .Catch(_ => throw new TestException("Handler failed."))
            .Subscribe(_ => Assert.Fail(), error => received = error);

        source.Fail(new InvalidOperationException());

        Assert.IsInstanceOfType<TestException>(received);
        Assert.AreEqual("Handler failed.", received.Message);
    }

    [TestMethod]
    public void Return_EmitsValueAndCompletes()
    {
        var values = new List<int>();
        var completed = false;

        using var subscription = ObservableExtensions.Return(42)
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        CollectionAssert.AreEqual(new[] { 42 }, values);
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void Empty_CompletesWithoutValues()
    {
        var values = new List<int>();
        var completed = false;

        using var subscription = ObservableExtensions.Empty<int>()
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        Assert.IsEmpty(values);
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void Operators_ValidateArguments()
    {
        var source = new ManualObservable<int>();

        Assert.ThrowsExactly<ArgumentNullException>(() => source.Select((Func<int, int>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Where(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.DistinctUntilChanged(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Throttle(TimeSpan.Zero, (ObservableSchedulers)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Skip(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Take(-1));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Do(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.ObserveOn(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.SubscribeOn((SynchronizationContext)null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.ObserveOn((ObservableSchedulers)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.SubscribeOn((ObservableSchedulers)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Throttle(TimeSpan.FromMilliseconds(-1)));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Catch(null!));
    }
}