using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableForPropertyTests
{
    [TestMethod]
    public void ObservableForProperty_SupportsInitialAfterBeforeAndDistinctOptions()
    {
        var model = new ObservationModel { Value = 1 };
        var after = new List<int>();
        var before = new List<int>();
        var all = new List<int>();
        using var afterSubscription = model.ObservableForProperty(x => x.Value).Subscribe(x => after.Add(x.Value));
        using var beforeSubscription = model.ObservableForProperty(x => x.Value, beforeChange: true).Subscribe(x => before.Add(x.Value));
        using var allSubscription = model.ObservableForProperty(x => x.Value, skipInitial: false, isDistinct: false).Subscribe(x => all.Add(x.Value));

        model.Value = 2;
        model.RaiseChanged(nameof(ObservationModel.Value));

        CollectionAssert.AreEqual(new[] { 2 }, after);
        CollectionAssert.AreEqual(new[] { 1 }, before);
        CollectionAssert.AreEqual(new[] { 1, 2, 2 }, all);
    }

    [TestMethod]
    public void ObservableForProperty_RebindsNestedAndNullPathsAndDetachesOldNodes()
    {
        var oldChild = new ObservationChild { Value = "old" };
        var model = new ObservationModel { Child = oldChild };
        var values = new List<string>();
        using var subscription = model.ObservableForProperty(x => x.Child!.Value, skipInitial: false).Subscribe(x => values.Add(x.Value));

        oldChild.Value = "first";
        model.Child = null;
        oldChild.Value = "ignored";
        model.Child = new ObservationChild { Value = "new" };
        model.Child.Value = "final";

        CollectionAssert.AreEqual(new[] { "old", "first", "new", "final" }, values);
    }

    [TestMethod]
    public void ObservableForProperty_DisposeDetachesAndPreservesLogger()
    {
        var model = new ObservationModel { Value = 1 };
        var observable = model.ObservableForProperty(x => x.Value, skipInitial: false);
        Assert.IsInstanceOfType<IObservableWithLogger>(observable);
        var values = new List<int>();
        var subscription = observable.Subscribe(x => values.Add(x.Value));

        subscription.Dispose();
        subscription.Dispose();
        model.Value = 2;

        CollectionAssert.AreEqual(new[] { 1 }, values);
    }

    [TestMethod]
    public void ObservableForProperty_SerializesConcurrentPropertyNotifications()
    {
        var model = new ObservationModel();
        var firstEntered = new ManualResetEventSlim();
        var releaseFirst = new ManualResetEventSlim();
        var values = new List<int>();
        var activeCallbacks = 0;
        var concurrentCallback = false;
        using var subscription = model.ObservableForProperty(x => x.Value).Subscribe(observation =>
        {
            if (Interlocked.Increment(ref activeCallbacks) != 1)
                concurrentCallback = true;
            if (observation.Value == 1)
            {
                firstEntered.Set();
                releaseFirst.Wait();
            }
            lock (values)
                values.Add(observation.Value);
            Interlocked.Decrement(ref activeCallbacks);
        });

        var firstChange = Task.Run(() => model.Value = 1);
        Assert.IsTrue(firstEntered.Wait(TimeSpan.FromSeconds(5)));
        var secondChange = Task.Run(() => model.Value = 2);
        Assert.IsTrue(secondChange.Wait(TimeSpan.FromSeconds(5)));
        releaseFirst.Set();
        Assert.IsTrue(firstChange.Wait(TimeSpan.FromSeconds(5)));

        Assert.IsFalse(concurrentCallback);
        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
    }

    [TestMethod]
    public void WhenAnyObservable_SwitchesLatestAndCombinesOneToFourProperties()
    {
        var oldStream = new ManualObservable<int>();
        var newStream = new ManualObservable<int>();
        var second = new ManualObservable<string>();
        var third = new ManualObservable<bool>();
        var fourth = new ManualObservable<double>();
        var model = new ObservableHolder
        {
            First = oldStream,
            Second = second,
            Third = third,
            Fourth = fourth,
        };
        var single = new List<int>();
        var pairs = new List<(int, string)>();
        var triples = new List<(int, string, bool)>();
        var quadruples = new List<(int, string, bool, double)>();
        var combined = new List<string>();
        using var singleSubscription = model.WhenAnyObservable(x => x.First).Subscribe(single.Add);
        using var pairSubscription = model
            .WhenAnyObservable(x => x.First, x => x.Second)
            .Subscribe(pairs.Add);
        using var tripleSubscription = model
            .WhenAnyObservable(x => x.First, x => x.Second, x => x.Third)
            .Subscribe(triples.Add);
        using var quadrupleSubscription = model
            .WhenAnyObservable(x => x.First, x => x.Second, x => x.Third, x => x.Fourth)
            .Subscribe(quadruples.Add);
        using var combinedSubscription = model.WhenAnyObservable(
            x => x.First,
            x => x.Second,
            x => x.Third,
            x => x.Fourth,
            (one, two, three, four) => $"{one}:{two}:{three}:{four}"
        ).Subscribe(combined.Add);

        oldStream.Emit(1);
        second.Emit("two");
        third.Emit(true);
        fourth.Emit(4.5);
        model.First = newStream;
        oldStream.Emit(9);
        newStream.Emit(2);

        CollectionAssert.AreEqual(new[] { 1, 2 }, single);
        CollectionAssert.AreEqual(new[] { (1, "two"), (2, "two") }, pairs);
        CollectionAssert.AreEqual(new[] { (1, "two", true), (2, "two", true) }, triples);
        CollectionAssert.AreEqual(
            new[] { (1, "two", true, 4.5), (2, "two", true, 4.5) },
            quadruples
        );
        CollectionAssert.AreEqual(new[] { "1:two:True:4.5", "2:two:True:4.5" }, combined);
    }

    private sealed class ObservationModel : ObservableObjectEx, IEnableLoggerWithLogger
    {
        public ILogger Logger => NullLogger.Instance;

        public int Value
        {
            get => field;
            set => SetProperty(ref field, value);
        }

        public ObservationChild? Child
        {
            get => field;
            set => SetProperty(ref field, value);
        }

        public void RaiseChanged(string propertyName) => NotifyPropertyChanged(propertyName);
    }

    private sealed class ObservationChild : ObservableObjectEx
    {
        public string Value
        {
            get => field;
            set => SetProperty(ref field, value);
        } = string.Empty;
    }

    private sealed class ObservableHolder : ObservableObjectEx
    {
        public IObservable<int> First
        {
            get => field;
            set => SetProperty(ref field, value);
        } = null!;

        public IObservable<string> Second
        {
            get => field;
            set => SetProperty(ref field, value);
        } = null!;

        public IObservable<bool> Third
        {
            get => field;
            set => SetProperty(ref field, value);
        } = null!;

        public IObservable<double> Fourth
        {
            get => field;
            set => SetProperty(ref field, value);
        } = null!;
    }

    private sealed class ManualObservable<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new Subscription(_observers, observer);
        }

        public void Emit(T value)
        {
            foreach (var observer in _observers.ToArray())
                observer.OnNext(value);
        }

        private sealed class Subscription(List<IObserver<T>> observers, IObserver<T> observer) : IDisposable
        {
            public void Dispose() => observers.Remove(observer);
        }
    }
}
