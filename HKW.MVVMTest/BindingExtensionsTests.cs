using System.Linq.Expressions;
using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class BindingExtensionsTests
{
    [TestMethod]
    public void BindTo_AssignmentActionAssignsValuesUntilDisposed()
    {
        var source = new ManualObservable<string>();
        var target = new Person();
        var binding = source.BindTo(target, (value, person) => person.FirstName = value);

        source.Emit("Ada");
        Assert.AreEqual("Ada", target.FirstName);

        binding.Dispose();
        source.Emit("Ignored");

        Assert.AreEqual("Ada", target.FirstName);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void BindTo_AssignmentActionSupportsArbitraryAssignmentLogic()
    {
        var source = new ManualObservable<int>();
        var target = new Person();
        using var binding = source.BindTo(
            target,
            (value, person) =>
            {
                person.Age = value;
                person.FirstName = $"Age: {value}";
            }
        );

        source.Emit(36);

        Assert.AreEqual(36, target.Age);
        Assert.AreEqual("Age: 36", target.FirstName);
    }

    [TestMethod]
    public void BindTo_AssignsValuesUntilDisposed()
    {
        var source = new ManualObservable<string>();
        var target = new Person();
        var binding = source.BindTo(target, x => x.FirstName);

        source.Emit("Ada");
        Assert.AreEqual("Ada", target.FirstName);

        binding.Dispose();
        binding.Dispose();
        source.Emit("Ignored");

        Assert.AreEqual("Ada", target.FirstName);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void BindTo_ConvertsValuesAndSupportsNestedProperties()
    {
        var source = new ManualObservable<int>();
        var target = new Person { Address = new Address() };
        using var binding = source.BindTo(target, x => x.Address!.City, value => $"City {value}");

        source.Emit(42);

        Assert.AreEqual("City 42", target.Address.City);
    }

    [TestMethod]
    public void TwoWayBind_InitializesTargetFromSourceAndUpdatesBothDirections()
    {
        var source = new Person { FirstName = "Source" };
        var target = new Person { FirstName = "Target" };
        using var binding = target.TwoWayBind(
            source,
            currentSource => currentSource.FirstName,
            currentTarget => currentTarget.FirstName
        );

        Assert.AreEqual("Source", target.FirstName);

        source.FirstName = "From source";
        Assert.AreEqual("From source", target.FirstName);

        target.FirstName = "From target";
        Assert.AreEqual("From target", source.FirstName);
    }

    [TestMethod]
    public void TwoWayBind_ConvertsValuesInBothDirections()
    {
        var source = new Person { Age = 36 };
        var target = new Person();
        using var binding = target.TwoWayBind(
            source,
            currentSource => currentSource.Age,
            currentTarget => currentTarget.FirstName,
            age => age.ToString(),
            text => int.Parse(text)
        );

        Assert.AreEqual("36", target.FirstName);

        target.FirstName = "37";
        Assert.AreEqual(37, source.Age);

        source.Age = 38;
        Assert.AreEqual("38", target.FirstName);
    }

    [TestMethod]
    public void TwoWayBind_AssignmentActionsUpdateBothDirections()
    {
        var source = new Person { Age = 36 };
        var target = new Person();
        using var binding = target.TwoWayBind(
            source,
            currentSource => currentSource.Age,
            currentTarget => currentTarget.FirstName,
            static (value, currentTarget) => currentTarget.FirstName = value.ToString(),
            static (value, currentSource) => currentSource.Age = int.Parse(value)
        );

        Assert.AreEqual("36", target.FirstName);

        target.FirstName = "37";
        Assert.AreEqual(37, source.Age);

        source.Age = 38;
        Assert.AreEqual("38", target.FirstName);
    }

    [TestMethod]
    public void TwoWayBind_AssignmentActionsCanUseCustomSetterLogic()
    {
        var source = new Person { FirstName = "Ada" };
        var target = new Person();
        using var binding = target.TwoWayBind(
            source,
            currentSource => currentSource.FirstName,
            currentTarget => currentTarget.FirstName,
            static (value, currentTarget) => currentTarget.FirstName = value.ToUpperInvariant(),
            static (value, currentSource) => currentSource.FirstName = value.Trim()
        );

        Assert.AreEqual("ADA", target.FirstName);

        target.FirstName = " Grace ";
        Assert.AreEqual("Grace", source.FirstName);
    }

    [TestMethod]
    public void TwoWayBind_DisposeStopsUpdatesInBothDirections()
    {
        var source = new Person { FirstName = "Initial" };
        var target = new Person();
        var binding = target.TwoWayBind(
            source,
            currentSource => currentSource.FirstName,
            currentTarget => currentTarget.FirstName
        );

        binding.Dispose();
        binding.Dispose();
        source.FirstName = "Source change";
        Assert.AreEqual("Initial", target.FirstName);

        target.FirstName = "Target change";
        Assert.AreEqual("Source change", source.FirstName);
    }

    [TestMethod]
    public void TwoWayBind_SupportsNestedPropertiesAndRebindsChangedOwners()
    {
        var oldTargetAddress = new Address { City = "Old" };
        var source = new Person { Address = new Address { City = "London" } };
        var target = new Person { Address = oldTargetAddress };
        using var binding = target.TwoWayBind(
            source,
            currentSource => currentSource.Address!.City,
            currentTarget => currentTarget.Address!.City
        );

        Assert.AreEqual("London", oldTargetAddress.City);

        target.Address = new Address { City = "Tokyo" };
        Assert.AreEqual("Tokyo", source.Address.City);

        oldTargetAddress.City = "Ignored";
        Assert.AreEqual("Tokyo", source.Address.City);

        source.Address.City = "Kyoto";
        Assert.AreEqual("Kyoto", target.Address.City);
    }

    [TestMethod]
    public void TwoWayBind_InvalidPropertyExpressionsThrow()
    {
        var source = new Person();
        var target = new Person();

        Assert.ThrowsExactly<ArgumentException>(() =>
            target.TwoWayBind(
                source,
                currentSource => currentSource.FirstName + currentSource.LastName,
                currentTarget => currentTarget.FirstName
            )
        );
    }

    [TestMethod]
    public void TwoWayBind_NullArgumentsThrow()
    {
        var source = new Person();
        var target = new Person();
        Expression<Func<Person, string>> property = person => person.FirstName;
        Action<string, Person> assignment = (value, person) => person.FirstName = value;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            BindingExtensions.TwoWayBind<Person, Person, string>(
                null!,
                source,
                property,
                property
            )
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            target.TwoWayBind(null!, property, property)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            target.TwoWayBind(source, null!, property)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            target.TwoWayBind(source, property, null!)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            BindingExtensions.TwoWayBind<Person, Person, string, string>(
                target,
                source,
                property,
                property,
                null!,
                assignment
            )
        );
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            BindingExtensions.TwoWayBind<Person, Person, string, string>(
                target,
                source,
                property,
                property,
                assignment,
                null!
            )
        );
    }

    [TestMethod]
    public void ReadOnlyPropertyExpressionsThrow()
    {
        var source = new ManualObservable<string>();
        var target = new ReadOnlyBindingTarget();

        Assert.ThrowsExactly<ArgumentException>(() => source.BindTo(target, x => x.Value));
    }

    [TestMethod]
    public void AssignmentActionNullArgumentsThrow()
    {
        var source = new ManualObservable<string>();
        var target = new Person();
        Action<string, Person> assignment = (value, person) => person.FirstName = value;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            BindingExtensions.BindTo<string, Person>(null!, target, assignment)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() => source.BindTo(null!, assignment));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.BindTo(target, (Action<string, Person>)null!)
        );
    }

    private sealed class ReadOnlyBindingTarget
    {
        public string Value => string.Empty;
    }
}
