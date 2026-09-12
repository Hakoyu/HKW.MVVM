using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>Compares WhenAnyValue with equivalent direct PropertyChanged subscriptions.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class WhenAnyValueBenchmarks
{
    private readonly FastPropertySource _manualFastCreateSource = new();
    private readonly FastPropertySource _whenAnyFastCreateSource = new();
    private readonly PlainPropertySource _manualPlainCreateSource = new();
    private readonly PlainPropertySource _whenAnyPlainCreateSource = new();
    private readonly FastPropertySource _manualFastUpdateSource = new();
    private readonly FastPropertySource _whenAnyFastUpdateSource = new();
    private readonly PlainPropertySource _manualPlainUpdateSource = new();
    private readonly PlainPropertySource _whenAnyPlainUpdateSource = new();
    private IDisposable? _manualFastSubscription;
    private IDisposable? _whenAnyFastSubscription;
    private IDisposable? _manualPlainSubscription;
    private IDisposable? _whenAnyPlainSubscription;
    private int _manualFastValue;
    private int _whenAnyFastValue;
    private int _manualPlainValue;
    private int _whenAnyPlainValue;
    private int _value;

    /// <summary>Creates the long-lived subscriptions used by update benchmarks.</summary>
    [GlobalSetup(
        Targets =
        [
            nameof(DirectFastPathUpdate),
            nameof(WhenAnyValueFastPathUpdate),
            nameof(DirectPlainUpdate),
            nameof(WhenAnyValuePlainUpdate),
        ]
    )]
    public void SetupUpdateSubscriptions()
    {
        _manualFastSubscription = new DirectPropertyChangedSubscription<FastPropertySource, int>(
            _manualFastUpdateSource,
            static source => source.Value,
            nameof(FastPropertySource.Value),
            value => _manualFastValue = value
        );
        _whenAnyFastSubscription = _whenAnyFastUpdateSource
            .WhenAnyValue(source => source.Value)
            .Subscribe(value => _whenAnyFastValue = value);
        _manualPlainSubscription = new DirectPropertyChangedSubscription<PlainPropertySource, int>(
            _manualPlainUpdateSource,
            static source => source.Value,
            nameof(PlainPropertySource.Value),
            value => _manualPlainValue = value
        );
        _whenAnyPlainSubscription = _whenAnyPlainUpdateSource
            .WhenAnyValue(source => source.Value)
            .Subscribe(value => _whenAnyPlainValue = value);
    }

    /// <summary>Disposes the long-lived subscriptions used by update benchmarks.</summary>
    [GlobalCleanup(
        Targets =
        [
            nameof(DirectFastPathUpdate),
            nameof(WhenAnyValueFastPathUpdate),
            nameof(DirectPlainUpdate),
            nameof(WhenAnyValuePlainUpdate),
        ]
    )]
    public void CleanupUpdateSubscriptions()
    {
        _manualFastSubscription?.Dispose();
        _whenAnyFastSubscription?.Dispose();
        _manualPlainSubscription?.Dispose();
        _whenAnyPlainSubscription?.Dispose();
    }

    /// <summary>Measures an equivalent direct PropertyChanged subscription on IPropertyNotifier.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FastPathCreateAndDispose")]
    public void DirectFastPathCreateAndDispose()
    {
        using var subscription = new DirectPropertyChangedSubscription<FastPropertySource, int>(
            _manualFastCreateSource,
            static source => source.Value,
            nameof(FastPropertySource.Value),
            value => _manualFastValue = value
        );
    }

    /// <summary>Measures WhenAnyValue expression compilation, subscription, initial value, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("FastPathCreateAndDispose")]
    public void WhenAnyValueFastPathCreateAndDispose()
    {
        using var subscription = _whenAnyFastCreateSource
            .WhenAnyValue(source => source.Value)
            .Subscribe(value => _whenAnyFastValue = value);
    }

    /// <summary>Measures direct PropertyChanged delivery on IPropertyNotifier.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FastPathUpdate")]
    public void DirectFastPathUpdate() => _manualFastUpdateSource.Value = ++_value;

    /// <summary>Measures WhenAnyValue delivery through its compiled-getter fast path.</summary>
    [Benchmark]
    [BenchmarkCategory("FastPathUpdate")]
    public void WhenAnyValueFastPathUpdate() => _whenAnyFastUpdateSource.Value = ++_value;

    /// <summary>Measures an equivalent direct subscription on plain INotifyPropertyChanged.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PlainCreateAndDispose")]
    public void DirectPlainCreateAndDispose()
    {
        using var subscription = new DirectPropertyChangedSubscription<PlainPropertySource, int>(
            _manualPlainCreateSource,
            static source => source.Value,
            nameof(PlainPropertySource.Value),
            value => _manualPlainValue = value
        );
    }

    /// <summary>Measures WhenAnyValue reflection-path creation, initial value, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("PlainCreateAndDispose")]
    public void WhenAnyValuePlainCreateAndDispose()
    {
        using var subscription = _whenAnyPlainCreateSource
            .WhenAnyValue(source => source.Value)
            .Subscribe(value => _whenAnyPlainValue = value);
    }

    /// <summary>Measures direct PropertyChanged delivery on plain INotifyPropertyChanged.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PlainUpdate")]
    public void DirectPlainUpdate() => _manualPlainUpdateSource.Value = ++_value;

    /// <summary>Measures WhenAnyValue delivery through its reflection-based path.</summary>
    [Benchmark]
    [BenchmarkCategory("PlainUpdate")]
    public void WhenAnyValuePlainUpdate() => _whenAnyPlainUpdateSource.Value = ++_value;

    private sealed class FastPropertySource : IPropertyChangeNotifier
    {
        private int _value;

        public event PropertyChangingEventHandler? PropertyChanging;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public void NotifyPropertyChanging(string? propertyName = null) =>
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

        public void NotifyPropertyChanged(string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class PlainPropertySource : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class DirectPropertyChangedSubscription<TSource, TValue> : IDisposable
        where TSource : class, INotifyPropertyChanged
    {
        private readonly TSource _source;
        private readonly Func<TSource, TValue> _getter;
        private readonly string _propertyName;
        private readonly Action<TValue> _onNext;
        private TValue? _lastValue;
        private bool _hasValue;
        private bool _disposed;

        public DirectPropertyChangedSubscription(
            TSource source,
            Func<TSource, TValue> getter,
            string propertyName,
            Action<TValue> onNext
        )
        {
            _source = source;
            _getter = getter;
            _propertyName = propertyName;
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
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == _propertyName
            )
            {
                PublishCurrentValue();
            }
        }

        private void PublishCurrentValue()
        {
            var value = _getter(_source);
            if (_hasValue && EqualityComparer<TValue>.Default.Equals(_lastValue!, value))
            {
                return;
            }

            _hasValue = true;
            _lastValue = value;
            _onNext(value);
        }
    }
}