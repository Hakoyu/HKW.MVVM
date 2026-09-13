using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM;

/// <summary>
/// 除标准通知事件之外,还公开属性更改通知方法.
/// </summary>
public interface IPropertyChangeNotifier : INotifyPropertyChanging, INotifyPropertyChanged
{
    /// <summary>
    /// 引发 <see cref="INotifyPropertyChanging.PropertyChanging"/>.
    /// </summary>
    void NotifyPropertyChanging([CallerMemberName] string? propertyName = null);

    /// <summary>
    /// 引发 <see cref="INotifyPropertyChanged.PropertyChanged"/>.
    /// </summary>
    void NotifyPropertyChanged([CallerMemberName] string? propertyName = null);
}

/// <summary>
/// 增强的 <see cref="ObservableObject"/>,其通知方法可公开访问.
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
