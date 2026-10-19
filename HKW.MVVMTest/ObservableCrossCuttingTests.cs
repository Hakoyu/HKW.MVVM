using HKW.MVVM;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableCrossCuttingTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1);

    [TestMethod]
    public void NewlyAddedOperators_ValidatePublicArguments()
    {
        var source = new ManualObservable<int>();
        var second = new ManualObservable<string>();
        var third = new ManualObservable<bool>();
        var fourth = new ManualObservable<double>();
        IObservable<int>? nullSource = null;
        IEnumerable<IObservable<int>>? nullSources = null;

        Assert.ThrowsExactly<ArgumentNullException>(() => nullSource!.Buffer(1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Buffer(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Buffer(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Buffer(Interval, 0));

        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.CombineLatest<int, string>(null!, second));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.CombineLatest((IObservable<string>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.CombineLatest(second, (Func<int, string, string>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.CombineLatest<int, string, bool, int>(source, second, null!, (a, b, c) => a));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.CombineLatest<int, string, bool, double, int>(source, second, third, null!, (a, b, c, d) => a));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Switch<int>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Merge<int>(null!, source));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Merge(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => nullSources!.Merge());
        Assert.ThrowsExactly<ArgumentNullException>(() => source.SelectMany<int, int>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Concat<int>(null!, source));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Concat(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => nullSources!.Concat());

        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Scan<int>(null!, (a, b) => a + b));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Scan((Func<int, int, int>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Scan(0, (Func<int, int, int>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Retry<int>(null!, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Retry(-1));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Finally(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.TakeUntil<int, string>(null!));

        Assert.ThrowsExactly<ArgumentNullException>(() => nullSource!.Delay(Interval));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Delay(Interval, null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Delay(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => nullSource!.Timeout(Interval));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Timeout(Interval, (TimeProvider)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Timeout(Interval, (IObservable<int>)null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Timeout(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => nullSource!.Sample(Interval));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Sample(Interval, null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Sample(TimeSpan.Zero));
    }

    [TestMethod]
    public void ResultTypeChangingOperators_PreservePrimarySourceLogger()
    {
        IObservable<int> source = new ObservableWithLogger<int>(
            new ManualObservable<int>(),
            () => NullLogger.Instance);
        var text = new ManualObservable<string>();
        var flag = new ManualObservable<bool>();
        var number = new ManualObservable<double>();

        IObservable<object>[] results =
        [
            source.Buffer(2).Select(static value => (object)value),
            source.Buffer(Interval).Select(static value => (object)value),
            source.CombineLatest(text).Select(static value => (object)value),
            source.CombineLatest(text, static (a, b) => (object)$"{a}{b}"),
            source.CombineLatest(text, flag).Select(static value => (object)value),
            source.CombineLatest(text, flag, static (a, b, c) => (object)$"{a}{b}{c}"),
            source.CombineLatest(text, flag, number).Select(static value => (object)value),
            source.CombineLatest(text, flag, number, static (a, b, c, d) => (object)$"{a}{b}{c}{d}"),
            source.SelectMany(static value => ObservableExtensions.Return<object>(value)),
            source.Scan(string.Empty, static (text, value) => $"{text}{value}").Select(static value => (object)value),
        ];

        Assert.IsTrue(results.All(static result => result is IObservableWithLogger));
    }

    [TestMethod]
    public void SynchronousSubscribeExceptions_AreForwardedExactlyOnce()
    {
        AssertSubscribeFailure(new ThrowingObservable<int>().Buffer(2));
        AssertSubscribeFailure(new ThrowingObservable<int>().Buffer(Interval));
        AssertSubscribeFailure(new ThrowingObservable<int>().Buffer(Interval, 2));
        AssertSubscribeFailure(new ThrowingObservable<int>().CombineLatest(new ManualObservable<int>()));
        AssertSubscribeFailure(new ThrowingObservable<int>().CombineLatest(new ManualObservable<int>(), new ManualObservable<int>()));
        AssertSubscribeFailure(new ThrowingObservable<int>().CombineLatest(new ManualObservable<int>(), new ManualObservable<int>(), new ManualObservable<int>()));
        AssertSubscribeFailure(new ThrowingObservable<IObservable<int>>().Switch());
        AssertSubscribeFailure(new ThrowingObservable<int>().Merge(new ManualObservable<int>()));
        AssertSubscribeFailure(new IObservable<int>[] { new ThrowingObservable<int>() }.Merge());
        AssertSubscribeFailure(new ThrowingObservable<int>().SelectMany(static _ => ObservableExtensions.Return(1)));
        AssertSubscribeFailure(new ThrowingObservable<int>().Concat(new ManualObservable<int>()));
        AssertSubscribeFailure(new IObservable<int>[] { new ThrowingObservable<int>() }.Concat());
        AssertSubscribeFailure(new ThrowingObservable<int>().Scan(static (a, b) => a + b));
        AssertSubscribeFailure(new ThrowingObservable<int>().Scan(0, static (a, b) => a + b));
        AssertSubscribeFailure(new ThrowingObservable<int>().Retry(0));
        AssertSubscribeFailure(new ThrowingObservable<int>().Finally(static () => { }));
        AssertSubscribeFailure(new ThrowingObservable<int>().TakeUntil(new ManualObservable<int>()));
        AssertSubscribeFailure(new ThrowingObservable<int>().Delay(Interval, new ManualTimeProvider()));
        AssertSubscribeFailure(new ThrowingObservable<int>().Timeout(Interval, new ManualTimeProvider()));
        AssertSubscribeFailure(new ThrowingObservable<int>().Timeout(Interval, new ManualObservable<int>(), new ManualTimeProvider()));
        AssertSubscribeFailure(new ThrowingObservable<int>().Sample(Interval, new ManualTimeProvider()));
    }

    [TestMethod]
    public void ExplicitDisposal_SilencesSourcesAndReleasesResources()
    {
        VerifyDisposal(static source => source.Buffer(2));
        VerifyDisposal(static source => source.Scan(static (a, b) => a + b));
        VerifyDisposal(static source => source.Retry(1));
        VerifyDisposal(static source => source.Finally(static () => { }));
        VerifyDisposal(source => source.Delay(Interval, new ManualTimeProvider()));
        VerifyDisposal(source => source.Timeout(Interval, new ManualTimeProvider()));
        VerifyDisposal(source => source.Sample(Interval, new ManualTimeProvider()));

        var first = new ManualObservable<int>();
        var second = new ManualObservable<int>();
        var values = new List<int>();
        var subscription = first.CombineLatest(second, static (a, b) => a + b).Subscribe(values.Add);
        subscription.Dispose();
        first.Emit(1);
        second.Emit(2);
        Assert.IsEmpty(values);
        Assert.AreEqual(1, first.DisposalCount);
        Assert.AreEqual(1, second.DisposalCount);

        var outer = new ManualObservable<IObservable<int>>();
        var inner = new ManualObservable<int>();
        var switched = new List<int>();
        var switchSubscription = outer.Switch().Subscribe(switched.Add);
        outer.Emit(inner);
        switchSubscription.Dispose();
        inner.Emit(1);
        Assert.IsEmpty(switched);
        Assert.AreEqual(1, outer.DisposalCount);
        Assert.AreEqual(1, inner.DisposalCount);
    }

    [TestMethod]
    public void CompetingTerminalSignals_FirstTerminalWinsAndResourcesAreReleasedOnce()
    {
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var source = new ConcurrentObservable<int>();
            var other = new ConcurrentObservable<int>();
            var terminals = 0;
            using var subscription = source.TakeUntil(other).Subscribe(
                _ => { },
                _ => Interlocked.Increment(ref terminals),
                () => Interlocked.Increment(ref terminals));

            Parallel.Invoke(
                () => source.Error(new TestException("source")),
                () => other.Next(1),
                source.Complete);

            Assert.AreEqual(1, terminals);
            Assert.AreEqual(1, source.DisposalCount);
            Assert.AreEqual(1, other.DisposalCount);
        }
    }

    [TestMethod]
    public void TimeoutSwitch_RaceSelectsOneSourceAndDisposesBothSubscriptions()
    {
        var time = new ManualTimeProvider();
        var source = new ManualObservable<int>();
        var fallback = new ManualObservable<int>();
        var values = new List<int>();
        var terminals = 0;
        using var subscription = source.Timeout(Interval, fallback, time).Subscribe(
            values.Add,
            _ => terminals++,
            () => terminals++);

        time.Advance(Interval);
        source.Emit(1);
        fallback.Emit(2);
        fallback.Complete();
        source.Complete();

        CollectionAssert.AreEqual(new[] { 2 }, values);
        Assert.AreEqual(1, terminals);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.AreEqual(1, fallback.DisposalCount);
    }

    private static void AssertSubscribeFailure<T>(IObservable<T> observable)
    {
        var errors = new List<Exception>();
        Exception? exception = null;
        try
        {
            observable.Subscribe(_ => Assert.Fail(), errors.Add);
        }
        catch (Exception error)
        {
            exception = error;
        }

        Assert.IsNull(exception);
        Assert.HasCount(1, errors);
        Assert.AreEqual("subscribe", errors[0].Message);
    }

    private static void VerifyDisposal<T>(Func<ManualObservable<int>, IObservable<T>> create)
    {
        var source = new ManualObservable<int>();
        var values = new List<T>();
        var subscription = create(source).Subscribe(values.Add);

        subscription.Dispose();
        source.Emit(1);

        Assert.IsEmpty(values);
        Assert.AreEqual(1, source.DisposalCount);
    }

    private sealed class ThrowingObservable<T> : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer) => throw new TestException("subscribe");
    }

    private sealed class ConcurrentObservable<T> : IObservable<T>
    {
        private readonly Lock _gate = new();
        private readonly List<IObserver<T>> _observers = [];
        private int _disposalCount;

        public int DisposalCount => Volatile.Read(ref _disposalCount);

        public IDisposable Subscribe(IObserver<T> observer)
        {
            lock (_gate)
                _observers.Add(observer);
            return new CallbackDisposable(() =>
            {
                lock (_gate)
                    _observers.Remove(observer);
                Interlocked.Increment(ref _disposalCount);
            });
        }

        public void Next(T value) => Notify(observer => observer.OnNext(value));
        public void Error(Exception error) => Notify(observer => observer.OnError(error));
        public void Complete() => Notify(static observer => observer.OnCompleted());

        private void Notify(Action<IObserver<T>> action)
        {
            IObserver<T>[] observers;
            lock (_gate)
                observers = _observers.ToArray();
            foreach (var observer in observers)
                action(observer);
        }
    }
}
