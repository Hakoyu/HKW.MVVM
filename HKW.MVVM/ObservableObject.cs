using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HKW.MVVM;

/// <summary>
/// Provides the notification hooks required by <see cref="ObservableAsPropertyHelper{T}"/>.
/// </summary>
public interface IPropertyChangeNotifier : INotifyPropertyChanged, INotifyPropertyChanging
{
    void RaisePropertyChanging(string propertyName);

    void RaisePropertyChanged(string propertyName);
}

/// <summary>
/// Minimal base class for objects implementing property change notification.
/// </summary>
public abstract class ObservableObject : IPropertyChangeNotifier
{
    public event PropertyChangingEventHandler? PropertyChanging;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RaisePropertyChanging(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        OnPropertyChanging(new PropertyChangingEventArgs(propertyName));
    }

    public void RaisePropertyChanged(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    protected virtual void OnPropertyChanging(PropertyChangingEventArgs eventArgs) =>
        PropertyChanging?.Invoke(this, eventArgs);

    protected virtual void OnPropertyChanged(PropertyChangedEventArgs eventArgs) =>
        PropertyChanged?.Invoke(this, eventArgs);

    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        RaisePropertyChanging(propertyName);
        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}