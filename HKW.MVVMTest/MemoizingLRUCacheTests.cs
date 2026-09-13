using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class MemoizingLRUCacheTests
{
    [TestMethod]
    public async Task ConcurrentRequestsForSameKey_CreateOnlyOneValue()
    {
        var factoryCalls = 0;
        var cache = new MemoizingLRUCache<int, string>(_ =>
        {
            Interlocked.Increment(ref factoryCalls);
            Thread.Sleep(50);
            return "value";
        }, 8);

        var values = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => cache.Get(1))));

        Assert.IsTrue(values.All(value => value == "value"));
        Assert.AreEqual(1, factoryCalls);
    }

    [TestMethod]
    public async Task DifferentKeys_CanCreateValuesConcurrently()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var cache = new MemoizingLRUCache<int, int>(key =>
        {
            if (key == 1)
            {
                firstStarted.Set();
                releaseFirst.Wait(TimeSpan.FromSeconds(5));
            }

            return key;
        }, 8);

        var first = Task.Run(() => cache.Get(1));
        Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => cache.Get(2));

        Assert.IsTrue(second.Wait(TimeSpan.FromSeconds(5)));
        releaseFirst.Set();
        await first;
    }

    [TestMethod]
    public void FactoryFailure_IsNotCached()
    {
        var calls = 0;
        var cache = new MemoizingLRUCache<int, int>(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new TestException("Expected.");
            return 42;
        }, 8);

        Assert.ThrowsExactly<TestException>(() => cache.Get(1));
        Assert.AreEqual(42, cache.Get(1));
        Assert.AreEqual(2, calls);
    }
}