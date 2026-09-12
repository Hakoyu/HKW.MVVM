using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HKW.MVVM;

/// <summary>
/// Stores the latest value from an observable and raises notifications for a read-only owner property.
/// </summary>
public sealed class ObservableAsPropertyHelper<T>
    : IDisposable,
        INotifyPropertyChanged,
        INotifyPropertyChanging
{
    private readonly Lock _gate = new();
    private readonly IObservable<T> _source;
    private readonly ObservableObject _owner;
    private readonly string _propertyName;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly ExceptionSubject _exceptions = new();
    private IDisposable? _subscription;
    private T _value;
    private bool _started;
    private bool _disposed;

    internal ObservableAsPropertyHelper(
        IObservable<T> source,
        ObservableObject owner,
        string propertyName,
        T initialValue,
        bool deferSubscription,
        SynchronizationContext? synchronizationContext
    )
    {
        _source = source;
        _owner = owner;
        _propertyName = propertyName;
        _value = initialValue;
        _synchronizationContext = synchronizationContext;

        if (deferSubscription is false)
        {
            EnsureSubscribed();
        }
    }

    /// <summary>Raised before <see cref="Value"/> changes.</summary>
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>Raised after <see cref="Value"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the latest value received from the source observable.</summary>
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

    /// <summary>Stops observing the source and releases all owned resources.</summary>
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

    private void SetValue(T value) =>
        Dispatch(() =>
        {
            lock (_gate)
            {
                if (_disposed || EqualityComparer<T>.Default.Equals(_value, value))
                {
                    return;
                }

                var changingArgs = new PropertyChangingEventArgs(nameof(Value));
                PropertyChanging?.Invoke(this, changingArgs);
                PropertyNotificationDispatcher.NotifyPropertyChanging(_owner, _propertyName);
                _value = value;
                var changedArgs = new PropertyChangedEventArgs(nameof(Value));
                PropertyChanged?.Invoke(this, changedArgs);
                PropertyNotificationDispatcher.NotifyPropertyChanged(_owner, _propertyName);
            }
        });

    private void Dispatch(Action action)
    {
        if (
            _synchronizationContext is null
            || SynchronizationContext.Current == _synchronizationContext
        )
        {
            action();
        }
        else
        {
            _synchronizationContext.Post(static state => ((Action)state!).Invoke(), action);
        }
    }
}

/// <summary>Provides extensions for exposing observable values as read-only properties.</summary>
public static class ObservableAsPropertyHelperExtensions
{
    /// <summary>
    /// Converts an observable sequence into a helper for a read-only owner property selected by an expression.
    /// </summary>
    /// <typeparam name="TOwner">The CommunityToolkit observable owner type.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="source">The sequence that supplies property values.</param>
    /// <param name="owner">The object that owns the read-only property.</param>
    /// <param name="property">An expression selecting a direct property on <paramref name="owner"/>.</param>
    /// <param name="initialValue">The value exposed before the source produces its first distinct value.</param>
    /// <param name="deferSubscription">Whether source subscription should be delayed until the helper value is first read.</param>
    /// <param name="synchronizationContext">An optional context used to dispatch value changes and notifications.</param>
    /// <returns>An observable property helper that stores the latest value and notifies the owner.</returns>
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> The property name is extracted from the expression. Owner notification
    /// prefers <see cref="IPropertyChangeNotifier"/> and otherwise invokes CommunityToolkit's protected methods
    /// through cached delegates created from <see cref="MethodInfo"/> instances.
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        Expression<Func<TOwner, TValue>> property,
        TValue initialValue = default!,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        var propertyName = property.GetPropertyName();
        return ToProperty(
            source,
            owner,
            propertyName,
            initialValue,
            deferSubscription,
            synchronizationContext
        );
    }

    /// <summary>
    /// Converts an observable sequence into a helper for a read-only owner property identified by name.
    /// </summary>
    /// <typeparam name="TOwner">The CommunityToolkit observable owner type.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="source">The sequence that supplies property values.</param>
    /// <param name="owner">The object that owns the read-only property.</param>
    /// <param name="propertyName">The owner property name used in change notifications.</param>
    /// <param name="initialValue">The value exposed before the source produces its first distinct value.</param>
    /// <param name="deferSubscription">Whether source subscription should be delayed until the helper value is first read.</param>
    /// <param name="synchronizationContext">An optional context used to dispatch value changes and notifications.</param>
    /// <returns>An observable property helper that stores the latest value and notifies the owner.</returns>
    /// <remarks>
    /// <b>REFLECTION: CONDITIONAL.</b> Owner notification prefers <see cref="IPropertyChangeNotifier"/> and otherwise
    /// invokes CommunityToolkit's protected methods through cached delegates created once with reflection.
    /// </remarks>
    public static ObservableAsPropertyHelper<TValue> ToProperty<TOwner, TValue>(
        this IObservable<TValue> source,
        TOwner owner,
        string propertyName,
        TValue initialValue = default!,
        bool deferSubscription = false,
        SynchronizationContext? synchronizationContext = null
    )
        where TOwner : ObservableObject
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
            synchronizationContext
        );
    }
}

internal static class PropertyNotificationDispatcher
{
    private static readonly Action<
        ObservableObject,
        PropertyChangingEventArgs
    > PropertyChangingDelegate = CreateDelegate<PropertyChangingEventArgs>("OnPropertyChanging");

    private static readonly Action<
        ObservableObject,
        PropertyChangedEventArgs
    > PropertyChangedDelegate = CreateDelegate<PropertyChangedEventArgs>("OnPropertyChanged");

    public static void NotifyPropertyChanging(ObservableObject owner, string propertyName)
    {
        if (owner is IPropertyChangeNotifier notifier)
        {
            notifier.NotifyPropertyChanging(propertyName);
            return;
        }

        PropertyChangingDelegate(owner, new PropertyChangingEventArgs(propertyName));
    }

    public static void NotifyPropertyChanged(ObservableObject owner, string propertyName)
    {
        if (owner is IPropertyChangeNotifier notifier)
        {
            notifier.NotifyPropertyChanged(propertyName);
            return;
        }

        PropertyChangedDelegate(owner, new PropertyChangedEventArgs(propertyName));
    }

    private static Action<ObservableObject, TEventArgs> CreateDelegate<TEventArgs>(
        string methodName
    )
        where TEventArgs : EventArgs =>
        typeof(ObservableObject)
            .GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(TEventArgs)],
                modifiers: null
            )
            ?.CreateDelegate<Action<ObservableObject, TEventArgs>>()
        ?? throw new InvalidOperationException(
            $"{typeof(ObservableObject).FullName} does not expose {methodName}({typeof(TEventArgs).Name})."
        );
}
