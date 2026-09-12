using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM;

/// <summary>
/// Exposes property-change notification methods in addition to the standard notification events.
/// </summary>
public interface IPropertyChangeNotifier : INotifyPropertyChanging, INotifyPropertyChanged
{
    /// <summary>Raises <see cref="INotifyPropertyChanging.PropertyChanging"/>.</summary>
    void NotifyPropertyChanging([CallerMemberName] string? propertyName = null);

    /// <summary>Raises <see cref="INotifyPropertyChanged.PropertyChanged"/>.</summary>
    void NotifyPropertyChanged([CallerMemberName] string? propertyName = null);
}

/// <summary>
/// An enhanced <see cref="ObservableObject"/> whose notification methods are publicly accessible.
/// </summary>
public class ObservableObjectEx : ObservableObject, IPropertyChangeNotifier
{
    /// <inheritdoc />
    public void NotifyPropertyChanging([CallerMemberName] string? propertyName = null) =>
        OnPropertyChanging(propertyName);

    /// <inheritdoc />
    public void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) =>
        OnPropertyChanged(propertyName);
}