using HKW.MVVM;
using Microsoft.Extensions.Logging.Abstractions;

namespace HKW.MVVMTest;

[TestClass]
public sealed class ObservableLifecycleExtensionsTests
{
    [TestMethod]
    public void Scan_UnseededAndSeeded_AccumulateValues()
    {
        var source = new LifecycleSubject<int>();
        var unseeded = new List<int>();
        var seeded = new List<string>();
        using var first = source.Scan((sum, value) => sum + value).Subscribe(unseeded.Add);
        using var second = source.Scan("start", (text, value) => $"{text}:{value}").Subscribe(seeded.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        CollectionAssert.AreEqual(new[] { 1, 3, 6 }, unseeded);
        CollectionAssert.AreEqual(new[] { "start:1", "start:1:2", "start:1:2:3" }, seeded);
    }

    [TestMethod]
    public void Scan_AccumulatorFailure_TerminatesAndDisposesSource()
    {
        var source = new LifecycleSubject<int>();
        Exception? received = null;
        var values = new List<int>();
        using var subscription = source
            .Scan(0, (sum, value) => value == 2 ? throw new TestException("scan") : sum + value)
            .Subscribe(values.Add, error => received = error);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        CollectionAssert.AreEqual(new[] { 1 }, values);
        Assert.IsInstanceOfType<TestException>(received);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Retry_CountMeansExtraSubscriptions_AndZeroDoesNotRetry()
    {
        var zero = new FailingObservable(2);
        Exception? zeroError = null;
        zero.Retry(0).Subscribe(_ => { }, error => zeroError = error);

        var twice = new FailingObservable(3);
        var values = new List<int>();
        var completed = false;
        twice.Retry(2).Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        Assert.AreEqual(1, zero.SubscriptionCount);
        Assert.IsNotNull(zeroError);
        Assert.AreEqual(3, twice.SubscriptionCount);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(3, twice.DisposalCount);
    }

    [TestMethod]
    public void Retry_ManySynchronousFailures_IsStackSafe()
    {
        var source = new FailingObservable(20_001);
        var completed = false;

        source.Retry(20_000).Subscribe(_ => { }, _ => Assert.Fail(), () => completed = true);

        Assert.IsTrue(completed);
        Assert.AreEqual(20_001, source.SubscriptionCount);
    }

    [TestMethod]
    public void Retry_SubscribeExceptionsAreRetriedAndThenForwarded()
    {
        var source = new ThrowingSubscribeObservable<int>();
        Exception? received = null;

        source.Retry(2).Subscribe(_ => { }, error => received = error);

        Assert.AreEqual(3, source.SubscriptionCount);
        Assert.IsInstanceOfType<TestException>(received);
    }

    [TestMethod]
    public void Finally_RunsExactlyOnceForCompletionErrorDisposalAndSubscribeFailure()
    {
        foreach (var termination in Enum.GetValues<Termination>())
        {
            var calls = 0;
            if (termination == Termination.SubscribeFailure)
            {
                new ThrowingSubscribeObservable<int>()
                    .Finally(() => calls++)
                    .Subscribe(_ => { }, _ => { });
            }
            else
            {
                var source = new LifecycleSubject<int>();
                var subscription = source.Finally(() => calls++).Subscribe(_ => { }, _ => { });
                if (termination == Termination.Completed)
                    source.OnCompleted();
                else if (termination == Termination.Error)
                    source.OnError(new TestException("error"));
                else
                    subscription.Dispose();
                subscription.Dispose();
            }

            Assert.AreEqual(1, calls, termination.ToString());
        }
    }

    [TestMethod]
    public void Finally_ActionExceptionPropagatesAfterResourceDisposal()
    {
        var source = new LifecycleSubject<int>();
        using var subscription = source.Finally(() => throw new TestException("finally")).Subscribe(_ => { });

        Assert.ThrowsExactly<TestException>(source.OnCompleted);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void Finally_CompletionRunsActionWhenUpstreamDisposalThrows()
    {
        var source = new ThrowingDisposeObservable<int>();
        var completed = false;
        var finallyCalls = 0;
        source.Finally(() => finallyCalls++).Subscribe(_ => { }, _ => Assert.Fail(), () => completed = true);

        var exception = Assert.ThrowsExactly<TestException>(source.Complete);
        source.Complete();

        Assert.AreEqual("dispose", exception.Message);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.AreEqual(1, finallyCalls);
    }

    [TestMethod]
    public void Finally_ErrorRunsActionWhenUpstreamDisposalThrows()
    {
        var source = new ThrowingDisposeObservable<int>();
        var expected = new InvalidOperationException("source");
        Exception? received = null;
        var finallyCalls = 0;
        source.Finally(() => finallyCalls++).Subscribe(_ => { }, error => received = error);

        var exception = Assert.ThrowsExactly<TestException>(() => source.Fail(expected));
        source.Fail(expected);

        Assert.AreEqual("dispose", exception.Message);
        Assert.AreSame(expected, received);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.AreEqual(1, finallyCalls);
    }

    [TestMethod]
    public void TakeUntil_OtherNextCompletes_OtherCompletionIsIgnored_AndOtherErrorTerminates()
    {
        var source = new LifecycleSubject<int>();
        var other = new LifecycleSubject<string>();
        var values = new List<int>();
        var completed = false;
        using var subscription = source.TakeUntil(other).Subscribe(values.Add, _ => Assert.Fail(), () => completed = true);

        other.OnCompleted();
        source.OnNext(1);
        Assert.IsFalse(completed);
        source.OnCompleted();

        CollectionAssert.AreEqual(new[] { 1 }, values);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);

        var source2 = new LifecycleSubject<int>();
        var other2 = new LifecycleSubject<int>();
        var stoppedByOther = false;
        source2.TakeUntil(other2).Subscribe(_ => { }, _ => Assert.Fail(), () => stoppedByOther = true);
        other2.OnNext(42);
        Assert.IsTrue(stoppedByOther);
        Assert.AreEqual(1, source2.DisposalCount);

        var source3 = new LifecycleSubject<int>();
        var other3 = new LifecycleSubject<int>();
        Exception? received = null;
        source3.TakeUntil(other3).Subscribe(_ => { }, error => received = error);
        var expected = new TestException("other");
        other3.OnError(expected);
        Assert.AreSame(expected, received);
        Assert.AreEqual(1, source3.DisposalCount);
    }

    [TestMethod]
    public void TakeUntil_SynchronousOtherNext_DoesNotSubscribeSource()
    {
        var source = new LifecycleSubject<int>();
        var completed = false;
        var other = new SynchronousLifecycleObservable<string>(observer => observer.OnNext("stop"));

        source.TakeUntil(other).Subscribe(_ => Assert.Fail(), _ => Assert.Fail(), () => completed = true);

        Assert.IsTrue(completed);
        Assert.AreEqual(0, source.SubscriptionCount);
        Assert.AreEqual(1, other.DisposalCount);
    }

    [TestMethod]
    public void LifecycleOperators_ValidateArgumentsAndPreserveLogger()
    {
        var source = new LifecycleSubject<int>();
        var loggerSource = new ObservableWithLogger<int>(source, () => NullLogger.Instance);

        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.Scan<int>(null!, (x, y) => x + y));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Scan((Func<int, int, int>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Scan(0, (Func<int, int, int>)null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.Retry(-1));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Finally(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => source.TakeUntil<int, string>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObservableExtensions.TakeUntil<int, string>(null!, new LifecycleSubject<string>()));

        Assert.IsInstanceOfType<IObservableWithLogger>(loggerSource.Scan((x, y) => x + y));
        Assert.IsInstanceOfType<IObservableWithLogger>(loggerSource.Scan(0, (x, y) => x + y));
        Assert.IsInstanceOfType<IObservableWithLogger>(loggerSource.Retry(0));
        Assert.IsInstanceOfType<IObservableWithLogger>(loggerSource.Finally(() => { }));
        Assert.IsInstanceOfType<IObservableWithLogger>(loggerSource.TakeUntil(new LifecycleSubject<string>()));
    }

    private enum Termination { Completed, Error, Disposal, SubscribeFailure }

    private sealed class LifecycleSubject<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];
        public int SubscriptionCount { get; private set; }
        public int DisposalCount { get; private set; }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            SubscriptionCount++;
            _observers.Add(observer);
            return new LifecycleDisposable(() =>
            {
                if (_observers.Remove(observer)) DisposalCount++;
            });
        }

        public void OnNext(T value) { foreach (var observer in _observers.ToArray()) observer.OnNext(value); }
        public void OnError(Exception error) { foreach (var observer in _observers.ToArray()) observer.OnError(error); }
        public void OnCompleted() { foreach (var observer in _observers.ToArray()) observer.OnCompleted(); }
    }

    private sealed class SynchronousLifecycleObservable<T>(Action<IObserver<T>> action) : IObservable<T>
    {
        public int DisposalCount { get; private set; }
        public IDisposable Subscribe(IObserver<T> observer)
        {
            action(observer);
            return new LifecycleDisposable(() => DisposalCount++);
        }
    }

    private sealed class FailingObservable(int succeedsOnAttempt) : IObservable<int>
    {
        public int SubscriptionCount { get; private set; }
        public int DisposalCount { get; private set; }
        public IDisposable Subscribe(IObserver<int> observer)
        {
            var attempt = ++SubscriptionCount;
            observer.OnNext(attempt);
            if (attempt < succeedsOnAttempt) observer.OnError(new TestException("retry"));
            else observer.OnCompleted();
            return new LifecycleDisposable(() => DisposalCount++);
        }
    }

    private sealed class ThrowingSubscribeObservable<T> : IObservable<T>
    {
        public int SubscriptionCount { get; private set; }
        public IDisposable Subscribe(IObserver<T> observer)
        {
            SubscriptionCount++;
            throw new TestException("subscribe");
        }
    }

    private sealed class ThrowingDisposeObservable<T> : IObservable<T>
    {
        private IObserver<T>? _observer;
        public int DisposalCount { get; private set; }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            return new LifecycleDisposable(() =>
            {
                DisposalCount++;
                throw new TestException("dispose");
            });
        }

        public void Complete() => _observer!.OnCompleted();
        public void Fail(Exception error) => _observer!.OnError(error);
    }

    private sealed class LifecycleDisposable(Action action) : IDisposable
    {
        private Action? _action = action;
        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
