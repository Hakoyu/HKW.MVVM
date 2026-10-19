using BenchmarkDotNet.Attributes;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>
    /// Representative microbenchmarks for APIs introduced in package version 0.2.0.
    ///</summary>
[MemoryDiagnoser]
public class ReleaseFeatureBenchmarks
{
    private readonly PushObservable<int> _pairwiseSource = new();
    private readonly IObservable<int> _pairwisePipeline;
    private IDisposable? _pairwiseSubscription;
    private int _pairwiseResult;
    private int _value;

    /// <summary>
    /// Initializes the practical-operator pipeline.
    ///</summary>
    public ReleaseFeatureBenchmarks()
    {
        _pairwisePipeline = ObservableExtensions.Select(
            ObservableExtensions.Pairwise(_pairwiseSource),
            static pair => pair.Previous + pair.Current
        );
    }

    /// <summary>
    /// Creates the long-lived subscription used by the update benchmark.
    ///</summary>
    [GlobalSetup]
    public void Setup() =>
        _pairwiseSubscription = ObservableExtensions.Subscribe(
            _pairwisePipeline,
            value => _pairwiseResult = value
        );

    /// <summary>
    /// Releases the long-lived benchmark subscription.
    ///</summary>
    [GlobalCleanup]
    public void Cleanup() => _pairwiseSubscription?.Dispose();

    /// <summary>
    /// Measures one FIFO trampoline scheduling operation.
    ///</summary>
    [Benchmark]
    public void CurrentThreadSchedule()
    {
        using var scheduled = ObservableSchedulers.CurrentThread.Schedule(static () => { });
    }

    /// <summary>
    /// Measures a synchronous-completion asynchronous projection.
    ///</summary>
    [Benchmark]
    public Task<int> SelectAsyncSequential() =>
        ObservableExtensions.ToTask(
            ObservableExtensions.SelectAsync(
                ObservableExtensions.Return(42),
                static (value, _) => Task.FromResult(value + 1),
                AsyncConcurrencyMode.Sequential
            )
        );

    /// <summary>
    /// Measures delivery through a representative new practical operator.
    ///</summary>
    [Benchmark]
    public int PairwiseUpdate()
    {
        var value = _value += 2;
        _pairwiseSource.Emit(value - 1);
        _pairwiseSource.Emit(value);
        return _pairwiseResult;
    }

    private sealed class PushObservable<T> : IObservable<T>
    {
        private IObserver<T>? _observer;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            return new Subscription(this, observer);
        }

        public void Emit(T value) => _observer?.OnNext(value);

        private sealed class Subscription(PushObservable<T> source, IObserver<T> observer)
            : IDisposable
        {
            private PushObservable<T>? _source = source;

            public void Dispose()
            {
                var source = Interlocked.Exchange(ref _source, null);
                if (source is not null && ReferenceEquals(source._observer, observer))
                {
                    source._observer = null;
                }
            }
        }
    }
}
