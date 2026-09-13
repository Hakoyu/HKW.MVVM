using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>
/// 比较可观察操作符管道与等效的直接观察者逻辑.
/// </summary>
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

    /// <summary>
/// 获取当前基准用例所使用的管道形态.
/// </summary>
    [Params(PipelineKind.Select, PipelineKind.WhereSelect, PipelineKind.WhereSelectDistinct)]
    public PipelineKind Pipeline { get; set; }

    /// <summary>
/// 创建消息传递基准测试所使用的长期订阅.
/// </summary>
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

    /// <summary>
/// 释放消息传递基准测试所使用的订阅.
/// </summary>
    [GlobalCleanup]
    public void CleanupUpdateSubscriptions()
    {
        _directSubscription?.Dispose();
        _operatorSubscription?.Dispose();
        _reactiveUISubscription?.Dispose();
    }

    #region Core
    /// <summary>
/// 测量直接观察者的创建、订阅和释放.
/// </summary>
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

    /// <summary>
/// 测量通过等效的直接观察者逻辑传递的两条消息.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Update")]
    public void DirectUpdate()
    {
        var value = _value += 2;
        _directUpdateSource.Emit(value - 1);
        _directUpdateSource.Emit(value);
    }
    #endregion

    #region HKW
    /// <summary>
/// 测量操作符管道的创建、订阅和释放.
/// </summary>
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

    /// <summary>
/// 测量通过所选可观察操作符管道传递的两条消息.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void OperatorUpdate()
    {
        var value = _value += 2;
        _operatorUpdateSource.Emit(value - 1);
        _operatorUpdateSource.Emit(value);
    }
    #endregion

    #region ReactiveUI
    /// <summary>
/// 测量 ReactiveUI 操作符管道的创建、订阅和释放.
/// </summary>
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

    /// <summary>
/// 测量通过等效 ReactiveUI 操作符管道传递的两条消息.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void ReactiveUIOperatorUpdate()
    {
        var value = _value += 2;
        _reactiveUIUpdateSource.Emit(value - 1);
        _reactiveUIUpdateSource.Emit(value);
    }
    #endregion


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
                ReactiveUI.Primitives.LinqExtensions.Select(
                    ReactiveUI.Primitives.LinqExtensions.Where(source, IsEven),
                    Transform
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

    /// <summary>
/// 标识当前所测量的可观察管道.
/// </summary>
    public enum PipelineKind
    {
        /// <summary>
/// 单个 Select 操作符.
/// </summary>
        Select,

        /// <summary>
/// Where 操作符后接 Select.
/// </summary>
        WhereSelect,

        /// <summary>
/// Where 和 Select 后接 DistinctUntilChanged.
/// </summary>
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
