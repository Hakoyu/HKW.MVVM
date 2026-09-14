using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableAsPropertyHelperTests
{
    [TestMethod]
    public void ComputedProperty_ReceivesInitialAndChangedValues()
    {
        using var person = new ComputedPerson { FirstName = "Ada", LastName = "Lovelace" };
        var changedProperties = new List<string?>();
        person.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.AreEqual("Ada Lovelace", person.FullName);
        person.FirstName = "Grace";

        Assert.AreEqual("Grace Lovelace", person.FullName);
        CollectionAssert.Contains(changedProperties, nameof(ComputedPerson.FullName));
    }

    [TestMethod]
    public void SourceValue_RaisesHelperAndOwnerNotificationsInOrder()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(owner, x => x.Result, initialValue: "Initial");
        var events = new List<string>();
        helper.PropertyChanging += (_, args) =>
            events.Add($"HelperChanging:{args.PropertyName}:{helper.Value}");
        owner.PropertyChanging += (_, args) =>
            events.Add($"OwnerChanging:{args.PropertyName}:{helper.Value}");
        helper.PropertyChanged += (_, args) =>
            events.Add($"HelperChanged:{args.PropertyName}:{helper.Value}");
        owner.PropertyChanged += (_, args) =>
            events.Add($"OwnerChanged:{args.PropertyName}:{helper.Value}");

        source.Emit("Updated");

        CollectionAssert.AreEqual(
            new[]
            {
                "HelperChanging:Value:Initial",
                "OwnerChanging:Result:Initial",
                "HelperChanged:Value:Updated",
                "OwnerChanged:Result:Updated",
            },
            events
        );
    }

    [TestMethod]
    public void EqualSourceValues_DoNotRaiseDuplicateNotifications()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(
            owner,
            nameof(PropertyOwner.Result),
            initialValue: "Initial"
        );
        var changedCount = 0;
        owner.PropertyChanged += (_, _) => changedCount++;

        source.Emit("Initial");
        source.Emit("Updated");
        source.Emit("Updated");

        Assert.AreEqual("Updated", helper.Value);
        Assert.AreEqual(1, changedCount);
    }

    [TestMethod]
    public void SchedulerOverload_CurrentUsesCapturedSynchronizationContext()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        var context = new QueuedSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        ObservableAsPropertyHelper<string> helper;
        try
        {
            helper = source.ToProperty(
                owner,
                owner => owner.Result,
                initialValue: "Initial",
                ObservableSchedulers.Current
            );
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        using (helper)
        {
            source.Emit("Updated");

            Assert.AreEqual("Initial", helper.Value);
            Assert.AreEqual(1, context.PendingCount);
            context.RunAll();
            Assert.AreEqual("Updated", helper.Value);
        }
    }

    [TestMethod]
    public async Task SchedulerOverload_ThreadPoolDispatchesPropertyChange()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(
            owner,
            nameof(PropertyOwner.Result),
            initialValue: "Initial",
            ObservableSchedulers.ThreadPool
        );
        var notification = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        owner.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PropertyOwner.Result))
            {
                notification.TrySetResult(helper.Value);
            }
        };

        source.Emit("Updated");

        Assert.AreEqual("Updated", await notification.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public async Task SchedulerOverload_ThreadPoolPreservesFinalValue()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(
            owner,
            nameof(PropertyOwner.Result),
            ObservableSchedulers.ThreadPool
        );
        var notification = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        owner.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PropertyOwner.Result) && helper.Value == "99")
                notification.TrySetResult(true);
        };

        for (var value = 0; value < 100; value++)
            source.Emit(value.ToString());

        await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("99", helper.Value);
    }

    [TestMethod]
    public void SchedulerOverload_RejectsUnknownScheduler()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            source.ToProperty(owner, owner => owner.Result, (ObservableSchedulers)999)
        );
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            source.ToProperty(owner, nameof(PropertyOwner.Result), (ObservableSchedulers)999)
        );
    }

    [TestMethod]
    public void DeferredSubscription_StartsOnFirstValueRead()
    {
        var person = new Person { FirstName = "Before read" };
        var owner = new PropertyOwner();
        var source = person.WhenAnyValue(x => x.FirstName);
        using var helper = source.ToProperty(
            owner,
            x => x.Result,
            initialValue: "Initial",
            deferSubscription: true
        );

        person.FirstName = "Still before read";
        Assert.AreEqual("Still before read", helper.Value);
        person.FirstName = "After read";

        Assert.AreEqual("After read", helper.Value);
    }

    [TestMethod]
    public void DeferredSubscription_WithManualSource_SubscribesOnlyOnce()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(
            owner,
            x => x.Result,
            initialValue: "Initial",
            deferSubscription: true
        );

        Assert.AreEqual(0, source.SubscriptionCount);
        Assert.AreEqual("Initial", helper.Value);
        Assert.AreEqual("Initial", helper.Value);

        Assert.AreEqual(1, source.SubscriptionCount);
    }

    [TestMethod]
    public void SynchronizationContext_QueuesSourceNotifications()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        var context = new QueuedSynchronizationContext();
        using var helper = source.ToProperty(
            owner,
            x => x.Result,
            initialValue: "Initial",
            synchronizationContext: context
        );

        source.Emit("Updated");

        Assert.AreEqual(1, context.PendingCount);
        Assert.AreEqual("Initial", helper.Value);
        context.RunAll();
        Assert.AreEqual("Updated", helper.Value);
    }

    [TestMethod]
    public void SourceError_IsPublishedByThrownExceptions()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(owner, x => x.Result, initialValue: string.Empty);
        Exception? received = null;
        using var subscription = helper.ThrownExceptions.Subscribe(error => received = error);
        var expected = new TestException("Source failed.");

        source.Fail(expected);

        Assert.AreSame(expected, received);
    }

    [TestMethod]
    public void Dispose_UnsubscribesSourceAndCompletesThrownExceptions()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        var helper = source.ToProperty(owner, x => x.Result, initialValue: string.Empty);
        var completed = false;
        using var exceptionSubscription = helper.ThrownExceptions.Subscribe(
            _ => { },
            _ => { },
            () => completed = true
        );

        helper.Dispose();
        helper.Dispose();
        source.Emit("Ignored");

        Assert.AreEqual(1, source.DisposalCount);
        Assert.IsTrue(completed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = helper.Value);
    }

    [TestMethod]
    public void DisposedDeferredHelper_NeverSubscribes()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        var helper = source.ToProperty(
            owner,
            x => x.Result,
            initialValue: string.Empty,
            deferSubscription: true
        );

        helper.Dispose();

        Assert.AreEqual(0, source.SubscriptionCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = helper.Value);
    }

    [TestMethod]
    public void NameOverload_RaisesNotificationForProvidedProperty()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(
            owner,
            nameof(PropertyOwner.OtherResult),
            initialValue: string.Empty
        );
        string? changedProperty = null;
        owner.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        source.Emit("Updated");

        Assert.AreEqual(nameof(PropertyOwner.OtherResult), changedProperty);
        Assert.AreEqual("Updated", helper.Value);
    }

    [TestMethod]
    public void PropertyNotifier_IsPreferredOverReflection()
    {
        var source = new ManualObservable<string>();
        var owner = new TrackingPropertyOwner();
        using var helper = source.ToProperty(owner, x => x.Result, initialValue: string.Empty);

        source.Emit("Updated");

        Assert.AreEqual(1, owner.ChangingNotificationCount);
        Assert.AreEqual(1, owner.ChangedNotificationCount);
    }

    [TestMethod]
    public void PlainObservableObject_UsesCachedDelegatesForNotifications()
    {
        var source = new ManualObservable<string>();
        var owner = new PlainPropertyOwner();
        using var helper = source.ToProperty(owner, x => x.Result, initialValue: string.Empty);
        var events = new List<string>();
        owner.PropertyChanging += (_, args) => events.Add($"Changing:{args.PropertyName}");
        owner.PropertyChanged += (_, args) => events.Add($"Changed:{args.PropertyName}");

        source.Emit("Updated");

        CollectionAssert.AreEqual(new[] { "Changing:Result", "Changed:Result" }, events);
    }

    [TestMethod]
    public void InvalidPropertyExpression_ThrowsArgumentException()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();

        Assert.ThrowsExactly<ArgumentException>(() =>
            source.ToProperty(owner, x => x.Result + x.OtherResult)
        );
    }

    [TestMethod]
    public void InvalidArguments_Throw()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ObservableAsPropertyHelperExtensions.ToProperty<PropertyOwner, string>(
                null!,
                owner,
                nameof(PropertyOwner.Result)
            )
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.ToProperty((PropertyOwner)null!, nameof(PropertyOwner.Result))
        );
        Assert.ThrowsExactly<ArgumentException>(() => source.ToProperty(owner, string.Empty));
    }

    [TestMethod]
    public void DeferredSubscription_IsTrackedByIsSubscribed()
    {
        var source = new ManualObservable<string>();
        var owner = new PropertyOwner();
        using var helper = source.ToProperty(
            owner,
            x => x.Result,
            initialValue: "Initial",
            deferSubscription: true
        );

        Assert.IsFalse(helper.IsSubscribed);

        _ = helper.Value;

        Assert.IsTrue(helper.IsSubscribed);
    }

    [TestMethod]
    public void SubscriptionFailure_DoesNotPoisonDeferredHelper()
    {
        var attempts = 0;
        var source = new SynchronousObservable<string>(observer =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TestException("First subscribe failed.");
            }

            observer.OnNext("Recovered");
        });

        var owner = new PropertyOwner();
        using var helper = source.ToProperty(
            owner,
            x => x.Result,
            initialValue: "Initial",
            deferSubscription: true
        );

        Assert.ThrowsExactly<TestException>(() => _ = helper.Value);
        Assert.IsFalse(helper.IsSubscribed);

        Assert.AreEqual("Recovered", helper.Value);
        Assert.IsTrue(helper.IsSubscribed);
        Assert.AreEqual(2, attempts);
    }
}
