using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HKW.MVVM;

namespace HKW.MVVMTest;

internal sealed class Person : ObservableObject
{
    public string FirstName
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string LastName
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public int Age
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public Address? Address
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public string PublicField = string.Empty;

    public void RaiseChanged(string? propertyName) =>
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
}

internal sealed class Address : ObservableObject
{
    public string City
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;
}

internal sealed class PlainNotifyModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => field;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    } = string.Empty;

    public void Raise(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class ComputedPerson : ObservableObject, IDisposable
{
    private readonly ObservableAsPropertyHelper<string> _fullName;

    public ComputedPerson()
    {
        _fullName = this.WhenAnyValue(
                x => x.FirstName,
                x => x.LastName,
                static (first, last) => $"{first} {last}".Trim()
            )
            .ToProperty(this, x => x.FullName, initialValue: string.Empty);
    }

    public string FirstName
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string LastName
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

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
