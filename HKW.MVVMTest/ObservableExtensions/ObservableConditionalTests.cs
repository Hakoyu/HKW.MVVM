using HKW.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableConditionalTests
{
    [TestMethod]
    public async Task FirstAsync_ReturnsFirstValueAndDisposesSynchronousSource()
    {
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnNext(2);
        });

        var result = await source.FirstAsync();

        Assert.AreEqual(1, result);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public async Task FirstAsync_PredicateReturnsFirstMatchAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        var result = source.FirstAsync(value => value % 2 == 0);

        source.Emit(1);
        source.Emit(2);
        source.Emit(4);

        Assert.AreEqual(2, await result);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public async Task FirstAsync_ForwardsPredicateAndSourceErrorsAndFailsForNoMatch()
    {
        var predicateError = new TestException("predicate");
        var sourceError = new TestException("source");
        var predicateSource = new ManualObservable<int>();
        var predicateTask = predicateSource.FirstAsync(_ => throw predicateError);
        predicateSource.Emit(1);

        var errorSource = new ManualObservable<int>();
        var errorTask = errorSource.FirstAsync();
        errorSource.Fail(sourceError);

        var emptySource = new ManualObservable<int>();
        var emptyTask = emptySource.FirstAsync(_ => false);
        emptySource.Complete();

        Assert.AreSame(
            predicateError,
            await Assert.ThrowsExactlyAsync<TestException>(async () => await predicateTask)
        );
        Assert.AreSame(
            sourceError,
            await Assert.ThrowsExactlyAsync<TestException>(async () => await errorTask)
        );
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await emptyTask);
        Assert.AreEqual(1, predicateSource.DisposalCount);
        Assert.AreEqual(1, errorSource.DisposalCount);
        Assert.AreEqual(1, emptySource.DisposalCount);
    }

    [TestMethod]
    public async Task FirstAsync_CancellationDisposesSource()
    {
        var source = new ManualObservable<int>();
        using var cancellation = new CancellationTokenSource();
        var task = source.FirstAsync(cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await task);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void TakeWhile_UsesIndexCompletesEarlyAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var indices = new List<int>();
        var completed = false;
        using var subscription = source
            .TakeWhile((value, index) =>
            {
                indices.Add(index);
                return value < 3;
            })
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        source.Emit(1);
        source.Emit(2);
        source.Emit(3);
        source.Emit(4);

        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, indices);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void SkipWhile_UsesIndexAndStopsEvaluatingAfterFirstFalse()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var indices = new List<int>();
        using var subscription = source
            .SkipWhile((_, index) =>
            {
                indices.Add(index);
                return index < 2;
            })
            .Subscribe(values.Add);

        source.Emit(10);
        source.Emit(20);
        source.Emit(30);
        source.Emit(40);

        CollectionAssert.AreEqual(new[] { 30, 40 }, values);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, indices);
    }

    [TestMethod]
    public void ConditionalOperators_ForwardPredicateAndSourceErrorsAndDispose()
    {
        var takeSource = new ManualObservable<int>();
        var takeError = new TestException("take");
        Exception? receivedTakeError = null;
        using var takeSubscription = takeSource
            .TakeWhile(_ => throw takeError)
            .Subscribe(_ => Assert.Fail(), error => receivedTakeError = error);
        takeSource.Emit(1);

        var skipSource = new ManualObservable<int>();
        var sourceError = new TestException("source");
        Exception? receivedSourceError = null;
        using var skipSubscription = skipSource
            .SkipWhile(_ => true)
            .Subscribe(_ => Assert.Fail(), error => receivedSourceError = error);
        skipSource.Fail(sourceError);

        Assert.AreSame(takeError, receivedTakeError);
        Assert.AreSame(sourceError, receivedSourceError);
        Assert.AreEqual(1, takeSource.DisposalCount);
        Assert.AreEqual(1, skipSource.DisposalCount);
    }

    [TestMethod]
    public void ConditionalOperators_DisposeSynchronousSubscriptionsAfterTermination()
    {
        var takeSource = new SynchronousObservable<int>(observer => observer.OnNext(1));
        var skipSource = new SynchronousObservable<int>(observer => observer.OnCompleted());

        using var takeSubscription = takeSource.TakeWhile(_ => false).Subscribe(_ => Assert.Fail());
        using var skipSubscription = skipSource.SkipWhile(_ => true).Subscribe(_ => Assert.Fail());

        Assert.AreEqual(1, takeSource.DisposalCount);
        Assert.AreEqual(1, skipSource.DisposalCount);
    }

    [TestMethod]
    public void ConditionalOperators_PreserveLogger()
    {
        var owner = new LoggerOwner();
        var source = owner.WhenAnyValue(value => value.Value);

        _ = source.TakeWhile(_ => true).Log();
        _ = source.SkipWhile((_, index) => index == 0).Log();
    }

    [TestMethod]
    public void ConditionalOperators_ValidateArguments()
    {
        var source = new ManualObservable<int>();

        Assert.ThrowsExactly<ArgumentNullException>(() => source.FirstAsync(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.TakeWhile((Func<int, bool>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.TakeWhile((Func<int, int, bool>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.SkipWhile((Func<int, bool>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.SkipWhile((Func<int, int, bool>)null!));
    }

    private sealed class LoggerOwner : ObservableObjectEx, IEnableLoggerWithLogger
    {
        private int _value;

        public ILogger Logger => NullLogger.Instance;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    private sealed class TestException(string message) : Exception(message);
}
