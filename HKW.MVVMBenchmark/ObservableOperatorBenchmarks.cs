using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>Compares observable operator pipelines with equivalent direct observer logic.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ObservableOperatorBenchmarks
{
    private readonly BenchmarkObservable<int> _directCreateSource = new();
    private readonly BenchmarkObservable<int> _operatorCreateSource = new();
    private readonly BenchmarkObservable<int> _reactiveUICreateSource = new();
    private readonly BenchmarkObservable<int> _directUpdateSource = new();
    private readonly BenchmarkObservable<int> _operatorUpdateSource = new();
    private readonly BenchmarkObservable<int> _reactiveUIUpdateSource = new();
    private IDisposable? _directSubscription;
    private IDisposable? _operatorSubscription;
    private IDisposable? _reactiveUISubscription;
    private int _directResult;
    private int _operatorResult;
    private int _reactiveUIResult;
    private int _value;

    /// <summary>Gets the pipeline shape used by the current benchmark case.</summary>
    [Params(PipelineKind.Select, PipelineKind.WhereSelect, PipelineKind.WhereSelectDistinct)]
    public PipelineKind Pipeline { get; set; }

    /// <summary>Creates long-lived subscriptions used by message-delivery benchmarks.</summary>
    [GlobalSetup]
    public void SetupUpdateSubscriptions()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directSubscription = SubscribeDirect(
            _directUpdateSource,
            Pipeline,
            value => _directResult = value
        );
        _operatorSubscription = SubscribeOperators(
            _operatorUpdateSource,
            Pipeline,
            value => _operatorResult = value
        );
        _reactiveUISubscription = SubscribeReactiveUIOperators(
            _reactiveUIUpdateSource,
            Pipeline,
            value => _reactiveUIResult = value
        );
    }

    /// <summary>Disposes subscriptions used by message-delivery benchmarks.</summary>
    [GlobalCleanup]
    public void CleanupUpdateSubscriptions()
    {
        _directSubscription?.Dispose();
        _operatorSubscription?.Dispose();
        _reactiveUISubscription?.Dispose();
    }

    /// <summary>Measures direct observer creation, subscription, and disposal.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateAndDispose")]
    public void DirectCreateAndDispose()
    {
        using var subscription = SubscribeDirect(
            _directCreateSource,
            Pipeline,
            value => _directResult = value
        );
    }

    /// <summary>Measures operator pipeline creation, subscription, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void OperatorCreateAndDispose()
    {
        using var subscription = SubscribeOperators(
            _operatorCreateSource,
            Pipeline,
            value => _operatorResult = value
        );
    }

    /// <summary>Measures ReactiveUI operator pipeline creation, subscription, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ReactiveUIOperatorCreateAndDispose()
    {
        using var subscription = SubscribeReactiveUIOperators(
            _reactiveUICreateSource,
            Pipeline,
            value => _reactiveUIResult = value
        );
    }

    /// <summary>Measures two messages through equivalent direct observer logic.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Update")]
    public void DirectUpdate()
    {
        var value = _value += 2;
        _directUpdateSource.Emit(value - 1);
        _directUpdateSource.Emit(value);
    }

    /// <summary>Measures two messages through the selected observable operator pipeline.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void OperatorUpdate()
    {
        var value = _value += 2;
        _operatorUpdateSource.Emit(value - 1);
        _operatorUpdateSource.Emit(value);
    }

    /// <summary>Measures two messages through the equivalent ReactiveUI operator pipeline.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void ReactiveUIOperatorUpdate()
    {
        var value = _value += 2;
        _reactiveUIUpdateSource.Emit(value - 1);
        _reactiveUIUpdateSource.Emit(value);
    }

    private static IDisposable SubscribeDirect(
        BenchmarkObservable<int> source,
        PipelineKind pipeline,
        Action<int> onNext
    ) => source.Subscribe(new DirectPipelineObserver(pipeline, onNext));

    private static IDisposable SubscribeOperators(
        BenchmarkObservable<int> source,
        PipelineKind pipeline,
        Action<int> onNext
    ) =>
        pipeline switch
        {
            PipelineKind.Select => source.Select(Transform).Subscribe(onNext),
            PipelineKind.WhereSelect => source.Where(IsEven).Select(Transform).Subscribe(onNext),
            PipelineKind.WhereSelectDistinct => source
                .Where(IsEven)
                .Select(Transform)
                .DistinctUntilChanged()
                .Subscribe(onNext),
            _ => throw new ArgumentOutOfRangeException(nameof(pipeline)),
        };

    private static IDisposable SubscribeReactiveUIOperators(
        BenchmarkObservable<int> source,
        PipelineKind pipeline,
        Action<int> onNext
    ) =>
        pipeline switch
        {
            PipelineKind.Select => ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
                ReactiveUI.Primitives.LinqExtensions.Select(source, Transform),
                onNext
            ),
            PipelineKind.WhereSelect => ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
                ReactiveUI.Primitives.LinqExtensions.Where(
                    ReactiveUI.Primitives.LinqExtensions.Select(source, Transform),
                    IsEven
                ),
                onNext
            ),
            PipelineKind.WhereSelectDistinct => ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
                ReactiveUI.Primitives.LinqExtensions.DistinctUntilChanged(
                    ReactiveUI.Primitives.LinqExtensions.Select(
                        ReactiveUI.Primitives.LinqExtensions.Where(source, IsEven),
                        Transform
                    )
                ),
                onNext
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(pipeline)),
        };

    private static bool IsEven(int value) => (value & 1) == 0;

    private static int Transform(int value) => value * 2;

    /// <summary>Identifies the observable pipeline being measured.</summary>
    public enum PipelineKind
    {
        /// <summary>A single Select operator.</summary>
        Select,

        /// <summary>A Where operator followed by Select.</summary>
        WhereSelect,

        /// <summary>Where and Select followed by DistinctUntilChanged.</summary>
        WhereSelectDistinct,
    }

    private sealed class DirectPipelineObserver(PipelineKind pipeline, Action<int> onNext)
        : IObserver<int>
    {
        private int _lastValue;
        private bool _hasValue;

        public void OnNext(int value)
        {
            if (pipeline is not PipelineKind.Select && IsEven(value) is false)
            {
                return;
            }

            var result = Transform(value);
            if (pipeline is PipelineKind.WhereSelectDistinct && _hasValue && _lastValue == result)
            {
                return;
            }

            _hasValue = true;
            _lastValue = result;
            onNext(result);
        }

        public void OnError(Exception error) { }

        public void OnCompleted() { }
    }

    private sealed class BenchmarkObservable<T> : IObservable<T>
    {
        private IObserver<T>? _observer;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            return new Subscription(this, observer);
        }

        public void Emit(T value) => _observer?.OnNext(value);

        private sealed class Subscription(BenchmarkObservable<T> source, IObserver<T> observer)
            : IDisposable
        {
            private BenchmarkObservable<T>? _source = source;

            public void Dispose()
            {
                var currentSource = Interlocked.Exchange(ref _source, null);
                if (currentSource is not null && ReferenceEquals(currentSource._observer, observer))
                {
                    currentSource._observer = null;
                }
            }
        }
    }
}
