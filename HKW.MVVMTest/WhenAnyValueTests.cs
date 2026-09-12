using System.Linq.Expressions;
using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class WhenAnyValueTests
{
    [TestMethod]
    public void SingleProperty_EmitsInitialAndDistinctChangedValues()
    {
        var person = new Person { FirstName = "Ada" };
        var values = new List<string>();
        using var subscription = person.WhenAnyValue(x => x.FirstName).Subscribe(values.Add);

        person.FirstName = "Grace";
        person.RaiseChanged(nameof(Person.FirstName));
        person.FirstName = "Katherine";

        CollectionAssert.AreEqual(new[] { "Ada", "Grace", "Katherine" }, values);
    }

    [TestMethod]
    public void PlainINotifyPropertyChanged_IsSupported()
    {
        var model = new PlainNotifyModel { Name = "Initial" };
        var values = new List<string>();
        using var subscription = model.WhenAnyValue(x => x.Name).Subscribe(values.Add);

        model.Name = "Updated";

        CollectionAssert.AreEqual(new[] { "Initial", "Updated" }, values);
    }

    [TestMethod]
    public void EmptyPropertyName_ReevaluatesObservedProperty()
    {
        var model = new PlainNotifyModel { Name = "Initial" };
        var values = new List<string>();
        using var subscription = model.WhenAnyValue(x => x.Name).Subscribe(values.Add);

        model.Name = "Updated";
        model.Raise(null);

        CollectionAssert.AreEqual(new[] { "Initial", "Updated" }, values);
    }

    [TestMethod]
    public void UnrelatedPropertyNotification_IsIgnored()
    {
        var model = new PlainNotifyModel { Name = "Initial" };
        var values = new List<string>();
        using var subscription = model.WhenAnyValue(x => x.Name).Subscribe(values.Add);

        model.Raise("Other");

        CollectionAssert.AreEqual(new[] { "Initial" }, values);
    }

    [TestMethod]
    public void Observable_IsCold_AndEachSubscriberReceivesCurrentValue()
    {
        var person = new Person { Age = 10 };
        var observable = person.WhenAnyValue(x => x.Age);
        var first = new List<int>();
        var second = new List<int>();

        using var firstSubscription = observable.Subscribe(first.Add);
        person.Age = 11;
        using var secondSubscription = observable.Subscribe(second.Add);
        person.Age = 12;

        CollectionAssert.AreEqual(new[] { 10, 11, 12 }, first);
        CollectionAssert.AreEqual(new[] { 11, 12 }, second);
    }

    [TestMethod]
    public void DisposingSubscription_StopsNotifications()
    {
        var person = new Person { Age = 10 };
        var values = new List<int>();
        var subscription = person.WhenAnyValue(x => x.Age).Subscribe(values.Add);

        subscription.Dispose();
        subscription.Dispose();
        person.Age = 11;

        CollectionAssert.AreEqual(new[] { 10 }, values);
    }

    [TestMethod]
    public void NestedProperty_RebindsAndStopsListeningToOldObject()
    {
        var oldAddress = new Address { City = "London" };
        var newAddress = new Address { City = "Tokyo" };
        var person = new Person { Address = oldAddress };
        var cities = new List<string>();
        using var subscription = person.WhenAnyValue(x => x.Address!.City).Subscribe(cities.Add);

        oldAddress.City = "Paris";
        person.Address = newAddress;
        oldAddress.City = "Ignored";
        newAddress.City = "Kyoto";

        CollectionAssert.AreEqual(new[] { "London", "Paris", "Tokyo", "Kyoto" }, cities);
    }

    [TestMethod]
    public void NestedProperty_WhenIntermediateIsNull_SuppressesUntilPathRecovers()
    {
        var person = new Person { Address = new Address { City = "London" } };
        var cities = new List<string>();
        using var subscription = person.WhenAnyValue(x => x.Address!.City).Subscribe(cities.Add);

        person.Address = null;
        person.Address = new Address { City = "Tokyo" };

        CollectionAssert.AreEqual(new[] { "London", "Tokyo" }, cities);
    }

    [TestMethod]
    public void NestedProperty_WhenInitiallyNull_EmitsAfterPathBecomesAvailable()
    {
        var person = new Person();
        var cities = new List<string>();
        using var subscription = person.WhenAnyValue(x => x.Address!.City).Subscribe(cities.Add);

        Assert.IsEmpty(cities);
        person.Address = new Address { City = "Tokyo" };

        CollectionAssert.AreEqual(new[] { "Tokyo" }, cities);
    }

    [TestMethod]
    public void ReplacingIntermediateWithEqualFinalValue_DoesNotEmitButRebinds()
    {
        var replacement = new Address { City = "London" };
        var person = new Person { Address = new Address { City = "London" } };
        var cities = new List<string>();
        using var subscription = person.WhenAnyValue(x => x.Address!.City).Subscribe(cities.Add);

        person.Address = replacement;
        replacement.City = "Paris";

        CollectionAssert.AreEqual(new[] { "London", "Paris" }, cities);
    }

    [TestMethod]
    public void ThreeLevelNestedProperty_RebindsOnlyAffectedPathAndStopsListeningToOldObjects()
    {
        var oldCountry = new Country { Name = "United Kingdom" };
        var replacementCountry = new Country { Name = "Japan" };
        var oldAddress = new Address { Country = oldCountry };
        var replacementAddress = new Address { Country = replacementCountry };
        var person = new Person { Address = oldAddress };
        var names = new List<string>();
        using var subscription = person
            .WhenAnyValue(x => x.Address!.Country!.Name)
            .Subscribe(names.Add);

        oldCountry.Name = "France";
        oldAddress.Country = replacementCountry;
        oldCountry.Name = "Ignored country";
        replacementCountry.Name = "Japan updated";
        person.Address = replacementAddress;
        oldAddress.Country = oldCountry;
        replacementCountry.Name = "Japan final";

        CollectionAssert.AreEqual(
            new[] { "United Kingdom", "France", "Japan", "Japan updated", "Japan final" },
            names
        );
    }

    [TestMethod]
    public void TwoProperties_EmitsCombinedInitialAndUpdates()
    {
        var person = new Person { FirstName = "Ada", LastName = "Lovelace" };
        var values = new List<string>();
        using var subscription = person
            .WhenAnyValue(x => x.FirstName, x => x.LastName, (first, last) => $"{first} {last}")
            .Subscribe(values.Add);

        person.FirstName = "Grace";
        person.LastName = "Hopper";

        CollectionAssert.AreEqual(
            new[] { "Ada Lovelace", "Grace Lovelace", "Grace Hopper" },
            values
        );
    }

    [TestMethod]
    public void TwoProperties_WithoutSelectorEmitsTuple()
    {
        var person = new Person { FirstName = "Ada", Age = 36 };
        var values = new List<(string, int)>();
        using var subscription = person
            .WhenAnyValue(x => x.FirstName, x => x.Age)
            .Subscribe(values.Add);

        person.Age = 37;

        CollectionAssert.AreEqual(new[] { ("Ada", 36), ("Ada", 37) }, values);
    }

    [TestMethod]
    public void ThreeProperties_EmitsWhenAnyInputChanges()
    {
        var person = new Person
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Age = 36,
        };
        var values = new List<string>();
        using var subscription = person
            .WhenAnyValue(
                x => x.FirstName,
                x => x.LastName,
                x => x.Age,
                (first, last, age) => $"{first} {last}:{age}"
            )
            .Subscribe(values.Add);

        person.Age = 37;

        CollectionAssert.AreEqual(new[] { "Ada Lovelace:36", "Ada Lovelace:37" }, values);
    }

    [TestMethod]
    public void ThreeProperties_WithoutSelectorEmitsTuple()
    {
        var person = new Person { FirstName = "Ada", LastName = "Lovelace", Age = 36 };
        var values = new List<(string, string, int)>();
        using var subscription = person
            .WhenAnyValue(x => x.FirstName, x => x.LastName, x => x.Age)
            .Subscribe(values.Add);

        person.FirstName = "Grace";

        CollectionAssert.AreEqual(
            new[] { ("Ada", "Lovelace", 36), ("Grace", "Lovelace", 36) },
            values
        );
    }

    [TestMethod]
    public void FourProperties_EmitsWhenAnyInputChanges()
    {
        var person = new Person
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Age = 36,
            Address = new Address { City = "London" },
        };
        var values = new List<string>();
        using var subscription = person
            .WhenAnyValue(
                x => x.FirstName,
                x => x.LastName,
                x => x.Age,
                x => x.Address!.City,
                (first, last, age, city) => $"{first} {last}:{age}:{city}"
            )
            .Subscribe(values.Add);

        person.Address!.City = "Paris";

        CollectionAssert.AreEqual(
            new[] { "Ada Lovelace:36:London", "Ada Lovelace:36:Paris" },
            values
        );
    }

    [TestMethod]
    public void FourProperties_WithoutSelectorEmitsTuple()
    {
        var person = new Person
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Age = 36,
            Address = new Address { City = "London" },
        };
        var values = new List<(string, string, int, string)>();
        using var subscription = person
            .WhenAnyValue(
                x => x.FirstName,
                x => x.LastName,
                x => x.Age,
                x => x.Address!.City
            )
            .Subscribe(values.Add);

        person.Address!.City = "Paris";

        CollectionAssert.AreEqual(
            new[]
            {
                ("Ada", "Lovelace", 36, "London"),
                ("Ada", "Lovelace", 36, "Paris"),
            },
            values
        );
    }

    [TestMethod]
    public void WhenAny_ProvidesSenderPropertyNameAndValue()
    {
        var person = new Person { FirstName = "Ada" };
        PropertyObservation<Person, string>? observation = null;
        using var subscription = person
            .WhenAny(x => x.FirstName, value => value)
            .Subscribe(value => observation = value);

        Assert.IsTrue(observation.HasValue);
        Assert.AreSame(person, observation.Value.Sender);
        Assert.AreEqual(nameof(Person.FirstName), observation.Value.PropertyName);
        Assert.AreEqual("Ada", observation.Value.Value);
    }

    [TestMethod]
    public void InvalidExpressions_ThrowArgumentException()
    {
        var person = new Person();

        Assert.ThrowsExactly<ArgumentException>(() =>
            person.WhenAnyValue(x => x.FirstName + x.LastName)
        );
        Assert.ThrowsExactly<ArgumentException>(() => person.WhenAnyValue(x => x.PublicField));
    }

    [TestMethod]
    public void GetterException_IsSentToObserver()
    {
        var model = new GetterFailureModel();
        Exception? received = null;

        using var subscription = model
            .WhenAnyValue(x => x.Broken)
            .Subscribe(_ => Assert.Fail("No value should be emitted."), error => received = error);

        Assert.IsInstanceOfType<TestException>(received);
        Assert.AreEqual("Getter failed.", received.Message);
    }

    [TestMethod]
    public void NullArguments_Throw()
    {
        var person = new Person();
        Expression<Func<Person, string>> expression = x => x.FirstName;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            WhenAnyExtensions.WhenAnyValue<Person, string>(null!, expression)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            person.WhenAnyValue((Expression<Func<Person, string>>)null!)
        );
    }
}
