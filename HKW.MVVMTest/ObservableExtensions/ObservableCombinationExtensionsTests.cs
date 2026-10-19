using HKW.MVVM;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableCombinationExtensionsTests
{
    [TestMethod]
    public void CombineLatest_TupleAndSelectorOverloads_EmitLatestValues()
    {
        var one = new TestObservable<int>();
        var two = new TestObservable<string>();
        var three = new TestObservable<bool>();
        var four = new TestObservable<double>();
        var pairs = new List<(int, string)>();
        var selected = new List<string>();

        using var pairSubscription = one.CombineLatest(two).Subscribe(pairs.Add);
        using var selectedSubscription = one.CombineLatest(two, three, four,
            (a, b, c, d) => $"{a}:{b}:{c}:{d}").Subscribe(selected.Add);

        one.Next(1);
        two.Next("a");
        three.Next(true);
        four.Next(2.5);
        one.Next(2);

        CollectionAssert.AreEqual(new[] { (1, "a"), (2, "a") }, pairs);
        CollectionAssert.AreEqual(new[] { "1:a:True:2.5", "2:a:True:2.5" }, selected);
    }

    [TestMethod]
    public void CombineLatest_SourceCompletesWithoutValue_CompletesAndDisposesOthers()
    {
        var one = new TestObservable<int>();
        var two = new TestObservable<int>();
        var completed = false;

        using var subscription = one.CombineLatest(two).Subscribe(_ => Assert.Fail(), _ => Assert.Fail(), () => completed = true);
        one.Complete();

        Assert.IsTrue(completed);
        Assert.AreEqual(1, two.DisposeCount);
    }

    [TestMethod]
    public void Switch_IgnoresStaleInnerAndWaitsForCurrentAfterOuterCompletes()
    {
        var outer = new TestObservable<IObservable<int>>();
        var first = new TestObservable<int>();
        var second = new TestObservable<int>();
        var values = new List<int>();
        var completed = false;
        using var subscription = outer.Switch().Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        outer.Next(first);
        first.Next(1);
        outer.Next(second);
        first.Next(2);
        outer.Complete();
        Assert.IsFalse(completed);
        second.Next(3);
        second.Complete();

        CollectionAssert.AreEqual(new[] { 1, 3 }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, first.DisposeCount);
    }

    [TestMethod]
    public void Switch_ReplacesPriorInnerWithoutHoldingStateGateDuringDisposal()
    {
        var outer = new TestObservable<IObservable<int>>();
        var disposalNotificationCompleted = false;
        var first = new DisposalCallbackObservable<int>((observer, _) =>
            disposalNotificationCompleted = Task.Run(() => observer.OnNext(99)).Wait(TimeSpan.FromSeconds(1)));
        var second = new TestObservable<int>();
        var values = new List<int>();
        using var subscription = outer.Switch().Subscribe(values.Add);

        outer.Next(first);
        outer.Next(second);
        second.Next(2);

        Assert.IsTrue(disposalNotificationCompleted);
        CollectionAssert.AreEqual(new[] { 2 }, values);
    }

    [TestMethod]
    public void Switch_OuterCompletionDisposesOuterWhileCurrentInnerRemainsActive()
    {
        var outer = new TestObservable<IObservable<int>>();
        var inner = new TestObservable<int>();
        var completed = false;
        using var subscription = outer.Switch().Subscribe(_ => { }, _ => Assert.Fail(), () => completed = true);

        outer.Next(inner);
        outer.Complete();

        Assert.AreEqual(1, outer.DisposeCount);
        Assert.AreEqual(0, inner.DisposeCount);
        Assert.IsFalse(completed);
        inner.Complete();
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void Merge_EnumerableStopsEnumeratingAfterSynchronousInnerError()
    {
        var error = new InvalidOperationException("failure");
        var sources = new CountingEnumerable(
            new InlineObservable<int>(observer => observer.OnError(error)),
            new TestObservable<int>());
        Exception? received = null;

        using var subscription = sources.Merge().Subscribe(_ => Assert.Fail(), exception => received = exception);

        Assert.AreSame(error, received);
        Assert.AreEqual(1, sources.MoveNextCount);
    }

    [TestMethod]
    public void Merge_EnumerableRunsConcurrentlyAndFirstErrorDisposesAll()
    {
        var first = new TestObservable<int>();
        var second = new TestObservable<int>();
        var error = new InvalidOperationException("failure");
        Exception? received = null;
        var values = new List<int>();
        using var subscription = new[] { first, second }.Merge().Subscribe(values.Add, ex => received = ex);

        first.Next(1);
        second.Next(2);
        second.Error(error);
        first.Next(3);

        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
        Assert.AreSame(error, received);
        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
    }

    [TestMethod]
    public void SelectMany_MergesInnerSequencesAndCompletesAfterAllInners()
    {
        var outer = new TestObservable<int>();
        var first = new TestObservable<string>();
        var second = new TestObservable<string>();
        var completed = false;
        var values = new List<string>();
        using var subscription = outer.SelectMany(value => value == 1 ? first : second)
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        outer.Next(1);
        outer.Next(2);
        first.Next("a");
        second.Next("b");
        outer.Complete();
        first.Complete();
        Assert.IsFalse(completed);
        second.Complete();

        CollectionAssert.AreEqual(new[] { "a", "b" }, values);
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void SelectMany_OuterCompletionDisposesOuterWhileInnersRemainActive()
    {
        var outer = new TestObservable<int>();
        var inner = new TestObservable<string>();
        var completed = false;
        using var subscription = outer.SelectMany(_ => inner)
            .Subscribe(_ => { }, _ => Assert.Fail(), () => completed = true);

        outer.Next(1);
        outer.Complete();

        Assert.AreEqual(1, outer.DisposeCount);
        Assert.AreEqual(0, inner.DisposeCount);
        Assert.IsFalse(completed);
        inner.Complete();
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void Concat_EnumerableSubscribesSequentiallyAndHandlesSynchronousSources()
    {
        var first = new TestObservable<int>();
        var second = new TestObservable<int>();
        var values = new List<int>();
        var completed = false;
        var synchronous = new InlineObservable<int>(observer => { observer.OnNext(0); observer.OnCompleted(); });
        using var subscription = new IObservable<int>[] { synchronous, first, second }.Concat()
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        Assert.AreEqual(1, first.SubscribeCount);
        Assert.AreEqual(0, second.SubscribeCount);
        first.Next(1);
        first.Complete();
        Assert.AreEqual(1, second.SubscribeCount);
        second.Next(2);
        second.Complete();

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, values);
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void WithLatestFrom_UsesLatestValue_AndPrimaryControlsCompletion()
    {
        var source = new TestObservable<int>();
        var other = new TestObservable<string>();
        var values = new List<string>();
        var completed = false;
        using var subscription = source.WithLatestFrom(other, (value, latest) => $"{value}:{latest}")
            .Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        source.Next(0);
        other.Next("a");
        source.Next(1);
        other.Next("b");
        other.Complete();
        source.Next(2);
        Assert.IsFalse(completed);
        source.Complete();

        CollectionAssert.AreEqual(new[] { "1:a", "2:b" }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposeCount);
        Assert.AreEqual(1, other.DisposeCount);
    }

    [TestMethod]
    public void WithLatestFrom_SynchronousOtherError_DoesNotSubscribePrimary()
    {
        var source = new TestObservable<int>();
        var expected = new InvalidOperationException("other");
        Exception? received = null;
        var other = new InlineObservable<string>(observer => observer.OnError(expected));

        using var subscription = source.WithLatestFrom(other).Subscribe(_ => Assert.Fail(), error => received = error);

        Assert.AreSame(expected, received);
        Assert.AreEqual(0, source.SubscribeCount);
    }

    [TestMethod]
    public void Zip_QueuesPairsAndCompletesWhenCompletedSideIsDrained()
    {
        var first = new TestObservable<int>();
        var second = new TestObservable<string>();
        var values = new List<(int, string)>();
        var completed = false;
        using var subscription = first.Zip(second).Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        first.Next(1);
        first.Next(2);
        first.Complete();
        Assert.IsFalse(completed);
        second.Next("a");
        Assert.IsFalse(completed);
        second.Next("b");

        CollectionAssert.AreEqual(new[] { (1, "a"), (2, "b") }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
    }

    [TestMethod]
    public void Zip_SynchronousEmptyFirst_CompletesWithoutSubscribingSecond()
    {
        var second = new TestObservable<int>();
        var completed = false;
        var first = new InlineObservable<int>(observer => observer.OnCompleted());

        using var subscription = first.Zip(second).Subscribe(_ => Assert.Fail(), _ => Assert.Fail(), () => completed = true);

        Assert.IsTrue(completed);
        Assert.AreEqual(0, second.SubscribeCount);
    }

    [TestMethod]
    public async Task Zip_ConcurrentProducers_SerializesAndPairsEveryValue()
    {
        var first = new TestObservable<int>();
        var second = new TestObservable<int>();
        var values = new List<(int, int)>();
        using var subscription = first.Zip(second).Subscribe(values.Add);

        await Task.WhenAll(
            Task.Run(() => { for (var index = 0; index < 1_000; index++) first.Next(index); }),
            Task.Run(() => { for (var index = 0; index < 1_000; index++) second.Next(index); })
        );

        Assert.AreEqual(1_000, values.Count);
        for (var index = 0; index < values.Count; index++)
            Assert.AreEqual((index, index), values[index]);
    }

    [TestMethod]
    public void Zip_SelectorFailure_ErrorsOnceAndDisposesBothSources()
    {
        var first = new TestObservable<int>();
        var second = new TestObservable<int>();
        var errors = new List<Exception>();
        using var subscription = first.Zip<int, int, int>(second, (_, _) => throw new InvalidOperationException("selector"))
            .Subscribe(_ => Assert.Fail(), errors.Add);

        first.Next(1);
        second.Next(2);
        first.Next(3);
        second.Next(4);

        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual("selector", errors[0].Message);
        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
    }

    [TestMethod]
    public void CombinationOperators_PreserveLoggerFromPrimarySource()
    {
        var raw = new TestObservable<int>();
        IObservable<int> source = new ObservableWithLogger<int>(raw, () => NullLogger.Instance);

        Assert.IsInstanceOfType<IObservableWithLogger>(source.CombineLatest(new TestObservable<int>()));
        Assert.IsInstanceOfType<IObservableWithLogger>(source.WithLatestFrom(new TestObservable<int>()));
        Assert.IsInstanceOfType<IObservableWithLogger>(source.Zip(new TestObservable<int>()));
        Assert.IsInstanceOfType<IObservableWithLogger>(source.Merge(new TestObservable<int>()));
        Assert.IsInstanceOfType<IObservableWithLogger>(source.Concat(new TestObservable<int>()));
        Assert.IsInstanceOfType<IObservableWithLogger>(source.SelectMany(_ => new TestObservable<string>()));

        IObservable<IObservable<int>> outer = new ObservableWithLogger<IObservable<int>>(
            new TestObservable<IObservable<int>>(), () => NullLogger.Instance);
        Assert.IsInstanceOfType<IObservableWithLogger>(outer.Switch());
    }

    [TestMethod]
    public void CombinationOperators_ValidateNullsAndForwardSelectorEnumerationAndSubscribeFailures()
    {
        var source = new TestObservable<int>();
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.CombineLatest<int, int>(source, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.WithLatestFrom<int, int>(source, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Zip<int, int>(source, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.WithLatestFrom<int, int, int>(source, new TestObservable<int>(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Zip<int, int, int>(source, new TestObservable<int>(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.SelectMany<int, int>(source, null!));

        Exception? selectorError = null;
        using var selectorSubscription = source.SelectMany<int, int>(_ => throw new InvalidOperationException("selector"))
            .Subscribe(_ => Assert.Fail(), ex => selectorError = ex);
        source.Next(1);
        Assert.AreEqual("selector", selectorError?.Message);

        Exception? enumerationError = null;
        using var enumerationSubscription = ThrowingEnumerable().Merge().Subscribe(_ => Assert.Fail(), ex => enumerationError = ex);
        Assert.AreEqual("enumeration", enumerationError?.Message);

        Exception? subscribeError = null;
        using var subscribeSubscription = new InlineObservable<int>(_ => throw new InvalidOperationException("subscribe"))
            .Concat(new TestObservable<int>()).Subscribe(_ => Assert.Fail(), ex => subscribeError = ex);
        Assert.AreEqual("subscribe", subscribeError?.Message);
    }

    private static IEnumerable<IObservable<int>> ThrowingEnumerable()
    {
        yield return new InlineObservable<int>(observer => observer.OnCompleted());
        throw new InvalidOperationException("enumeration");
    }

    private sealed class InlineObservable<T>(Action<IObserver<T>> action) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            action(observer);
            return new TestDisposable();
        }
    }

    private sealed class DisposalCallbackObservable<T>(Action<IObserver<T>, IDisposable> onDispose) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            TestDisposable? subscription = null;
            subscription = new TestDisposable(() => onDispose(observer, subscription!));
            return subscription;
        }
    }

    private sealed class CountingEnumerable(params IObservable<int>[] sources) : IEnumerable<IObservable<int>>
    {
        public int MoveNextCount { get; private set; }

        public IEnumerator<IObservable<int>> GetEnumerator()
        {
            foreach (var source in sources)
            {
                MoveNextCount++;
                yield return source;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TestObservable<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];
        public int DisposeCount { get; private set; }
        public int SubscribeCount { get; private set; }
        public Action? OnDispose { get; set; }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            SubscribeCount++;
            _observers.Add(observer);
            return new TestDisposable(() =>
            {
                _observers.Remove(observer);
                DisposeCount++;
                OnDispose?.Invoke();
            });
        }

        public void Next(T value) { foreach (var observer in _observers.ToArray()) observer.OnNext(value); }
        public void Error(Exception error) { foreach (var observer in _observers.ToArray()) observer.OnError(error); }
        public void Complete() { foreach (var observer in _observers.ToArray()) observer.OnCompleted(); }
    }

    private sealed class TestDisposable(Action? dispose = null) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
