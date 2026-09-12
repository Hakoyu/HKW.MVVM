using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using HKW.MVVM;
using ReactiveUI;
using HkwObservableAsPropertyHelper = HKW.MVVM.ObservableAsPropertyHelper<int>;
using ReactiveObservableAsPropertyHelper = ReactiveUI.ObservableAsPropertyHelper<int>;

namespace HKW.MVVMBenchmark;

/// <summary>Compares ToProperty with an equivalent hand-written observable-backed property.</summary>
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

    /// <summary>Creates long-lived properties used by update benchmarks.</summary>
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

    /// <summary>Disposes long-lived properties used by update benchmarks.</summary>
    [GlobalCleanup]
    public void CleanupUpdateProperties()
    {
        _directFastProperty?.Dispose();
        _toPropertyFastProperty?.Dispose();
        _toPropertyPlainProperty?.Dispose();
        _reactiveUIProperty?.Dispose();
    }

    #region Core

    /// <summary>Measures a hand-written property using direct IPropertyNotifier notifications.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FastOwnerCreateAndDispose")]
    public void DirectFastOwnerCreateAndDispose()
    {
        using var property = new DirectStoredProperty(
            _directFastCreateSource,
            _directOwner.SetResult
        );
    }

    /// <summary>Measures a changed value through a hand-written IPropertyNotifier-backed property.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FastOwnerUpdate")]
    public void DirectFastOwnerUpdate() => _directFastUpdateSource.Emit(++_value);

    /// <summary>Measures first access to an equivalent deferred hand-written property.</summary>
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
    /// <summary>Measures ToProperty creation using its IPropertyNotifier notification path.</summary>
    [Benchmark]
    [BenchmarkCategory("FastOwnerCreateAndDispose")]
    public void ToPropertyFastOwnerCreateAndDispose()
    {
        using var property = _toPropertyFastCreateSource.ToProperty(
            _toPropertyFastOwner,
            owner => owner.Result
        );
    }

    /// <summary>Measures ToProperty creation using cached ObservableObject notification delegates.</summary>
    [Benchmark]
    [BenchmarkCategory("PlainOwnerCreateAndDispose")]
    public void ToPropertyPlainOwnerCreateAndDispose()
    {
        using var property = _toPropertyPlainCreateSource.ToProperty(
            _toPropertyPlainOwner,
            owner => owner.Result
        );
    }

    /// <summary>Measures a changed value through ToProperty's IPropertyNotifier path.</summary>
    [Benchmark]
    [BenchmarkCategory("FastOwnerUpdate")]
    public void ToPropertyFastOwnerUpdate() => _toPropertyFastUpdateSource.Emit(++_value);

    /// <summary>Measures a changed value through ToProperty's plain ObservableObject path.</summary>
    [Benchmark]
    [BenchmarkCategory("PlainOwnerUpdate")]
    public void ToPropertyPlainOwnerUpdate() => _toPropertyPlainUpdateSource.Emit(++_value);

    /// <summary>Measures ToProperty creation followed by its deferred first subscription and read.</summary>
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
    /// <summary>Measures ReactiveUI ToProperty creation and disposal.</summary>
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

    /// <summary>Measures a changed value through ReactiveUI ToProperty.</summary>
    [Benchmark]
    [BenchmarkCategory("PlainOwnerUpdate")]
    public void ReactiveUIUpdate() => _reactiveUIUpdateSource.Emit(++_value);

    /// <summary>Measures ReactiveUI ToProperty creation followed by deferred first subscription and read.</summary>
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
        private int _result;

        public int Result => _result;
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
