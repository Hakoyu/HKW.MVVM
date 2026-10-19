using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableExtensionsTests
{
    private sealed class ThrowingSubscribeObservable(Exception exception) : IObservable<int>
    {
        public IDisposable Subscribe(IObserver<int> observer) => throw exception;
    }

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

        Assert.AreEqual(1, context.PendingCount);
        Assert.IsEmpty(events);
        context.RunAll();
        CollectionAssert.AreEqual(new[] { "Next:1", "Completed" }, events);
    }

    [TestMethod]
    public async Task ObserveOn_ThreadPoolPreservesValueAndTerminalOrder()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = source.ObserveOn(ObservableSchedulers.ThreadPool).Subscribe(
            values.Add,
            error => Assert.Fail(error.Message),
            () => completed.TrySetResult(true));

        for (var value = 0; value < 100; value++) source.Emit(value);
        source.Complete();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(Enumerable.Range(0, 100).ToArray(), values);
    }

    [TestMethod]
    public void ObserveOn_CurrentThreadDeliversOnCallingThread()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        using var subscription = source
            .ObserveOn(ObservableSchedulers.CurrentThread)
            .Subscribe(values.Add);

        source.Emit(1);

        CollectionAssert.AreEqual(new[] { 1 }, values);
    }

    [TestMethod]
    public void ImmediateScheduler_ExecutesSynchronously()
    {
        var invoked = false;

        using var scheduled = ObservableSchedulers.Immediate.Schedule(() => invoked = true);

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void CurrentThreadScheduler_TrampolinesNestedWorkAndSupportsCancellation()
    {
        var events = new List<string>();

        ObservableSchedulers.CurrentThread.Schedule(() =>
        {
            events.Add("OuterStart");
            ObservableSchedulers.CurrentThread.Schedule(() => events.Add("Nested"));
            var cancelled = ObservableSchedulers.CurrentThread.Schedule(() => events.Add("Cancelled"));
            cancelled.Dispose();
            events.Add("OuterEnd");
        });

        CollectionAssert.AreEqual(new[] { "OuterStart", "OuterEnd", "Nested" }, events);
    }

    [TestMethod]
    public void SynchronizationContextScheduler_DisposeCancelsPostedWork()
    {
        var context = new QueuedSynchronizationContext();
        var invoked = false;
        var scheduled = ObservableSchedulers
            .FromSynchronizationContext(context)
            .Schedule(() => invoked = true);

        scheduled.Dispose();
        context.RunAll();

        Assert.IsFalse(invoked);
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
    public void SubscribeOn_CurrentThreadSubscribesOnCallingThread()
    {
        var source = new ManualObservable<int>();

        using var subscription = source
            .SubscribeOn(ObservableSchedulers.CurrentThread)
            .Subscribe(_ => Assert.Fail());

        Assert.AreEqual(1, source.SubscriptionCount);
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
    public async Task SubscribeOn_WhenSubscriptionThrows_ForwardsError()
    {
        var context = new QueuedSynchronizationContext();
        var expected = new TestException("Subscription failed.");
        Exception? received = null;
        var source = new ThrowingSubscribeObservable(expected);

        using var subscription = source.SubscribeOn(context).Subscribe(
            _ => Assert.Fail(),
            error => received = error
        );

        context.RunAll();
        await Task.Yield();

        Assert.AreSame(expected, received);
    }

    [TestMethod]
    public void Buffer_Count_EmitsFullBuffersAndFlushesRemainderOnCompletion()
    {
        var source = new ManualObservable<int>();
        var buffers = new List<IList<int>>();
        var completed = false;
        using var subscription = source.Buffer(2).Subscribe(
            buffers.Add,
            _ => Assert.Fail(),
            () => completed = true);

        source.Emit(1);
        source.Emit(2);
        source.Emit(3);
        source.Complete();

        Assert.HasCount(2, buffers);
        CollectionAssert.AreEqual(new[] { 1, 2 }, buffers[0].ToArray());
        CollectionAssert.AreEqual(new[] { 3 }, buffers[1].ToArray());
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public async Task Buffer_Time_EmitsValuesAndEmptyWindows()
    {
        var source = new ManualObservable<int>();
        var buffers = new List<IList<int>>();
        var twoBuffers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = source
            .Buffer(TimeSpan.FromMilliseconds(40))
            .Subscribe(buffer =>
            {
                lock (buffers)
                {
                    buffers.Add(buffer);
                    if (buffers.Count == 2)
                    {
                        twoBuffers.TrySetResult();
                    }
                }
            });

        source.Emit(1);
        await twoBuffers.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lock (buffers)
        {
            CollectionAssert.AreEqual(new[] { 1 }, buffers[0].ToArray());
            Assert.IsEmpty(buffers[1]);
        }
    }

    [TestMethod]
    public async Task Buffer_TimeOrCount_ClosesAtEitherBoundaryAndRestartsTimer()
    {
        var source = new ManualObservable<int>();
        var buffers = new List<IList<int>>();
        var twoBuffers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = source
            .Buffer(TimeSpan.FromMilliseconds(80), 2)
            .Subscribe(buffer =>
            {
                lock (buffers)
                {
                    buffers.Add(buffer);
                    if (buffers.Count == 2)
                    {
                        twoBuffers.TrySetResult();
                    }
                }
            });

        source.Emit(1);
        source.Emit(2);
        await Task.Delay(20);
        source.Emit(3);
        await twoBuffers.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lock (buffers)
        {
            CollectionAssert.AreEqual(new[] { 1, 2 }, buffers[0].ToArray());
            CollectionAssert.AreEqual(new[] { 3 }, buffers[1].ToArray());
        }
    }

    [TestMethod]
    public void Buffer_OnError_DiscardsPendingValuesAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        var buffers = new List<IList<int>>();
        Exception? error = null;
        using var subscription = source.Buffer(2).Subscribe(buffers.Add, received => error = received);

        source.Emit(1);
        source.Fail(new TestException("Expected."));

        Assert.IsEmpty(buffers);
        Assert.IsInstanceOfType<TestException>(error);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Buffer_DisposeStopsSourceAndTimerNotifications()
    {
        var source = new ManualObservable<int>();
        var buffers = new List<IList<int>>();
        var subscription = source.Buffer(TimeSpan.FromMilliseconds(20)).Subscribe(buffers.Add);

        source.Emit(1);
        subscription.Dispose();
        Thread.Sleep(60);

        Assert.IsEmpty(buffers);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Buffer_SynchronousCompletion_FlushesAndDisposesSourceSubscription()
    {
        var buffers = new List<IList<int>>();
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnCompleted();
        });

        using var subscription = source.Buffer(2).Subscribe(buffers.Add);

        Assert.HasCount(1, buffers);
        CollectionAssert.AreEqual(new[] { 1 }, buffers[0].ToArray());
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Throttle_EmitsOnlyLatestValueAfterQuietPeriod()
    {
        var source = new ManualObservable<int>();
        var timeProvider = new ManualTimeProvider();
        var values = new List<int>();
        using var subscription = source
            .Throttle(TimeSpan.FromMilliseconds(40), timeProvider)
            .Subscribe(values.Add);

        source.Emit(1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(20));
        source.Emit(2);
        timeProvider.Advance(TimeSpan.FromMilliseconds(40));

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
    public void Throttle_CurrentThreadSchedulesValueAndCompletionInOrder()
    {
        var source = new ManualObservable<int>();
        var events = new List<string>();
        using var subscription = source
            .Throttle(TimeSpan.FromMinutes(1), ObservableSchedulers.CurrentThread)
            .Subscribe(
                value => events.Add($"Next:{value}"),
                _ => Assert.Fail(),
                () => events.Add("Completed"));

        source.Emit(1);
        source.Complete();

        CollectionAssert.AreEqual(new[] { "Next:1", "Completed" }, events);
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
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.Throttle(TimeSpan.Zero, (IObservableScheduler)null!)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ObservableSchedulers.FromSynchronizationContext(null!)
        );
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Skip(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Take(-1));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Do(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.ObserveOn((IObservableScheduler)null!)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.SubscribeOn((SynchronizationContext)null!)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.SubscribeOn((IObservableScheduler)null!)
        );
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Throttle(TimeSpan.FromMilliseconds(-1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Buffer(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Buffer(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Buffer(TimeSpan.FromSeconds(1), 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Buffer(TimeSpan.Zero, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Catch(null!));
    }
}