using System.Collections;

namespace HKW.MVVM;

/// <summary>
/// 收集多个可释放资源并将它们一起释放.
/// </summary>
/// <remarks>
/// 该类型是线程安全的.在此容器被释放之后再添加资源,会立即释放该
/// 资源.多次释放容器不会产生其他效果.
/// </remarks>
public sealed class MultipleDisposable : ICollection<IDisposable>, IDisposable
{
    private readonly Lock _gate = new();
    private List<IDisposable>? _items = [];

    /// <summary>
/// 获取此容器当前持有的资源数量.
/// </summary>
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

    /// <summary>
/// 获取一个值,指示该集合是否为只读.
/// </summary>
    public bool IsReadOnly => false;

    /// <summary>
/// 向此容器添加一个资源.
/// </summary>
    /// <param name="item">要添加的资源.</param>
    /// <remarks>如果容器已被释放,则立即释放 <paramref name="item"/>.</remarks>
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

    /// <summary>
/// 移除并释放此容器当前持有的全部资源.
/// </summary>
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

    /// <summary>
/// 确定此容器是否持有指定的资源.
/// </summary>
    /// <param name="item">要查找的资源.</param>
    /// <returns>资源存在时为 <see langword="true"/>;否则为 <see langword="false"/>.</returns>
    public bool Contains(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            return _items?.Contains(item) is true;
        }
    }

    /// <summary>
/// 将持有的资源复制到数组.
/// </summary>
    /// <param name="array">目标数组.</param>
    /// <param name="arrayIndex">从零开始的目标索引.</param>
    public void CopyTo(IDisposable[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        lock (_gate)
        {
            (_items ?? []).CopyTo(array, arrayIndex);
        }
    }

    /// <summary>
/// 移除某个资源,但不释放它.
/// </summary>
    /// <param name="item">要移除的资源.</param>
    /// <returns>资源被移除时为 <see langword="true"/>;否则为 <see langword="false"/>.</returns>
    public bool Remove(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            return _items?.Remove(item) is true;
        }
    }

    /// <summary>
/// 返回遍历所持有资源快照的枚举器.
/// </summary>
    /// <returns>遍历该快照的枚举器.</returns>
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

    /// <summary>
/// 释放所有持有的资源,并永久关闭此容器.
/// </summary>
    /// <exception cref="AggregateException">有一个或多个资源在释放时引发了异常.</exception>
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

/// <summary>
/// 提供用于注册可释放资源的流式辅助方法.
/// </summary>
public static class DisposableExtensions
{
    /// <summary>
/// 将可释放资源添加到 <see cref="MultipleDisposable"/> 容器.
/// </summary>
    /// <typeparam name="TDisposable">具体的可释放资源类型.</typeparam>
    /// <param name="disposable">要注册的资源.</param>
    /// <param name="multipleDisposable">将拥有该资源的容器.</param>
    /// <returns>原始资源,便于在流式表达式中继续注册.</returns>
    /// <remarks><b>反射:否.</b>资源被直接添加到所提供的容器中.</remarks>
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
