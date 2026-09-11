using System.ComponentModel;
using System.Linq.Expressions;

namespace HKW.MVVM;

/// <summary>
/// Stores the latest value from an observable and raises notifications for a read-only owner property.
/// </summary>
public sealed class ObservableAsPropertyHelper<T> : IDisposable, INotifyPropertyChanged, INotifyPropertyChanging
{
    private readonly object _gate = new();
    private readonly IObservable<T> _source;
    private readonly IPropertyChangeNotifier _owner;
    private readonly string _propertyName;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly ExceptionSubject _exceptions = new();
    private IDisposable? _subscription;
    private T _value;
    private bool _started;
    private bool _disposed;

    internal ObservableAsPropertyHelper(
        IObservable<T> source,
        IPropertyChangeNotifier owner,
        string propertyName,
        T initialValue,
        bool deferSubscription,
        SynchronizationContext? synchronizationContext)
    {
        _source = source;
        _owner = owner;
        _propertyName = propertyName;
        _value = initialValue;
        _synchronizationContext = synchronizationContext;

        if (!deferSubscription)
        {
            EnsureSubscribed();
        }
    }

    public event PropertyChangingEventHandler? PropertyChanging;

    public event PropertyChangedEventHandler? PropertyChanged;

    public T Value
    {
        get
        {
            EnsureSubscribed();
            lock (_gate)
            {
                return _value;
            }
        }
    }

    /// <summary>Receives terminal errors produced by the source.</summary>
    public IObservable<Exception> ThrownExceptions => _exceptions;

    public void Dispose()
    {
        IDisposable? subscription;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            subscription = _subscription;
            _subscription = null;
        }

        subscription?.Dispose();
        _exceptions.Dispose();
    }

    private void EnsureSubscribed()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            _subscription = _source.Subscribe(SetValue, _exceptions.OnNext);
        }
    }

    private void SetValue(T value) => Dispatch(() =>
    {
        lock (_gate)
        {
            if (_disposed || EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            var changingArgs = new PropertyChangingEventArgs(nameof(Value));
            PropertyChanging?.Invoke(this, changingArgs);
            _owner.RaisePropertyChanging(_propertyName);
            _value = value;
            var changedArgs = new PropertyChangedEventArgs(nameof(Value));
            PropertyChanged?.Invoke(this, changedArgs);
            _owner.RaisePropertyChanged(_propertyName);
        }
    });

    private void Dispatch(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
        }
        else
        {
            _synchronizationContext.Post(static state => ((Action)state!).Invoke(), action);
        }
    }
}

public static class ObservableAsPropertyHelperExtensions
{
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        TValue initialValue = default!,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null)
        where TOwner : class, IPropertyChangeNotifier
    {
        ArgumentNullException.ThrowIfNull(property);
        var propertyName = GetPropertyName(property);
        return ToProperty(source, owner, propertyName, initialValue, deferSubscription, synchronizationContext);
    }

    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        TValue initialValue = default!,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null)
        where TOwner : class, IPropertyChangeNotifier
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new ObservableAsPropertyHelper<TValue>(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            synchronizationContext);
    }

    private static string GetPropertyName<TOwner, TValue>(Expression<Func<TOwner, TValue>> property)
    {
        Expression body = property.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
        {
            body = conversion.Operand;
        }

        if (body is not MemberExpression { Member: System.Reflection.PropertyInfo info } member ||
            member.Expression != property.Parameters[0])
        {
            throw new ArgumentException("The expression must select a direct owner property, for example x => x.FullName.", nameof(property));
        }

        return info.Name;
    }
}