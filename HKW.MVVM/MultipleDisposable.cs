using System.Collections;

namespace HKW.MVVM;

/// <summary>
/// Collects multiple disposable resources and disposes them together.
/// </summary>
/// <remarks>
/// The type is thread-safe. Adding a resource after this container has been disposed disposes that
/// resource immediately. Disposing the container more than once has no additional effect.
/// </remarks>
public sealed class MultipleDisposable : ICollection<IDisposable>, IDisposable
{
    private readonly System.Threading.Lock _gate = new();
    private List<IDisposable>? _items = [];

    /// <summary>Gets the number of resources currently held by this container.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items?.Count ?? 0;
            }
        }
    }

    /// <summary>Gets a value indicating whether the collection is read-only.</summary>
    public bool IsReadOnly => false;

    /// <summary>Adds a resource to this container.</summary>
    /// <param name="item">The resource to add.</param>
    /// <remarks>If the container is already disposed, <paramref name="item"/> is disposed immediately.</remarks>
    public void Add(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            if (_items is not null)
            {
                _items.Add(item);
                return;
            }
        }

        item.Dispose();
    }

    /// <summary>Removes and disposes all resources currently held by this container.</summary>
    public void Clear()
    {
        IDisposable[] items;
        lock (_gate)
        {
            if (_items is null || _items.Count == 0)
            {
                return;
            }

            items = [.. _items];
            _items.Clear();
        }

        DisposeAll(items);
    }

    /// <summary>Determines whether a resource is held by this container.</summary>
    /// <param name="item">The resource to locate.</param>
    /// <returns><see langword="true"/> when the resource is present; otherwise, <see langword="false"/>.</returns>
    public bool Contains(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            return _items?.Contains(item) is true;
        }
    }

    /// <summary>Copies the held resources to an array.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination index.</param>
    public void CopyTo(IDisposable[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        lock (_gate)
        {
            (_items ?? []).CopyTo(array, arrayIndex);
        }
    }

    /// <summary>Removes a resource without disposing it.</summary>
    /// <param name="item">The resource to remove.</param>
    /// <returns><see langword="true"/> when the resource was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            return _items?.Remove(item) is true;
        }
    }

    /// <summary>Returns an enumerator over a snapshot of the held resources.</summary>
    /// <returns>An enumerator over the snapshot.</returns>
    public IEnumerator<IDisposable> GetEnumerator()
    {
        lock (_gate)
        {
            return (
                (_items is null ? [] : _items.ToArray()) as IEnumerable<IDisposable>
            ).GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Disposes every held resource and permanently closes this container.</summary>
    /// <exception cref="AggregateException">One or more resources threw while being disposed.</exception>
    public void Dispose()
    {
        IDisposable[] items;
        lock (_gate)
        {
            if (_items is null)
            {
                return;
            }

            items = [.. _items];
            _items = null;
        }

        DisposeAll(items);
    }

    private static void DisposeAll(IEnumerable<IDisposable> items)
    {
        List<Exception>? exceptions = null;
        foreach (var item in items)
        {
            try
            {
                item.Dispose();
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }

        if (exceptions is not null)
        {
            throw new AggregateException("One or more resources failed to dispose.", exceptions);
        }
    }
}

/// <summary>Provides fluent helpers for registering disposable resources.</summary>
public static class DisposableExtensions
{
    /// <summary>Adds a disposable resource to a <see cref="MultipleDisposable"/> container.</summary>
    /// <typeparam name="TDisposable">The concrete disposable resource type.</typeparam>
    /// <param name="disposable">The resource to register.</param>
    /// <param name="multipleDisposable">The container that will own the resource.</param>
    /// <returns>The original resource, allowing registration in a fluent expression.</returns>
    /// <remarks><b>REFLECTION: NO.</b> The resource is added directly to the supplied container.</remarks>
    public static TDisposable DisposeWith<TDisposable>(
        this TDisposable disposable,
        MultipleDisposable multipleDisposable
    )
        where TDisposable : IDisposable
    {
        ArgumentNullException.ThrowIfNull(disposable);
        ArgumentNullException.ThrowIfNull(multipleDisposable);
        multipleDisposable.Add(disposable);
        return disposable;
    }
}
