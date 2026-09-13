using System.ComponentModel;
using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableObjectTests
{
    [TestMethod]
    public void SetProperty_WhenValueChanges_RaisesChangingThenChanged()
    {
        var model = new Person();
        var events = new List<string>();
        model.PropertyChanging += (_, args) => events.Add($"Changing:{args.PropertyName}:{model.FirstName}");
        model.PropertyChanged += (_, args) => events.Add($"Changed:{args.PropertyName}:{model.FirstName}");

        model.FirstName = "Ada";

        CollectionAssert.AreEqual(
            new[] { "Changing:FirstName:", "Changed:FirstName:Ada" },
            events);
    }

    [TestMethod]
    public void SetProperty_WhenValueIsEqual_DoesNotRaiseNotifications()
    {
        var model = new Person { FirstName = "Ada" };
        var changingCount = 0;
        var changedCount = 0;
        model.PropertyChanging += (_, _) => changingCount++;
        model.PropertyChanged += (_, _) => changedCount++;

        model.FirstName = "Ada";

        Assert.AreEqual(0, changingCount);
        Assert.AreEqual(0, changedCount);
    }

    [TestMethod]
    public void ObservableObjectEx_PublicNotifyMethods_RaiseNotifications()
    {
        var model = new ObservableObjectEx();
        var events = new List<string>();
        model.PropertyChanging += (_, args) => events.Add($"Changing:{args.PropertyName}");
        model.PropertyChanged += (_, args) => events.Add($"Changed:{args.PropertyName}");

        model.NotifyPropertyChanging("Result");
        model.NotifyPropertyChanged("Result");

        CollectionAssert.AreEqual(new[] { "Changing:Result", "Changed:Result" }, events);
        Assert.IsInstanceOfType<IPropertyChangeNotifier>(model);
    }

    [TestMethod]
    public void ObservableObjectEx_ChangingAndChanged_EmitReactiveNotifications()
    {
        var model = new Person();
        var events = new List<string>();
        using var changing = model.Changing.Subscribe(args =>
            events.Add($"Changing:{args.PropertyName}:{ReferenceEquals(model, args.Sender)}"));
        using var changed = model.Changed.Subscribe(args =>
            events.Add($"Changed:{args.PropertyName}:{ReferenceEquals(model, args.Sender)}"));

        model.FirstName = "Ada";

        CollectionAssert.AreEqual(
            new[] { "Changing:FirstName:True", "Changed:FirstName:True" },
            events);
    }

    [TestMethod]
    public void ObservableObjectEx_ChangingAndChanged_DoNotEmitForEqualValue()
    {
        var model = new Person { FirstName = "Ada" };
        var changingCount = 0;
        var changedCount = 0;
        using var changing = model.Changing.Subscribe(_ => changingCount++);
        using var changed = model.Changed.Subscribe(_ => changedCount++);

        model.FirstName = "Ada";

        Assert.AreEqual(0, changingCount);
        Assert.AreEqual(0, changedCount);
    }

    [TestMethod]
    public void ObservableObjectEx_ReactiveSubscriptions_StopAfterDispose()
    {
        var model = new Person();
        var changingCount = 0;
        var subscription = model.Changing.Subscribe(_ => changingCount++);

        subscription.Dispose();
        model.FirstName = "Ada";

        Assert.AreEqual(0, changingCount);
    }

}