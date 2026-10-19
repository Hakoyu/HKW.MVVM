using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class BindingDiagnosticsTests
{
    [TestMethod]
    public void BindTo_SourceErrorIsReportedOnceAndStopsBinding()
    {
        var source = new ManualObservable<int>();
        var target = new Person();
        using var binding = source.BindTo(target, (value, person) => person.Age = value);
        var errors = new List<Exception>();
        var completed = false;
        using var errorSubscription = binding.ThrownExceptions.Subscribe(errors.Add, _ => Assert.Fail(), () => completed = true);
        var expected = new TestException("Source failed.");

        source.Fail(expected);
        source.Fail(new TestException("Ignored."));
        source.Emit(42);

        Assert.HasCount(1, errors);
        Assert.AreSame(expected, errors[0]);
        Assert.AreEqual(0, target.Age);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.IsFalse(completed);
        binding.Dispose();
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void BindTo_SourceErrorSchedulesFallbackBeforePublishingError()
    {
        var source = new ManualObservable<int>();
        var target = new Person();
        var context = new QueuedSynchronizationContext();
        using var binding = source.BindTo(
            target,
            person => person.Age,
            static value => value,
            new BindingOptions<int>
            {
                Scheduler = ObservableSchedulers.FromSynchronizationContext(context),
                FallbackValue = -1,
            }
        );
        var errors = new List<Exception>();
        binding.ThrownExceptions.Subscribe(errors.Add);
        var expected = new TestException("Source failed.");

        source.Fail(expected);

        Assert.AreEqual(0, target.Age);
        Assert.IsEmpty(errors);
        context.RunAll();
        Assert.AreEqual(-1, target.Age);
        CollectionAssert.AreEqual(new[] { expected }, errors);
    }

    [TestMethod]
    public void BindingErrorStream_DeliversValueBeforeReentrantCompletion()
    {
        var source = new ManualObservable<int>();
        var binding = source.BindTo(new Person(), static (value, person) => person.Age = value);
        var notifications = new List<string>();
        using var first = binding.ThrownExceptions.Subscribe(_ => binding.Dispose());
        using var second = binding.ThrownExceptions.Subscribe(
            _ => notifications.Add("next"),
            _ => Assert.Fail(),
            () => notifications.Add("completed")
        );

        source.Fail(new TestException("Source failed."));

        CollectionAssert.AreEqual(new[] { "next", "completed" }, notifications);
    }

    [TestMethod]
    public void BindTo_ConverterErrorUsesFallbackReportsAndStops()
    {
        var source = new ManualObservable<string>();
        var target = new Person();
        using var binding = source.BindTo(
            target,
            person => person.Age,
            value => value == "bad" ? throw new TestException("Conversion failed.") : int.Parse(value),
            new BindingOptions<int> { FallbackValue = -1 }
        );
        Exception? error = null;
        using var errors = binding.ThrownExceptions.Subscribe(exception => error = exception);

        source.Emit("3");
        source.Emit("bad");
        source.Emit("4");

        Assert.AreEqual(-1, target.Age);
        Assert.IsInstanceOfType<TestException>(error);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void BindTo_SetterErrorDoesNotEscapeAndIsReplayedToLateSubscriber()
    {
        var source = new SynchronousObservable<int>(observer => observer.OnNext(1));
        var binding = source.BindTo(new Person(), static (_, _) => throw new TestException("Setter failed."));
        Exception? error = null;

        using var errors = binding.ThrownExceptions.Subscribe(exception => error = exception);

        Assert.IsInstanceOfType<TestException>(error);
        Assert.AreEqual(1, source.DisposalCount);
        binding.Dispose();
    }

    [TestMethod]
    public void BindTo_UsesSchedulerTargetNullAndComparer()
    {
        var source = new ManualObservable<string?>();
        var target = new Person();
        var context = new QueuedSynchronizationContext();
        using var binding = source.BindTo(
            target,
            person => person.FirstName,
            value => value!,
            new BindingOptions<string>
            {
                Scheduler = ObservableSchedulers.FromSynchronizationContext(context),
                Comparer = StringComparer.OrdinalIgnoreCase,
                TargetNullValue = "(null)",
            }
        );

        source.Emit(null);
        source.Emit("VALUE");
        source.Emit("value");
        Assert.AreEqual(string.Empty, target.FirstName);

        context.RunAll();

        Assert.AreEqual("VALUE", target.FirstName);
    }

    [TestMethod]
    [DataRow(BindingInitialSyncDirection.SourceToTarget, "Source", "Source")]
    [DataRow(BindingInitialSyncDirection.TargetToSource, "Target", "Target")]
    [DataRow(BindingInitialSyncDirection.None, "Source", "Target")]
    public void TwoWayBind_RespectsInitialSyncDirection(
        BindingInitialSyncDirection direction,
        string expectedSource,
        string expectedTarget
    )
    {
        var source = new Person { FirstName = "Source" };
        var target = new Person { FirstName = "Target" };
        using var binding = source.TwoWayBind(
            target,
            value => value.FirstName,
            value => value.FirstName,
            new TwoWayBindingOptions<string, string> { InitialSyncDirection = direction }
        );

        Assert.AreEqual(expectedSource, source.FirstName);
        Assert.AreEqual(expectedTarget, target.FirstName);
    }

    [TestMethod]
    public void TwoWayBind_ConverterFailureIsReportedAndStopsBothDirections()
    {
        var source = new Person { Age = 1 };
        var target = new Person();
        using var binding = source.TwoWayBind(
            target,
            value => value.Age,
            value => value.FirstName,
            value => value == 2 ? throw new TestException("Conversion failed.") : value.ToString(),
            int.Parse
        );
        Exception? error = null;
        using var errors = binding.ThrownExceptions.Subscribe(exception => error = exception);

        source.Age = 2;
        target.FirstName = "3";

        Assert.IsInstanceOfType<TestException>(error);
        Assert.AreEqual(2, source.Age);
        Assert.AreEqual("3", target.FirstName);
    }
}
