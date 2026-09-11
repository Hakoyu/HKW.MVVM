using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HKW.MVVM;

namespace HKW.MVVMTest;

internal sealed class Person : ObservableObject
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private int _age;
    private Address? _address;

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

    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value);
    }

    public Address? Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string PublicField = string.Empty;

    public void RaiseChanged(string? propertyName) =>
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
}

internal sealed class Address : ObservableObject
{
    private string _city = string.Empty;

    public string City
    {
        get => _city;
        set => SetProperty(ref _city, value);
    }
}

internal sealed class PlainNotifyModel : INotifyPropertyChanged
{
    private string _name = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public void Raise(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class ComputedPerson : ObservableObject, IDisposable
{
    private readonly ObservableAsPropertyHelper<string> _fullName;
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;

    public ComputedPerson()
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

    public string FullName => _fullName.Value;

    public void Dispose() => _fullName.Dispose();
}

internal sealed class PropertyOwner : ObservableObject
{
    public string Result { get; private set; } = string.Empty;

    public string OtherResult { get; private set; } = string.Empty;
}

internal sealed class GetterFailureModel : ObservableObject
{
    public string Broken => throw new TestException("Getter failed.");
}

internal sealed class TestException(string message) : Exception(message);

internal sealed class ManualObservable<T> : IObservable<T>
{
    private readonly List<IObserver<T>> _observers = [];

    public int SubscriptionCount { get; private set; }

    public int DisposalCount { get; private set; }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        SubscriptionCount++;
        _observers.Add(observer);
        return new CallbackDisposable(() =>
        {
            if (_observers.Remove(observer))
            {
                DisposalCount++;
            }
        });
    }

    public void Emit(T value)
    {
        foreach (var observer in _observers.ToArray())
        {
            observer.OnNext(value);
        }
    }

    public void Fail(Exception exception)
    {
        foreach (var observer in _observers.ToArray())
        {
            observer.OnError(exception);
        }
    }

    public void Complete()
    {
        foreach (var observer in _observers.ToArray())
        {
            observer.OnCompleted();
        }
    }
}

internal sealed class CallbackDisposable(Action callback) : IDisposable
{
    private Action? _callback = callback;

    public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
}

internal sealed class QueuedSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _work = new();

    public int PendingCount => _work.Count;

    public override void Post(SendOrPostCallback d, object? state) => _work.Enqueue((d, state));

    public void RunAll()
    {
        while (_work.TryDequeue(out var work))
        {
            work.Callback(work.State);
        }
    }
}