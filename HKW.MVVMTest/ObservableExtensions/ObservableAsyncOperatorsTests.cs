using System.Runtime.CompilerServices;
using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableAsyncOperatorsTests
{
    [TestMethod]
    public async Task FromAsync_ValueTaskOverloadsPropagateValuesAndErrors()
    {
        var value = await ObservableExtensions.FromAsync(new ValueTask<int>(1)).ToTask();
        var unit = await ObservableExtensions.FromAsync(ValueTask.CompletedTask).ToTask();
        var expected = new TestException();
        var received = await Assert.ThrowsExactlyAsync<TestException>(async () =>
            await ObservableExtensions.FromAsync(ValueTask.FromException<int>(expected)).ToTask()
        );

        Assert.AreEqual(1, value);
        Assert.AreEqual(Unit.Default, unit);
        Assert.AreSame(expected, received);
    }

    [TestMethod]
    public async Task AsyncEnumerableConversionsPropagateCompletionErrorCancellationAndDispose()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var values = new List<int>();
        await foreach (var value in ObservableExtensions.FromAsyncEnumerable(Sequence(disposed)).ToAsyncEnumerable(1))
        {
            values.Add(value);
        }
        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var expected = new TestException();
        var error = await Assert.ThrowsExactlyAsync<TestException>(async () =>
        {
            await foreach (var _ in ObservableExtensions.FromAsyncEnumerable(Failing(expected)).ToAsyncEnumerable()) { }
        });
        Assert.AreSame(expected, error);

        using var cancellation = new CancellationTokenSource();
        var source = new TrackingObservable<int>();
        var enumeration = source.ToAsyncEnumerable(cancellationToken: cancellation.Token).GetAsyncEnumerator();
        var pending = enumeration.MoveNextAsync().AsTask();
        await source.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
        await enumeration.DisposeAsync();
        Assert.AreEqual(1, source.DisposeCount);
    }

    [TestMethod]
    public async Task SelectAsync_LatestCancelsPriorAndSequentialPreservesOrder()
    {
        var latestSource = new TestSubject<int>();
        var firstCancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestValues = new List<int>();
        var latestDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        latestSource.SelectAsync(async (value, token) =>
        {
            if (value == 1)
            {
                token.Register(() => firstCancellation.TrySetResult());
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return value;
        }, AsyncConcurrencyMode.Latest).Subscribe(latestValues.Add, latestDone.SetException, latestDone.SetResult);

        latestSource.Next(1);
        latestSource.Next(2);
        latestSource.Complete();
        await latestDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await firstCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { 2 }, latestValues);

        var sequentialSource = new TestSubject<int>();
        var releases = new Dictionary<int, TaskCompletionSource<int>>
        {
            [1] = NewCompletion<int>(),
            [2] = NewCompletion<int>(),
        };
        var started = new List<int>();
        var sequentialValues = new List<int>();
        var sequentialDone = NewCompletion();
        sequentialSource.SelectAsync((value, _) =>
        {
            started.Add(value);
            return releases[value].Task;
        }, AsyncConcurrencyMode.Sequential).Subscribe(sequentialValues.Add, sequentialDone.SetException, sequentialDone.SetResult);

        sequentialSource.Next(1);
        sequentialSource.Next(2);
        sequentialSource.Complete();
        CollectionAssert.AreEqual(new[] { 1 }, started);
        releases[1].SetResult(10);
        await WaitUntilAsync(() => started.Count == 2);
        releases[2].SetResult(20);
        await sequentialDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { 10, 20 }, sequentialValues);
    }

    [TestMethod]
    public async Task SelectAsync_DropIgnoresBusyAndConcurrentUsesCompletionOrder()
    {
        var dropSource = new TestSubject<int>();
        var dropRelease = NewCompletion<int>();
        var dropValues = new List<int>();
        var dropDone = NewCompletion();
        dropSource.SelectAsync((value, _) => dropRelease.Task, AsyncConcurrencyMode.Drop)
            .Subscribe(dropValues.Add, dropDone.SetException, dropDone.SetResult);
        dropSource.Next(1);
        dropSource.Next(2);
        dropSource.Complete();
        dropRelease.SetResult(1);
        await dropDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { 1 }, dropValues);

        var concurrentSource = new TestSubject<int>();
        var operations = new[] { NewCompletion<int>(), NewCompletion<int>() };
        var concurrentValues = new List<int>();
        var concurrentDone = NewCompletion();
        concurrentSource.SelectAsync((value, _) => operations[value - 1].Task, AsyncConcurrencyMode.Concurrent)
            .Subscribe(concurrentValues.Add, concurrentDone.SetException, concurrentDone.SetResult);
        concurrentSource.Next(1);
        concurrentSource.Next(2);
        concurrentSource.Complete();
        operations[1].SetResult(2);
        await WaitUntilAsync(() => concurrentValues.Count == 1);
        operations[0].SetResult(1);
        await concurrentDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { 2, 1 }, concurrentValues);
    }

    [TestMethod]
    public void Retry_PredicateDelayBackoffAndDisposalAreDeterministic()
    {
        var time = new ManualTimeProvider();
        var source = new RetryObservable();
        var values = new List<int>();
        source.Retry(3, error => error is TestException, TimeSpan.FromSeconds(1), 2, time)
            .Subscribe(values.Add, _ => Assert.Fail());

        Assert.AreEqual(1, source.SubscribeCount);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, source.SubscribeCount);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, source.SubscribeCount);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(3, source.SubscribeCount);

        var disposedSource = new RetryObservable();
        var subscription = disposedSource.Retry(1, TimeSpan.FromSeconds(1), time).Subscribe(_ => { });
        subscription.Dispose();
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(1, disposedSource.SubscribeCount);
    }

    private static async IAsyncEnumerable<int> Sequence(
        TaskCompletionSource disposed,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            yield return 1;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return 2;
        }
        finally
        {
            disposed.TrySetResult();
        }
    }

    private static async IAsyncEnumerable<int> Failing(Exception error)
    {
        yield return 1;
        await Task.Yield();
        throw error;
    }

    private static TaskCompletionSource NewCompletion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<T> NewCompletion<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class TestSubject<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new CallbackDisposable(() => _observers.Remove(observer));
        }
        public void Next(T value) { foreach (var observer in _observers.ToArray()) observer.OnNext(value); }
        public void Complete() { foreach (var observer in _observers.ToArray()) observer.OnCompleted(); }
    }

    private sealed class TrackingObservable<T> : IObservable<T>
    {
        public TaskCompletionSource Subscribed { get; } = NewCompletion();
        public int DisposeCount { get; private set; }
        public IDisposable Subscribe(IObserver<T> observer)
        {
            Subscribed.TrySetResult();
            return new CallbackDisposable(() => DisposeCount++);
        }
    }

    private sealed class RetryObservable : IObservable<int>
    {
        public int SubscribeCount { get; private set; }
        public IDisposable Subscribe(IObserver<int> observer)
        {
            SubscribeCount++;
            observer.OnError(new TestException());
            return new CallbackDisposable();
        }
    }

    private sealed class CallbackDisposable(Action? callback = null) : IDisposable
    {
        private Action? _callback = callback;
        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }

    private sealed class TestException : Exception { }
}
