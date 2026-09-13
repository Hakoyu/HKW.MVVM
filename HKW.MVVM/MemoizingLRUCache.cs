namespace HKW.MVVM;

/// <summary>
/// 提供线程安全的有界缓存,用于记忆值并保留最近使用过的条目.
/// </summary>
/// <typeparam name="TKey">缓存键类型.</typeparam>
/// <typeparam name="TValue">缓存值类型.</typeparam>
internal sealed class MemoizingLRUCache<TKey, TValue>
    where TKey : notnull
{
    private readonly Lock _gate = new();
    private readonly Func<TKey, TValue> _valueFactory;
    private readonly int _maximumSize;
    private readonly Dictionary<TKey, CacheEntry> _entries;
    private readonly LinkedList<TKey> _mostRecentlyUsedKeys = new();

    /// <summary>
    /// 初始化新缓存.
    /// </summary>
    /// <param name="valueFactory">为缺失的键创建值.</param>
    /// <param name="maximumSize">缓存保留的最大值数量.</param>
    public MemoizingLRUCache(Func<TKey, TValue> valueFactory, int maximumSize)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSize);

        _valueFactory = valueFactory;
        _maximumSize = maximumSize;
        _entries = new Dictionary<TKey, CacheEntry>(maximumSize);
    }

    /// <summary>
    /// 获取 <paramref name="key"/> 对应的缓存值,尚未缓存时创建该值.
    /// </summary>
    /// <param name="key">要返回其值的键.</param>
    /// <returns>已缓存或新创建的值.</returns>
    /// <remarks>
    /// 值创建过程与缓存更新串行执行,因此针对同一键的并发请求只会调用一次
    /// 值工厂.
    /// </remarks>
    public TValue Get(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                Refresh(entry.Node);
                return entry.Value;
            }

            var value = _valueFactory(key);
            var node = _mostRecentlyUsedKeys.AddFirst(key);
            _entries.Add(key, new CacheEntry(node, value));

            if (_entries.Count > _maximumSize)
            {
                var leastRecentlyUsedNode = _mostRecentlyUsedKeys.Last!;
                _entries.Remove(leastRecentlyUsedNode.Value);
                _mostRecentlyUsedKeys.RemoveLast();
            }

            return value;
        }
    }

    private void Refresh(LinkedListNode<TKey> node)
    {
        if (ReferenceEquals(node, _mostRecentlyUsedKeys.First))
        {
            return;
        }

        _mostRecentlyUsedKeys.Remove(node);
        _mostRecentlyUsedKeys.AddFirst(node);
    }

    private sealed record CacheEntry(LinkedListNode<TKey> Node, TValue Value);
}