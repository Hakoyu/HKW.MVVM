using HKW.MVVM;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservablePracticalOperatorsTests
{
    [TestMethod]
    public void Quantifiers_HandleEmptyMultipleAndEarlyTermination()
    {
        CollectionAssert.AreEqual(new[] { false }, Collect(ObservableExtensions.Empty<int>().Any()));
        CollectionAssert.AreEqual(new[] { true }, Collect(Values(1, 2, 3).Any(value => value == 2)));
        CollectionAssert.AreEqual(new[] { true }, Collect(ObservableExtensions.Empty<int>().All(_ => false)));
        CollectionAssert.AreEqual(new[] { false }, Collect(Values(2, 3).All(value => value % 2 == 0)));
        CollectionAssert.AreEqual(new[] { true }, Collect(Values("a", "B").Contains("b", StringComparer.OrdinalIgnoreCase)));

        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnNext(2);
            observer.OnNext(3);
        });
        CollectionAssert.AreEqual(new[] { true }, Collect(source.Any(value => value == 1)));
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Aggregates_HandleEmptySingleMultipleAndFreshLists()
    {
        CollectionAssert.AreEqual(new[] { 0 }, Collect(ObservableExtensions.Empty<int>().Count()));
        CollectionAssert.AreEqual(new[] { 2 }, Collect(Values(1, 2, 4).Count(value => value % 2 == 0)));
        CollectionAssert.AreEqual(new[] { 6 }, Collect(Values(1, 2, 3).Aggregate((left, right) => left + right)));
        CollectionAssert.AreEqual(new[] { 16 }, Collect(Values(1, 2, 3).Aggregate(10, (sum, value) => sum + value)));
        CollectionAssert.AreEqual(new[] { "6" }, Collect(Values(1, 2, 3).Aggregate(0, (sum, value) => sum + value, value => value.ToString())));
        Assert.ThrowsExactly<InvalidOperationException>(() => Collect(ObservableExtensions.Empty<int>().Aggregate((x, y) => x + y)));

        var lists = new List<IList<int>>();
        var observable = Values(1, 2).ToList();
        lists.Add(Collect(observable).Single());
        lists.Add(Collect(observable).Single());
        CollectionAssert.AreEqual(new[] { 1, 2 }, lists[0].ToArray());
        Assert.AreNotSame(lists[0], lists[1]);
        CollectionAssert.AreEqual(new[] { 42 }, Collect(ObservableExtensions.Empty<int>().DefaultIfEmpty(42)));
        CollectionAssert.AreEqual(new[] { 1 }, Collect(Values(1).DefaultIfEmpty(42)));
    }

    [TestMethod]
    public async Task TaskTerminals_HandleEmptySingleMultipleAndPredicates()
    {
        Assert.AreEqual(0, await ObservableExtensions.Empty<int>().FirstOrDefaultAsync());
        Assert.AreEqual(2, await Values(1, 2, 3).FirstOrDefaultAsync(value => value > 1));
        Assert.AreEqual(3, await Values(1, 2, 3).LastAsync());
        Assert.AreEqual(2, await Values(1, 2, 3).LastAsync(value => value < 3));
        Assert.AreEqual(0, await ObservableExtensions.Empty<int>().LastOrDefaultAsync());
        Assert.AreEqual(7, await Values(7).SingleAsync());
        Assert.AreEqual(2, await Values(1, 2, 3).SingleAsync(value => value % 2 == 0));
        Assert.AreEqual(0, await ObservableExtensions.Empty<int>().SingleOrDefaultAsync());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await ObservableExtensions.Empty<int>().SingleAsync());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Values(1, 2).SingleAsync());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await ObservableExtensions.Empty<int>().LastAsync());
    }

    [TestMethod]
    public async Task TaskTerminals_ForwardErrorsCancelAndDispose()
    {
        var error = new TestException("source");
        var failed = new ManualObservable<int>();
        var failedTask = failed.LastOrDefaultAsync();
        failed.Fail(error);
        Assert.AreSame(error, await Assert.ThrowsExactlyAsync<TestException>(async () => await failedTask));
        Assert.AreEqual(1, failed.DisposalCount);

        var cancelled = new ManualObservable<int>();
        using var cancellation = new CancellationTokenSource();
        var cancelledTask = cancelled.SingleOrDefaultAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await cancelledTask);
        Assert.AreEqual(1, cancelled.DisposalCount);

        var multiple = new ManualObservable<int>();
        var multipleTask = multiple.SingleAsync();
        multiple.Emit(1);
        multiple.Emit(2);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await multipleTask);
        Assert.AreEqual(1, multiple.DisposalCount);
    }

    [TestMethod]
    public void TypeAndDistinctOperators_HandleValuesAndDelegateErrors()
    {
        IObservable<object?> source = Values<object?>(1, "two", 1, null, "two");
        CollectionAssert.AreEqual(new[] { "two", "two" }, Collect(source.OfType<string>()));
        Assert.ThrowsExactly<InvalidCastException>(() => Collect(source.Cast<int>()));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Collect(Values(1, 2, 1, 3, 2).Distinct()));

        var error = new TestException("comparer");
        Assert.AreSame(error, Assert.ThrowsExactly<TestException>(() => Collect(Values(1).Distinct(new ThrowingComparer<int>(error)))));
        Assert.AreSame(error, Assert.ThrowsExactly<TestException>(() => Collect(Values(1).Contains(1, new ThrowingComparer<int>(error)))));
    }

    [TestMethod]
    public void TailAndPairwiseOperators_HandleEmptySingleAndMultiple()
    {
        CollectionAssert.AreEqual(Array.Empty<int>(), Collect(ObservableExtensions.Empty<int>().TakeLast(2)));
        CollectionAssert.AreEqual(new[] { 3, 4 }, Collect(Values(1, 2, 3, 4).TakeLast(2)));
        CollectionAssert.AreEqual(new[] { 1, 2 }, Collect(Values(1, 2, 3, 4).SkipLast(2)));
        CollectionAssert.AreEqual(new[] { 1, 2 }, Collect(Values(1, 2).SkipLast(0)));
        Assert.AreEqual(0, Collect(Values(1).Pairwise()).Count);
        CollectionAssert.AreEqual(
            new[] { (Previous: 1, Current: 2), (Previous: 2, Current: 3) },
            Collect(Values(1, 2, 3).Pairwise())
        );
    }

    [TestMethod]
    public void SequenceOperators_PreserveOrderingErrorsAndCompletion()
    {
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Collect(Values(1, 2).Prepend(0)));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Collect(Values(1, 2).Append(3)));
        CollectionAssert.AreEqual(new[] { 1, 2, 1, 2 }, Collect(Values(1, 2).Repeat(2)));
        CollectionAssert.AreEqual(Array.Empty<int>(), Collect(Values(1, 2).Repeat(0)));
        CollectionAssert.AreEqual(Array.Empty<int>(), Collect(Values(1, 2).IgnoreElements()));

        var error = new TestException("source");
        var source = new ManualObservable<int>();
        var values = new List<int>();
        Exception? received = null;
        source.Append(3).Subscribe(values.Add, exception => received = exception);
        source.Emit(1);
        source.Fail(error);
        CollectionAssert.AreEqual(new[] { 1 }, values);
        Assert.AreSame(error, received);
    }

    [TestMethod]
    public void Repeat_YieldsBeforeResubscribingAfterSynchronousCompletion()
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var subscriptionCount = 0;
        using var secondSubscription = new ManualResetEventSlim();
        var source = new SynchronousObservable<int>(observer =>
        {
            if (Interlocked.Increment(ref subscriptionCount) == 1)
            {
                observer.OnCompleted();
                return;
            }

            if (Environment.CurrentManagedThreadId == callerThread)
            {
                observer.OnError(new TestException("Repeated synchronously."));
            }
            secondSubscription.Set();
        });
        Exception? error = null;

        using var subscription = source.Repeat().Subscribe(_ => { }, exception => error = exception);

        Assert.IsTrue(secondSubscription.Wait(TimeSpan.FromSeconds(2)));
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Operators_ForwardSourceErrorsAndDisposeExplicitly()
    {
        var error = new TestException("source");
        var source = new ManualObservable<int>();
        Exception? received = null;
        using var subscription = source.TakeLast(2).Subscribe(_ => Assert.Fail(), exception => received = exception);
        source.Emit(1);
        source.Fail(error);
        Assert.AreSame(error, received);
        Assert.AreEqual(1, source.DisposalCount);

        var disposableSource = new ManualObservable<int>();
        var explicitSubscription = disposableSource.Pairwise().Subscribe(_ => Assert.Fail());
        explicitSubscription.Dispose();
        disposableSource.Emit(1);
        disposableSource.Emit(2);
        Assert.AreEqual(1, disposableSource.DisposalCount);
    }

    [TestMethod]
    public void DelegateExceptions_TerminateAndDispose()
    {
        var error = new TestException("delegate");
        var source = new ManualObservable<int>();
        Exception? received = null;
        source.Aggregate(0, (_, _) => throw error).Subscribe(_ => Assert.Fail(), exception => received = exception);
        source.Emit(1);
        Assert.AreSame(error, received);
        Assert.AreEqual(1, source.DisposalCount);

        var predicateSource = new ManualObservable<int>();
        Exception? predicateReceived = null;
        predicateSource.Any(_ => throw error).Subscribe(_ => Assert.Fail(), exception => predicateReceived = exception);
        predicateSource.Emit(1);
        Assert.AreSame(error, predicateReceived);
        Assert.AreEqual(1, predicateSource.DisposalCount);
    }

    [TestMethod]
    public void PracticalOperators_PreserveLogger()
    {
        IObservable<object?> source = new ObservableWithLogger<object?>(Values<object?>(1, 2), () => NullLogger.Instance);
        IObservable<int> integers = new ObservableWithLogger<int>(Values(1, 2), () => NullLogger.Instance);
        object[] results =
        [
            integers.Any(), integers.All(_ => true), integers.Contains(1), integers.Count(),
            integers.Aggregate(0, (x, y) => x + y), integers.ToList(), integers.DefaultIfEmpty(),
            source.OfType<int>(), source.Cast<int>(), integers.Distinct(), integers.TakeLast(1),
            integers.SkipLast(1), integers.Pairwise(), integers.Prepend(0), integers.Append(3),
            integers.Repeat(1), integers.IgnoreElements()
        ];
        Assert.IsTrue(results.All(result => result is IObservableWithLogger));
    }

    [TestMethod]
    public void PracticalOperators_ValidateArguments()
    {
        IObservable<int>? missing = null;
        var source = Values(1);
        Assert.ThrowsExactly<ArgumentNullException>(() => missing!.Any());
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Any(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.All(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Contains(1, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Count(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Aggregate(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Aggregate(0, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Distinct(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.TakeLast(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.SkipLast(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Repeat(-1));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.FirstOrDefaultAsync(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.LastAsync(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.LastOrDefaultAsync(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.SingleAsync(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.SingleOrDefaultAsync(null!));
    }

    private static IObservable<T> Values<T>(params T[] values) =>
        new SynchronousObservable<T>(observer =>
        {
            foreach (var value in values)
            {
                observer.OnNext(value);
            }
            observer.OnCompleted();
        });

    private static List<T> Collect<T>(IObservable<T> source)
    {
        var values = new List<T>();
        Exception? error = null;
        source.Subscribe(values.Add, exception => error = exception);
        if (error is not null)
        {
            throw error;
        }
        return values;
    }

    private sealed class ThrowingComparer<T>(Exception error) : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y) => throw error;
        public int GetHashCode(T obj) => throw error;
    }
}
