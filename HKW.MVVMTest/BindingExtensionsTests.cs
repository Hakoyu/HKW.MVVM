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
