using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM;

/// <summary>
/// 除标准通知事件之外,还公开属性更改通知方法
/// </summary>
public interface IPropertyChangeNotifier : INotifyPropertyChanging, INotifyPropertyChanged
{
    /// <summary>
    /// 引发 <see cref="INotifyPropertyChanging.PropertyChanging"/>
    /// </summary>
    void NotifyPropertyChanging([CallerMemberName] string? propertyName = null);

    /// <summary>
    /// 引发 <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// </summary>
    void NotifyPropertyChanged([CallerMemberName] string? propertyName = null);

    /// <summary>
    /// 引发 <see cref="INotifyPropertyChanging.PropertyChanging"/>
    /// </summary>
    void NotifyPropertyChanging(PropertyChangingEventArgs args);

    /// <summary>
    /// 引发 <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// </summary>
    void NotifyPropertyChanged(PropertyChangedEventArgs args);
}

/// <summary>
/// 增强的 <see cref="ObservableObject"/>,其通知方法可公开访问
/// </summary>
public class ObservableObjectEx : ObservableObject, IPropertyChangeNotifier
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly PropertyChangeSubject<IPropertyChangeEventArgs<ObservableObjectEx>> _changing =
        new();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly PropertyChangeSubject<IPropertyChangeEventArgs<ObservableObjectEx>> _changed =
        new();

    /// <summary>
    /// 在属性值更改之前发出通知
    /// </summary>
    public IObservable<IPropertyChangeEventArgs<ObservableObjectEx>> Changing => _changing;

    /// <summary>
    /// 在属性值更改之后发出通知
    /// </summary>
    public IObservable<IPropertyChangeEventArgs<ObservableObjectEx>> Changed => _changed;

    /// <inheritdoc />
    public void NotifyPropertyChanging([CallerMemberName] string? propertyName = null) =>
        OnPropertyChanging(propertyName);

    /// <inheritdoc />
    public void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) =>
        OnPropertyChanged(propertyName);

    /// <inheritdoc />
    public void NotifyPropertyChanging(PropertyChangingEventArgs args) => OnPropertyChanging(args);

    /// <inheritdoc />
    public void NotifyPropertyChanged(PropertyChangedEventArgs args) => OnPropertyChanged(args);

    /// <inheritdoc />
    protected override void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        base.OnPropertyChanging(e);
        _changing.OnNext(new PropertyChangeEventArgs<ObservableObjectEx>(this, e.PropertyName));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        _changed.OnNext(new PropertyChangeEventArgs<ObservableObjectEx>(this, e.PropertyName));
    }
}
