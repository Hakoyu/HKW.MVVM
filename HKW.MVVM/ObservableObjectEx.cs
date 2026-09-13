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
    private readonly PropertyChangeSubject<
        IPropertyChangedEventArgs<ObservableObjectEx>
    > _changing = new();
    private readonly PropertyChangeSubject<IPropertyChangedEventArgs<ObservableObjectEx>> _changed =
        new();

    /// <summary>
    /// 在属性值更改之前发出通知.
    /// </summary>
    public IObservable<IPropertyChangedEventArgs<ObservableObjectEx>> Changing => _changing;

    /// <summary>
    /// 在属性值更改之后发出通知.
    /// </summary>
    public IObservable<IPropertyChangedEventArgs<ObservableObjectEx>> Changed => _changed;

    /// <summary>
    /// 创建对象并连接标准属性通知与响应式属性通知流.
    /// </summary>
    public ObservableObjectEx()
    {
        PropertyChanging += OnPropertyChanging;
        PropertyChanged += OnPropertyChanged;
    }

    /// <inheritdoc />
    public void NotifyPropertyChanging([CallerMemberName] string? propertyName = null) =>
        OnPropertyChanging(propertyName);

    /// <inheritdoc />
    public void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) =>
        OnPropertyChanged(propertyName);

    private void OnPropertyChanging(object? sender, PropertyChangingEventArgs args) =>
        _changing.OnNext(new PropertyChangedEventArgs<ObservableObjectEx>(this, args.PropertyName));

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        _changed.OnNext(new PropertyChangedEventArgs<ObservableObjectEx>(this, args.PropertyName));
}
