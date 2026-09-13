using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>
/// 测量 WhenAnyValue 的创建与更新如何从单属性扩展到四属性.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MultiPropertyWhenAnyValueBenchmarks
{
    private readonly Values _directOneCreateSource = new();
    private readonly Values _whenAnyOneCreateSource = new();
    private readonly Values _reactiveUIOneCreateSource = new();
    private readonly Values _directTwoCreateSource = new();
    private readonly Values _whenAnyTwoCreateSource = new();
    private readonly Values _reactiveUITwoCreateSource = new();
    private readonly Values _directFourCreateSource = new();
    private readonly Values _whenAnyFourCreateSource = new();
    private readonly Values _reactiveUIFourCreateSource = new();
    private readonly Values _directOneUpdateSource = new();
    private readonly Values _whenAnyOneUpdateSource = new();
    private readonly Values _reactiveUIOneUpdateSource = new();
    private readonly Values _directTwoUpdateSource = new();
    private readonly Values _whenAnyTwoUpdateSource = new();
    private readonly Values _reactiveUITwoUpdateSource = new();
    private readonly Values _directFourUpdateSource = new();
    private readonly Values _whenAnyFourUpdateSource = new();
    private readonly Values _reactiveUIFourUpdateSource = new();
    private IDisposable? _directOneSubscription;
    private IDisposable? _whenAnyOneSubscription;
    private IDisposable? _reactiveUIOneSubscription;
    private IDisposable? _directTwoSubscription;
    private IDisposable? _whenAnyTwoSubscription;
    private IDisposable? _reactiveUITwoSubscription;
    private IDisposable? _directFourSubscription;
    private IDisposable? _whenAnyFourSubscription;
    private IDisposable? _reactiveUIFourSubscription;
    private int _directResult;
    private int _whenAnyResult;
    private int _reactiveUIResult;
    private int _value;

    /// <summary>
/// 创建更新基准测试所使用的长期订阅.
/// </summary>
    [GlobalSetup]
    public void SetupUpdateSubscriptions()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directOneSubscription = new DirectCombinedSubscription(
            _directOneUpdateSource,
            1,
            value => _directResult = value
        );
        _whenAnyOneSubscription = _whenAnyOneUpdateSource
            .WhenAnyValue(source => source.Value1)
            .Subscribe(value => _whenAnyResult = value);
        _reactiveUIOneSubscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUIOneUpdateSource, source => source.Value1)
            .Subscribe(value => _reactiveUIResult = value);
        _directTwoSubscription = new DirectCombinedSubscription(
            _directTwoUpdateSource,
            2,
            value => _directResult = value
        );
        _whenAnyTwoSubscription = _whenAnyTwoUpdateSource
            .WhenAnyValue(
                source => source.Value1,
                source => source.Value2,
                static (value1, value2) => value1 + value2
            )
            .Subscribe(value => _whenAnyResult = value);
        _reactiveUITwoSubscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(
                _reactiveUITwoUpdateSource,
                source => source.Value1,
                source => source.Value2,
                static (value1, value2) => value1 + value2
            )
            .Subscribe(value => _reactiveUIResult = value);
        _directFourSubscription = new DirectCombinedSubscription(
            _directFourUpdateSource,
            4,
            value => _directResult = value
        );
        _whenAnyFourSubscription = _whenAnyFourUpdateSource
            .WhenAnyValue(
                source => source.Value1,
                source => source.Value2,
                source => source.Value3,
                source => source.Value4,
                static (value1, value2, value3, value4) => value1 + value2 + value3 + value4
            )
            .Subscribe(value => _whenAnyResult = value);
        _reactiveUIFourSubscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(
                _reactiveUIFourUpdateSource,
                source => source.Value1,
                source => source.Value2,
                source => source.Value3,
                source => source.Value4,
                static (value1, value2, value3, value4) => value1 + value2 + value3 + value4
            )
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>
/// 释放更新基准测试所使用的订阅.
/// </summary>
    [GlobalCleanup]
    public void CleanupUpdateSubscriptions()
    {
        _directOneSubscription?.Dispose();
        _whenAnyOneSubscription?.Dispose();
        _reactiveUIOneSubscription?.Dispose();
        _directTwoSubscription?.Dispose();
        _whenAnyTwoSubscription?.Dispose();
        _reactiveUITwoSubscription?.Dispose();
        _directFourSubscription?.Dispose();
        _whenAnyFourSubscription?.Dispose();
        _reactiveUIFourSubscription?.Dispose();
    }

    #region Core
    /// <summary>
/// 测量直接的单属性订阅.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("OnePropertyCreateAndDispose")]
    public void DirectOnePropertyCreateAndDispose()
    {
        using var subscription = new DirectCombinedSubscription(
            _directOneCreateSource,
            1,
            value => _directResult = value
        );
    }

    /// <summary>
/// 测量直接的双属性组合订阅.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TwoPropertiesCreateAndDispose")]
    public void DirectTwoPropertiesCreateAndDispose()
    {
        using var subscription = new DirectCombinedSubscription(
            _directTwoCreateSource,
            2,
            value => _directResult = value
        );
    }

    /// <summary>
/// 测量直接的四属性组合订阅.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FourPropertiesCreateAndDispose")]
    public void DirectFourPropertiesCreateAndDispose()
    {
        using var subscription = new DirectCombinedSubscription(
            _directFourCreateSource,
            4,
            value => _directResult = value
        );
    }

    /// <summary>
/// 测量直接的单属性更新.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("OnePropertyUpdate")]
    public void DirectOnePropertyUpdate() => _directOneUpdateSource.Value1 = ++_value;

    /// <summary>
/// 测量由直接双属性订阅观察到的更新.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TwoPropertiesUpdate")]
    public void DirectTwoPropertiesUpdate() => _directTwoUpdateSource.Value1 = ++_value;

    /// <summary>
/// 测量由直接四属性订阅观察到的更新.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FourPropertiesUpdate")]
    public void DirectFourPropertiesUpdate() => _directFourUpdateSource.Value1 = ++_value;
    #endregion

    #region HKW
    /// <summary>
/// 测量单属性 WhenAnyValue 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("OnePropertyCreateAndDispose")]
    public void WhenAnyValueOnePropertyCreateAndDispose()
    {
        using var subscription = _whenAnyOneCreateSource
            .WhenAnyValue(source => source.Value1)
            .Subscribe(value => _whenAnyResult = value);
    }

    /// <summary>
/// 测量双属性 WhenAnyValue 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("TwoPropertiesCreateAndDispose")]
    public void WhenAnyValueTwoPropertiesCreateAndDispose()
    {
        using var subscription = _whenAnyTwoCreateSource
            .WhenAnyValue(
                source => source.Value1,
                source => source.Value2,
                static (value1, value2) => value1 + value2
            )
            .Subscribe(value => _whenAnyResult = value);
    }

    /// <summary>
/// 测量四属性 WhenAnyValue 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("FourPropertiesCreateAndDispose")]
    public void WhenAnyValueFourPropertiesCreateAndDispose()
    {
        using var subscription = _whenAnyFourCreateSource
            .WhenAnyValue(
                source => source.Value1,
                source => source.Value2,
                source => source.Value3,
                source => source.Value4,
                static (value1, value2, value3, value4) => value1 + value2 + value3 + value4
            )
            .Subscribe(value => _whenAnyResult = value);
    }

    /// <summary>
/// 测量单属性 WhenAnyValue 的更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("OnePropertyUpdate")]
    public void WhenAnyValueOnePropertyUpdate() => _whenAnyOneUpdateSource.Value1 = ++_value;

    /// <summary>
/// 测量由双属性 WhenAnyValue 观察到的更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("TwoPropertiesUpdate")]
    public void WhenAnyValueTwoPropertiesUpdate() => _whenAnyTwoUpdateSource.Value1 = ++_value;

    /// <summary>
/// 测量由四属性 WhenAnyValue 观察到的更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("FourPropertiesUpdate")]
    public void WhenAnyValueFourPropertiesUpdate() => _whenAnyFourUpdateSource.Value1 = ++_value;
    #endregion
    #region ReactiveUI

    /// <summary>
/// 测量 ReactiveUI 单属性 WhenAnyValue 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("OnePropertyCreateAndDispose")]
    public void ReactiveUIOnePropertyCreateAndDispose()
    {
        using var subscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUIOneCreateSource, source => source.Value1)
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>
/// 测量 ReactiveUI 双属性 WhenAnyValue 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("TwoPropertiesCreateAndDispose")]
    public void ReactiveUITwoPropertiesCreateAndDispose()
    {
        using var subscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(
                _reactiveUITwoCreateSource,
                source => source.Value1,
                source => source.Value2,
                static (value1, value2) => value1 + value2
            )
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>
/// 测量 ReactiveUI 四属性 WhenAnyValue 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("FourPropertiesCreateAndDispose")]
    public void ReactiveUIFourPropertiesCreateAndDispose()
    {
        using var subscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(
                _reactiveUIFourCreateSource,
                source => source.Value1,
                source => source.Value2,
                source => source.Value3,
                source => source.Value4,
                static (value1, value2, value3, value4) => value1 + value2 + value3 + value4
            )
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>
/// 测量 ReactiveUI 单属性 WhenAnyValue 的更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("OnePropertyUpdate")]
    public void ReactiveUIOnePropertyUpdate() => _reactiveUIOneUpdateSource.Value1 = ++_value;

    /// <summary>
/// 测量由 ReactiveUI 双属性 WhenAnyValue 观察到的更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("TwoPropertiesUpdate")]
    public void ReactiveUITwoPropertiesUpdate() => _reactiveUITwoUpdateSource.Value1 = ++_value;

    /// <summary>
/// 测量由 ReactiveUI 四属性 WhenAnyValue 观察到的更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("FourPropertiesUpdate")]
    public void ReactiveUIFourPropertiesUpdate() => _reactiveUIFourUpdateSource.Value1 = ++_value;

    #endregion


    private sealed class Values : IPropertyChangeNotifier
    {
        private static readonly PropertyChangedEventArgs Value1Changed = new(nameof(Value1));
        private static readonly PropertyChangedEventArgs Value2Changed = new(nameof(Value2));
        private static readonly PropertyChangedEventArgs Value3Changed = new(nameof(Value3));
        private static readonly PropertyChangedEventArgs Value4Changed = new(nameof(Value4));
        private int _value1;
        private int _value2;
        private int _value3;
        private int _value4;

        public event PropertyChangingEventHandler? PropertyChanging;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value1
        {
            get => _value1;
            set => SetValue(ref _value1, value, Value1Changed);
        }

        public int Value2
        {
            get => _value2;
            set => SetValue(ref _value2, value, Value2Changed);
        }

        public int Value3
        {
            get => _value3;
            set => SetValue(ref _value3, value, Value3Changed);
        }

        public int Value4
        {
            get => _value4;
            set => SetValue(ref _value4, value, Value4Changed);
        }

        public void NotifyPropertyChanging(string? propertyName = null) =>
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

        public void NotifyPropertyChanged(string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void SetValue(ref int field, int value, PropertyChangedEventArgs eventArgs)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, eventArgs);
        }
    }

    private sealed class DirectCombinedSubscription : IDisposable
    {
        private readonly Values _source;
        private readonly int _propertyCount;
        private readonly Action<int> _onNext;
        private int _lastValue;
        private bool _hasValue;
        private bool _disposed;

        public DirectCombinedSubscription(Values source, int propertyCount, Action<int> onNext)
        {
            _source = source;
            _propertyCount = propertyCount;
            _onNext = onNext;
            _source.PropertyChanged += OnPropertyChanged;
            PublishCurrentValue();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _source.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            var propertyName = eventArgs.PropertyName;
            if (
                string.IsNullOrEmpty(propertyName)
                || propertyName == nameof(Values.Value1)
                || (_propertyCount >= 2 && propertyName == nameof(Values.Value2))
                || (_propertyCount >= 3 && propertyName == nameof(Values.Value3))
                || (_propertyCount >= 4 && propertyName == nameof(Values.Value4))
            )
            {
                PublishCurrentValue();
            }
        }

        private void PublishCurrentValue()
        {
            var value = _source.Value1;
            if (_propertyCount >= 2)
            {
                value += _source.Value2;
            }

            if (_propertyCount >= 3)
            {
                value += _source.Value3;
            }

            if (_propertyCount >= 4)
            {
                value += _source.Value4;
            }

            if (_hasValue && _lastValue == value)
            {
                return;
            }

            _hasValue = true;
            _lastValue = value;
            _onNext(value);
        }
    }
}
