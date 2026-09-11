namespace HKW.MVVM;

/// <summary>
/// Provides a thread-safe, bounded cache that memoizes values and retains the most recently used entries.
/// </summary>
/// <typeparam name="TKey">The cache key type.</typeparam>
/// <typeparam name="TValue">The cached value type.</typeparam>
internal sealed class MemoizingMRUCache<TKey, TValue>
    where TKey : notnull
{
    private readonly Lock _gate = new();
    private readonly Func<TKey, TValue> _valueFactory;
    private readonly int _maximumSize;
    private readonly Dictionary<TKey, CacheEntry> _entries;
    private readonly LinkedList<TKey> _mostRecentlyUsedKeys = new();

    /// <summary>
    /// Initializes a new cache.
    /// </summary>
    /// <param name="valueFactory">Creates a value for a missing key.</param>
    /// <param name="maximumSize">The maximum number of values retained by the cache.</param>
    public MemoizingMRUCache(Func<TKey, TValue> valueFactory, int maximumSize)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSize);

        _valueFactory = valueFactory;
        _maximumSize = maximumSize;
        _entries = new Dictionary<TKey, CacheEntry>(maximumSize);
    }

    /// <summary>
    /// Gets the cached value for <paramref name="key"/>, creating it when it is not already cached.
    /// </summary>
    /// <param name="key">The key whose value should be returned.</param>
    /// <returns>The cached or newly created value.</returns>
    /// <remarks>
    /// Value creation is serialized with cache updates so concurrent requests for the same key invoke the value
    /// factory only once.
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