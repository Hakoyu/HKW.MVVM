using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class NativeObservableSubscriptionTests
{
    [TestMethod]
    public void Subscribe_ForwardsValuesErrorsAndCompletion()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        Exception? receivedError = null;
        var completed = false;
        using var subscription = source.Subscribe(
            values.Add,
            error => receivedError = error,
            () => completed = true);

        source.Emit(1);
        source.Emit(2);
        var error = new TestException("Failed.");
        source.Fail(error);
        source.Complete();

        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
        Assert.AreSame(error, receivedError);
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public void Subscribe_WhenDisposed_StopsForwardingValues()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var subscription = source.Subscribe(values.Add);

        subscription.Dispose();
        source.Emit(1);

        Assert.IsEmpty(values);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Subscribe_InvalidArgumentsThrow()
    {
        var source = new ManualObservable<int>();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            NativeObservableSubscriptionExtensions.Subscribe<int>(null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.Subscribe((Action<int>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            source.Subscribe(_ => { }, null!));
    }
}