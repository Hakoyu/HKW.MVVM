namespace HKW.MVVM;

internal static class Program
{
    private static void Main()
    {
        TestSingleAndDistinctValues();
        TestNestedPropertiesAndNullSuppression();
        TestCombinedValuesAndPropertyHelper();
        TestDeferredSubscription();
        Console.WriteLine("All native MVVM observable tests passed.");
    }

    private static void TestSingleAndDistinctValues()
    {
        var person = new Person { FirstName = "Ada" };
        var values = new List<string>();
        using var subscription = person.WhenAnyValue(x => x.FirstName).Subscribe(values.Add);

        person.FirstName = "Grace";
        person.FirstName = "Grace";

        Assert(values.SequenceEqual(["Ada", "Grace"]), "WhenAnyValue initial/distinct behavior");
    }

    private static void TestNestedPropertiesAndNullSuppression()
    {
        var model = new Person { Address = new Address { City = "London" } };
        var cities = new List<string>();
        using var subscription = model.WhenAnyValue(x => x.Address!.City).Subscribe(cities.Add);

        model.Address!.City = "Paris";
        model.Address = null;
        model.Address = new Address { City = "Tokyo" };
        model.Address.City = "Kyoto";

        Assert(cities.SequenceEqual(["London", "Paris", "Tokyo", "Kyoto"]), "Nested path rebinding/null behavior");
    }

    private static void TestCombinedValuesAndPropertyHelper()
    {
        using var person = new Person { FirstName = "Ada", LastName = "Lovelace" };
        var notifications = new List<string>();
        person.PropertyChanged += (_, args) => notifications.Add(args.PropertyName!);

        Assert(person.FullName == "Ada Lovelace", "OAPH initial combined value");
        person.FirstName = "Grace";

        Assert(person.FullName == "Grace Lovelace", "OAPH updated combined value");
        Assert(notifications.Contains(nameof(Person.FullName)), "OAPH owner notification");
    }

    private static void TestDeferredSubscription()
    {
        var person = new DeferredPerson();
        person.Name = "Before read";
        Assert(person.DisplayName == "Before read", "Deferred OAPH subscribes on first Value read");
        person.Name = "After read";
        Assert(person.DisplayName == "After read", "Deferred OAPH remains subscribed");
        person.Dispose();
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Test failed: {description}");
        }
    }

    private sealed class Person : ObservableObject, IDisposable
    {
        private readonly ObservableAsPropertyHelper<string> _fullName;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private Address? _address;

        public Person()
        {
            _fullName = this.WhenAnyValue(
                    x => x.FirstName,
                    x => x.LastName,
                    static (first, last) => $"{first} {last}".Trim())
                .ToProperty(this, x => x.FullName, initialValue: string.Empty);
        }

        public string FirstName
        {
            get => _firstName;
            set => SetProperty(ref _firstName, value);
        }

        public string LastName
        {
            get => _lastName;
            set => SetProperty(ref _lastName, value);
        }

        public Address? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string FullName => _fullName.Value;

        public void Dispose() => _fullName.Dispose();
    }

    private sealed class DeferredPerson : ObservableObject, IDisposable
    {
        private readonly ObservableAsPropertyHelper<string> _displayName;
        private string _name = string.Empty;

        public DeferredPerson()
        {
            _displayName = this.WhenAnyValue(x => x.Name)
                .ToProperty(this, x => x.DisplayName, initialValue: string.Empty, deferSubscription: true);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string DisplayName => _displayName.Value;

        public void Dispose() => _displayName.Dispose();
    }

    private sealed class Address : ObservableObject
    {
        private string _city = string.Empty;

        public string City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }
    }
}
