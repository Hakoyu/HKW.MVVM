using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class MultipleDisposableTests
{
    [TestMethod]
    public void Dispose_DisposesEveryResourceExactlyOnce()
    {
        var first = new TrackingDisposable();
        var second = new TrackingDisposable();
        var disposables = new MultipleDisposable { first, second };

        disposables.Dispose();
        disposables.Dispose();

        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
        Assert.AreEqual(0, disposables.Count);
    }

    [TestMethod]
    public void Add_AfterContainerIsDisposed_DisposesResourceImmediately()
    {
        var disposables = new MultipleDisposable();
        disposables.Dispose();
        var resource = new TrackingDisposable();

        disposables.Add(resource);

        Assert.AreEqual(1, resource.DisposeCount);
        Assert.AreEqual(0, disposables.Count);
    }

    [TestMethod]
    public void Remove_DetachesResourceWithoutDisposingIt()
    {
        var resource = new TrackingDisposable();
        var disposables = new MultipleDisposable { resource };

        var removed = disposables.Remove(resource);
        disposables.Dispose();

        Assert.IsTrue(removed);
        Assert.AreEqual(0, resource.DisposeCount);
    }

    [TestMethod]
    public void Clear_DisposesCurrentResourcesAndAllowsNewResources()
    {
        var first = new TrackingDisposable();
        var second = new TrackingDisposable();
        var disposables = new MultipleDisposable { first };

        disposables.Clear();
        disposables.Add(second);

        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(0, second.DisposeCount);
        Assert.AreEqual(1, disposables.Count);
        disposables.Dispose();
        Assert.AreEqual(1, second.DisposeCount);
    }

    [TestMethod]
    public void CollectionMembers_OperateOnHeldResources()
    {
        var first = new TrackingDisposable();
        var second = new TrackingDisposable();
        using var disposables = new MultipleDisposable { first, second };
        var copy = new IDisposable[2];

        disposables.CopyTo(copy, 0);

        Assert.IsTrue(disposables.Contains(first));
        CollectionAssert.AreEqual(new IDisposable[] { first, second }, copy);
        CollectionAssert.AreEqual(new IDisposable[] { first, second }, disposables.ToArray());
    }

    [TestMethod]
    public void Dispose_WhenResourcesThrow_AttemptsAllAndAggregatesErrors()
    {
        var first = new ThrowingDisposable("First");
        var middle = new TrackingDisposable();
        var last = new ThrowingDisposable("Last");
        var disposables = new MultipleDisposable { first, middle, last };

        var exception = Assert.ThrowsExactly<AggregateException>(disposables.Dispose);

        Assert.HasCount(2, exception.InnerExceptions);
        Assert.AreEqual(1, middle.DisposeCount);
    }

    [TestMethod]
    public void DisposableWith_AddsResourceAndReturnsSameInstance()
    {
        var disposables = new MultipleDisposable();
        var resource = new TrackingDisposable();

        var returned = resource.DisposeWith(disposables);

        Assert.AreSame(resource, returned);
        Assert.AreEqual(1, disposables.Count);
        disposables.Dispose();
        Assert.AreEqual(1, resource.DisposeCount);
    }

    [TestMethod]
    public void DisposableWith_ValidatesArguments()
    {
        var disposables = new MultipleDisposable();
        TrackingDisposable resource = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() => resource.DisposeWith(disposables));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new TrackingDisposable().DisposeWith(null!)
        );
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    private sealed class ThrowingDisposable(string message) : IDisposable
    {
        public void Dispose() => throw new TestException(message);
    }
}
