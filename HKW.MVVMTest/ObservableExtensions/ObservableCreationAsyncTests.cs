using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableCreationAsyncTests
{
    [TestMethod]
    public void Create_SynchronousCompletionDisposesResourceAndSuppressesLaterNotifications()
    {
        var values = new List<int>();
        var completed = 0;
        var resource = new TrackingDisposable();
        var observable = ObservableExtensions.Create<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnCompleted();
            observer.OnNext(2);
            return resource;
        });

        var subscription = observable.Subscribe(values.Add, _ => Assert.Fail(), () => completed++);
        subscription.Dispose();

        CollectionAssert.AreEqual(new[] { 1 }, values);
        Assert.AreEqual(1, completed);
        Assert.AreEqual(1, resource.DisposeCount);
    }

    [TestMethod]
    public void Create_ForwardsSubscribeFailuresAndNullResources()
    {
        var expected = new TestException("subscribe");
        Exception? thrownError = null;
        Exception? nullError = null;

        ObservableExtensions
            .Create<int>(_ => throw expected)
            .Subscribe(_ => Assert.Fail(), error => thrownError = error);
        ObservableExtensions
            .Create<int>(_ => null!)
            .Subscribe(_ => Assert.Fail(), error => nullError = error);

        Assert.AreSame(expected, thrownError);
        Assert.IsInstanceOfType<InvalidOperationException>(nullError);
    }

    [TestMethod]
    public void Defer_CreatesSourcePerSubscriptionAndForwardsFactoryFailure()
    {
        var factoryCalls = 0;
        var deferred = ObservableExtensions.Defer(() =>
            ObservableExtensions.Return(++factoryCalls)
        );
        var values = new List<int>();

        deferred.Subscribe(values.Add);
        deferred.Subscribe(values.Add);
        var expected = new TestException("factory");
        Exception? received = null;
        ObservableExtensions
            .Defer<int>(() => throw expected)
            .Subscribe(_ => Assert.Fail(), error => received = error);

        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
        Assert.AreSame(expected, received);
    }

    [TestMethod]
    public async Task FromAsync_FactoriesArePerSubscriptionAndSupportValueAndUnitTasks()
    {
        var calls = 0;
        var values = new List<int>();
        var observable = ObservableExtensions.FromAsync(_ => Task.FromResult(++calls));

        await observable.ToTask().WaitAsync(TimeSpan.FromSeconds(5));
        observable.Subscribe(values.Add);
        var unit = await ObservableExtensions
            .FromAsync(Task.CompletedTask)
            .ToTask()
            .WaitAsync(TimeSpan.FromSeconds(5));

        CollectionAssert.AreEqual(new[] { 2 }, values);
        Assert.AreEqual(2, calls);
        Assert.AreEqual(Unit.Default, unit);
    }

    [TestMethod]
    public async Task FromAsync_DisposalCancelsOwnedOperationAndSuppressesNotifications()
    {
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var notifications = 0;
        var observable = ObservableExtensions.FromAsync<int>(async cancellationToken =>
        {
            using var registration = cancellationToken.Register(() => cancellationObserved.SetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 1;
        });

        var subscription = observable.Subscribe(
            _ => notifications++,
            _ => notifications++,
            () => notifications++
        );
        subscription.Dispose();

        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(20);
        Assert.AreEqual(0, notifications);
    }

    [TestMethod]
    public async Task FromAsync_ForwardsTaskAndSynchronousFactoryErrors()
    {
        var taskError = new TestException("task");
        var factoryError = new TestException("factory");

        var receivedTaskError = await Assert.ThrowsExactlyAsync<TestException>(async () =>
            await ObservableExtensions
                .FromAsync(Task.FromException<int>(taskError))
                .ToTask()
        );
        var receivedFactoryError = await Assert.ThrowsExactlyAsync<TestException>(async () =>
            await ObservableExtensions
                .FromAsync<int>(_ => throw factoryError)
                .ToTask()
        );

        Assert.AreSame(taskError, receivedTaskError);
        Assert.AreSame(factoryError, receivedFactoryError);
    }

    [TestMethod]
    public async Task ToTask_ReturnsLastValueAndDisposesSynchronousSource()
    {
        var resource = new TrackingDisposable();
        var source = new InlineObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnNext(2);
            observer.OnCompleted();
            return resource;
        });

        var result = await source.ToTask();

        Assert.AreEqual(2, result);
        Assert.AreEqual(1, resource.DisposeCount);
    }

    [TestMethod]
    public async Task ToTask_EmptyAndErrorSequencesFail()
    {
        var expected = new TestException("source");
        var emptyError = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await ObservableExtensions.Empty<int>().ToTask()
        );
        var sourceError = await Assert.ThrowsExactlyAsync<TestException>(async () =>
            await new InlineObservable<int>(observer =>
            {
                observer.OnError(expected);
                return new TrackingDisposable();
            }).ToTask()
        );

        StringAssert.Contains(emptyError.Message, "no elements");
        Assert.AreSame(expected, sourceError);
    }

    [TestMethod]
    public async Task ToTask_CancellationDisposesSubscriptionAndIgnoresLaterValues()
    {
        var source = new ControlledObservable<int>();
        using var cancellation = new CancellationTokenSource();
        var task = source.ToTask(cancellation.Token);

        cancellation.Cancel();
        source.OnNext(42);
        source.OnCompleted();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await task);
        Assert.AreEqual(1, source.DisposalCount);
    }

    private sealed class InlineObservable<T>(
        Func<IObserver<T>, IDisposable> subscribe
    ) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer) => subscribe(observer);
    }

    private sealed class ControlledObservable<T> : IObservable<T>
    {
        private IObserver<T>? _observer;

        public int DisposalCount { get; private set; }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            return new CallbackDisposable(() => DisposalCount++);
        }

        public void OnNext(T value) => _observer?.OnNext(value);

        public void OnCompleted() => _observer?.OnCompleted();
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }

    private sealed class TestException(string message) : Exception(message);
}
