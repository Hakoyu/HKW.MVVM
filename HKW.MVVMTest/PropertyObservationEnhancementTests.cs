using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class PropertyObservationEnhancementTests
{
    [TestMethod]
    public void WhenAnyValue_DirectOptionsApplyComparerAndSkipInitial()
    {
        var model = new ObservationOptionsModel { Text = "Alpha" };
        var values = new List<string?>();
        using var subscription = model.WhenAnyValue(
            x => x.Text,
            new WhenAnyValueOptions<string?>
            {
                Comparer = StringComparer.OrdinalIgnoreCase,
                SkipInitial = true,
            }
        ).Subscribe(values.Add);

        model.Text = "ALPHA";
        model.Text = "Beta";

        CollectionAssert.AreEqual(new string?[] { "Beta" }, values);
    }

    [TestMethod]
    public void WhenAnyValue_DirectComparerErrorTerminatesThroughOnError()
    {
        var model = new ObservationOptionsModel { Text = "Alpha" };
        var values = new List<string?>();
        Exception? error = null;
        using var subscription = model.WhenAnyValue(
            x => x.Text,
            new WhenAnyValueOptions<string?> { Comparer = new ThrowingComparer<string?>() }
        ).Subscribe(values.Add, exception => error = exception);

        model.Text = "Beta";
        model.Text = "Gamma";

        CollectionAssert.AreEqual(new string?[] { "Alpha" }, values);
        Assert.IsInstanceOfType<TestException>(error);
    }

    [TestMethod]
    public void WhenAnyValue_NestedOptionsRebindAndDistinguishFallbackFromNullLeaf()
    {
        var oldChild = new ObservationOptionsChild { Text = null };
        var model = new ObservationOptionsModel { Child = oldChild };
        var values = new List<string?>();
        using var subscription = model.WhenAnyValue(
            x => x.Child!.Text,
            new WhenAnyValueOptions<string?>
            {
                Comparer = StringComparer.OrdinalIgnoreCase,
                PathUnavailableFallback = static () => null,
            }
        ).Subscribe(values.Add);

        oldChild.Text = "value";
        oldChild.Text = "VALUE";
        model.Child = null;
        oldChild.Text = "ignored";
        model.Child = new ObservationOptionsChild { Text = null };
        model.Child.Text = "final";

        CollectionAssert.AreEqual(
            new string?[] { null, "value", null, null, "final" },
            values
        );
    }

    [TestMethod]
    public void WhenAnyValue_NestedSkipInitialSeedsComparisonAndRebinds()
    {
        var model = new ObservationOptionsModel
        {
            Child = new ObservationOptionsChild { Text = "same" },
        };
        var values = new List<string?>();
        using var subscription = model.WhenAnyValue(
            x => x.Child!.Text,
            new WhenAnyValueOptions<string?> { SkipInitial = true }
        ).Subscribe(values.Add);

        model.Child.Text = "same";
        model.Child = new ObservationOptionsChild { Text = "next" };

        CollectionAssert.AreEqual(new string?[] { "next" }, values);
    }

    [TestMethod]
    public void ChangingAndChanged_WrapObservableForPropertyOptions()
    {
        var model = new ObservationOptionsModel { Text = "Alpha" };
        var changing = new List<string?>();
        var changed = new List<string?>();
        using var changingSubscription = model.WhenAnyChanging(
            x => x.Text,
            comparer: StringComparer.OrdinalIgnoreCase
        ).Subscribe(x => changing.Add(x.Value));
        using var changedSubscription = model.WhenAnyChanged(
            x => x.Text,
            comparer: StringComparer.OrdinalIgnoreCase
        ).Subscribe(x => changed.Add(x.Value));

        model.Text = "Beta";
        model.Text = "BETA";

        CollectionAssert.AreEqual(new string?[] { "Alpha", "Beta" }, changing);
        CollectionAssert.AreEqual(new string?[] { "Beta" }, changed);
    }

    [TestMethod]
    public void WhenAnyValue_CombinesFiveThroughEightProperties()
    {
        var model = new EightValueModel();
        var five = new List<(int, int, int, int, int)>();
        var six = new List<(int, int, int, int, int, int)>();
        var seven = new List<(int, int, int, int, int, int, int)>();
        var eight = new List<(int, int, int, int, int, int, int, int)>();
        var projected = new List<int>();
        using var s5 = model.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5).Subscribe(five.Add);
        using var s6 = model.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6).Subscribe(six.Add);
        using var s7 = model.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7).Subscribe(seven.Add);
        using var s8 = model.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7, x => x.P8).Subscribe(eight.Add);
        using var projectedSubscription = model.WhenAnyValue(
            x => x.P1, x => x.P2, x => x.P3, x => x.P4,
            x => x.P5, x => x.P6, x => x.P7, x => x.P8,
            static (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h
        ).Subscribe(projected.Add);

        model.P8 = 80;
        model.P5 = 50;

        CollectionAssert.AreEqual(new[] { (1, 2, 3, 4, 5), (1, 2, 3, 4, 50) }, five);
        CollectionAssert.AreEqual(new[] { (1, 2, 3, 4, 5, 6), (1, 2, 3, 4, 50, 6) }, six);
        CollectionAssert.AreEqual(new[] { (1, 2, 3, 4, 5, 6, 7), (1, 2, 3, 4, 50, 6, 7) }, seven);
        CollectionAssert.AreEqual(new[] { (1, 2, 3, 4, 5, 6, 7, 8), (1, 2, 3, 4, 5, 6, 7, 80), (1, 2, 3, 4, 50, 6, 7, 80) }, eight);
        CollectionAssert.AreEqual(new[] { 36, 108, 153 }, projected);
    }

    [TestMethod]
    public void WhenAnyObservable_CombinesFiveThroughEightAndRebindsLatest()
    {
        var streams = Enumerable.Range(1, 9).Select(_ => new TestStream<int>()).ToArray();
        var replacement = new TestStream<int>();
        var model = new EightObservableModel(streams);
        var counts = new int[4];
        var projected = new List<int>();
        using var s5 = model.WhenAnyObservable(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5).Subscribe(_ => counts[0]++);
        using var s6 = model.WhenAnyObservable(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6).Subscribe(_ => counts[1]++);
        using var s7 = model.WhenAnyObservable(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7).Subscribe(_ => counts[2]++);
        using var s8 = model.WhenAnyObservable(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7, x => x.P8).Subscribe(_ => counts[3]++);
        using var projectedSubscription = model.WhenAnyObservable(
            x => x.P1, x => x.P2, x => x.P3, x => x.P4,
            x => x.P5, x => x.P6, x => x.P7, x => x.P8,
            static (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h
        ).Subscribe(projected.Add);

        for (var index = 0; index < 8; index++)
            streams[index].Emit(index + 1);
        model.P1 = replacement;
        streams[0].Emit(100);
        replacement.Emit(10);

        CollectionAssert.AreEqual(new[] { 2, 2, 2, 2 }, counts);
        CollectionAssert.AreEqual(new[] { 36, 45 }, projected);
    }

    private sealed class ObservationOptionsModel : ObservableObjectEx
    {
        public string? Text { get => field; set => SetProperty(ref field, value); }
        public ObservationOptionsChild? Child { get => field; set => SetProperty(ref field, value); }
    }

    private sealed class ObservationOptionsChild : ObservableObjectEx
    {
        public string? Text { get => field; set => SetProperty(ref field, value); }
    }

    private sealed class EightValueModel : ObservableObjectEx
    {
        public int P1 { get => field; set => SetProperty(ref field, value); } = 1;
        public int P2 { get => field; set => SetProperty(ref field, value); } = 2;
        public int P3 { get => field; set => SetProperty(ref field, value); } = 3;
        public int P4 { get => field; set => SetProperty(ref field, value); } = 4;
        public int P5 { get => field; set => SetProperty(ref field, value); } = 5;
        public int P6 { get => field; set => SetProperty(ref field, value); } = 6;
        public int P7 { get => field; set => SetProperty(ref field, value); } = 7;
        public int P8 { get => field; set => SetProperty(ref field, value); } = 8;
    }

    private sealed class EightObservableModel(TestStream<int>[] streams) : ObservableObjectEx
    {
        public IObservable<int> P1 { get => field; set => SetProperty(ref field, value); } = streams[0];
        public IObservable<int> P2 { get => field; set => SetProperty(ref field, value); } = streams[1];
        public IObservable<int> P3 { get => field; set => SetProperty(ref field, value); } = streams[2];
        public IObservable<int> P4 { get => field; set => SetProperty(ref field, value); } = streams[3];
        public IObservable<int> P5 { get => field; set => SetProperty(ref field, value); } = streams[4];
        public IObservable<int> P6 { get => field; set => SetProperty(ref field, value); } = streams[5];
        public IObservable<int> P7 { get => field; set => SetProperty(ref field, value); } = streams[6];
        public IObservable<int> P8 { get => field; set => SetProperty(ref field, value); } = streams[7];
    }

    private sealed class ThrowingComparer<T> : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y) => throw new TestException("Comparison failed.");

        public int GetHashCode(T value) => EqualityComparer<T>.Default.GetHashCode(value!);
    }

    private sealed class TestStream<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new CallbackDisposable(() => _observers.Remove(observer));
        }
        public void Emit(T value)
        {
            foreach (var observer in _observers.ToArray())
                observer.OnNext(value);
        }
    }
}
