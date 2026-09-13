using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using HKW.MVVM;
using ReactiveUI;
using HkwObservableAsPropertyHelper = HKW.MVVM.ObservableAsPropertyHelper<int>;
using ReactiveObservableAsPropertyHelper = ReactiveUI.ObservableAsPropertyHelper<int>;

namespace HKW.MVVMBenchmark;

/// <summary>
/// 比较 ToProperty 与等效的手写,由可观察序列支持的属性.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ToPropertyBenchmarks
{
    private readonly BenchmarkObservable<int> _directFastCreateSource = new();
    private readonly BenchmarkObservable<int> _toPropertyFastCreateSource = new();
    private readonly BenchmarkObservable<int> _toPropertyPlainCreateSource = new();
    private readonly BenchmarkObservable<int> _reactiveUICreateSource = new();
    private readonly BenchmarkObservable<int> _directFastUpdateSource = new();
    private readonly BenchmarkObservable<int> _toPropertyFastUpdateSource = new();
    private readonly BenchmarkObservable<int> _toPropertyPlainUpdateSource = new();
    private readonly BenchmarkObservable<int> _reactiveUIUpdateSource = new();
    private readonly BenchmarkObservable<int> _directDeferredSource = new();
    private readonly BenchmarkObservable<int> _toPropertyDeferredSource = new();
    private readonly BenchmarkObservable<int> _reactiveUIDeferredSource = new();
    private readonly FastOwner _directOwner = new();
    private readonly FastOwner _toPropertyFastOwner = new();
    private readonly PlainOwner _toPropertyPlainOwner = new();
    private readonly ReactiveUIOwner _reactiveUIOwner = new();
    private DirectStoredProperty? _directFastProperty;
    private HkwObservableAsPropertyHelper? _toPropertyFastProperty;
    private HkwObservableAsPropertyHelper? _toPropertyPlainProperty;
    private ReactiveObservableAsPropertyHelper? _reactiveUIProperty;
    private int _value;

    /// <summary>
/// 创建更新基准测试所使用的长期属性.
/// </summary>
    [GlobalSetup]
    public void SetupUpdateProperties()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directFastProperty = new DirectStoredProperty(
            _directFastUpdateSource,
            _directOwner.SetResult
        );
        _toPropertyFastProperty = _toPropertyFastUpdateSource.ToProperty(
            _toPropertyFastOwner,
            owner => owner.Result
        );
        _toPropertyPlainProperty = _toPropertyPlainUpdateSource.ToProperty(
            _toPropertyPlainOwner,
            owner => owner.Result
        );
        _reactiveUIProperty = ReactiveUI.OAPHCreationHelperMixins.ToProperty(
            _reactiveUIUpdateSource,
            _reactiveUIOwner,
            owner => owner.Result
        );
    }

    /// <summary>
/// 释放更新基准测试所使用的长期属性.
/// </summary>
    [GlobalCleanup]
    public void CleanupUpdateProperties()
    {
        _directFastProperty?.Dispose();
        _toPropertyFastProperty?.Dispose();
        _toPropertyPlainProperty?.Dispose();
        _reactiveUIProperty?.Dispose();
    }

    #region Core

    /// <summary>
/// 测量使用直接 IPropertyNotifier 通知的手写属性.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FastOwnerCreateAndDispose")]
    public void DirectFastOwnerCreateAndDispose()
    {
        using var property = new DirectStoredProperty(
            _directFastCreateSource,
            _directOwner.SetResult
        );
    }

    /// <summary>
/// 测量通过手写的,由 IPropertyNotifier 支持的属性发生的一次值变更.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FastOwnerUpdate")]
    public void DirectFastOwnerUpdate() => _directFastUpdateSource.Emit(++_value);

    /// <summary>
/// 测量对等效的延迟手写属性的首次访问.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DeferredFirstRead")]
    public int DirectDeferredFirstRead()
    {
        using var property = new DirectStoredProperty(
            _directDeferredSource,
            _directOwner.SetResult,
            deferSubscription: true
        );
        return property.Value;
    }
    #endregion

    #region HKW
    /// <summary>
/// 测量使用其 IPropertyNotifier 通知路径的 ToProperty 创建.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("FastOwnerCreateAndDispose")]
    public void ToPropertyFastOwnerCreateAndDispose()
    {
        using var property = _toPropertyFastCreateSource.ToProperty(
            _toPropertyFastOwner,
            owner => owner.Result
        );
    }

    /// <summary>
/// 测量使用缓存的 ObservableObject 通知委托的 ToProperty 创建.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("PlainOwnerCreateAndDispose")]
    public void ToPropertyPlainOwnerCreateAndDispose()
    {
        using var property = _toPropertyPlainCreateSource.ToProperty(
            _toPropertyPlainOwner,
            owner => owner.Result
        );
    }

    /// <summary>
/// 测量通过 ToProperty 的 IPropertyNotifier 路径发生的一次值变更.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("FastOwnerUpdate")]
    public void ToPropertyFastOwnerUpdate() => _toPropertyFastUpdateSource.Emit(++_value);

    /// <summary>
/// 测量通过 ToProperty 的普通 ObservableObject 路径发生的一次值变更.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("PlainOwnerUpdate")]
    public void ToPropertyPlainOwnerUpdate() => _toPropertyPlainUpdateSource.Emit(++_value);

    /// <summary>
/// 测量 ToProperty 创建及其延迟的首次订阅与读取.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("DeferredFirstRead")]
    public int ToPropertyDeferredFirstRead()
    {
        using var property = _toPropertyDeferredSource.ToProperty(
            _toPropertyFastOwner,
            owner => owner.Result,
            deferSubscription: true
        );
        return property.Value;
    }
    #endregion

    #region ReactiveUI
    /// <summary>
/// 测量 ReactiveUI ToProperty 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("PlainOwnerCreateAndDispose")]
    public void ReactiveUICreateAndDispose()
    {
        using var property = ReactiveUI.OAPHCreationHelperMixins.ToProperty(
            _reactiveUICreateSource,
            _reactiveUIOwner,
            owner => owner.Result
        );
    }

    /// <summary>
/// 测量通过 ReactiveUI ToProperty 发生的一次值变更.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("PlainOwnerUpdate")]
    public void ReactiveUIUpdate() => _reactiveUIUpdateSource.Emit(++_value);

    /// <summary>
/// 测量 ReactiveUI ToProperty 创建及其延迟的首次订阅与读取.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("DeferredFirstRead")]
    public int ReactiveUIDeferredFirstRead()
    {
        using var property = ReactiveUI.OAPHCreationHelperMixins.ToProperty(
            _reactiveUIDeferredSource,
            _reactiveUIOwner,
            owner => owner.Result,
            deferSubscription: true
        );
        return property.Value;
    }
    #endregion


    private sealed class FastOwner : ObservableObjectEx
    {
        private int _result;

        public int Result => _result;

        public void SetResult(int value)
        {
            if (_result == value)
            {
                return;
            }

            NotifyPropertyChanging(nameof(Result));
            _result = value;
            NotifyPropertyChanged(nameof(Result));
        }
    }

    private sealed class PlainOwner : ObservableObject
    {
        public int Result => 0;
    }

    private sealed class ReactiveUIOwner : ReactiveObject
    {
        public int Result => 0;
    }

    private sealed class DirectStoredProperty : IDisposable, IObserver<int>
    {
        private readonly BenchmarkObservable<int> _source;
        private readonly Action<int> _setOwnerValue;
        private IDisposable? _subscription;
        private int _value;
        private bool _started;
        private bool _disposed;

        public DirectStoredProperty(
            BenchmarkObservable<int> source,
            Action<int> setOwnerValue,
            bool deferSubscription = false
        )
        {
            _source = source;
            _setOwnerValue = setOwnerValue;
            if (deferSubscription is false)
            {
                EnsureSubscribed();
            }
        }

        public int Value
        {
            get
            {
                EnsureSubscribed();
                return _value;
            }
        }

        public void OnNext(int value)
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            _setOwnerValue(value);
        }

        public void OnError(Exception error) { }

        public void OnCompleted() { }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _subscription?.Dispose();
        }

        private void EnsureSubscribed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            _subscription = _source.Subscribe(this);
        }
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
