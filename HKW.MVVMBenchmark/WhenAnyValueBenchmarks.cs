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
    private readonly PropertySource _directCreateSource = new();
    private readonly PropertySource _whenAnyCreateSource = new();
    private readonly PropertySource _reactiveUICreateSource = new();
    private readonly PropertySource _directUpdateSource = new();
    private readonly PropertySource _whenAnyUpdateSource = new();
    private readonly PropertySource _reactiveUIUpdateSource = new();
    private IDisposable? _directSubscription;
    private IDisposable? _whenAnySubscription;
    private IDisposable? _reactiveUISubscription;
    private int _directValue;
    private int _whenAnyValue;
    private int _reactiveUIValue;
    private int _value;

    /// <summary>Creates the long-lived subscriptions used by update benchmarks.</summary>
    [GlobalSetup]
    public void SetupUpdateSubscriptions()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directSubscription = new DirectPropertyChangedSubscription<PropertySource, int>(
            _directUpdateSource,
            static source => source.Value,
            nameof(PropertySource.Value),
            value => _directValue = value
        );
        _whenAnySubscription = _whenAnyUpdateSource
            .WhenAnyValue(source => source.Value)
            .Subscribe(value => _whenAnyValue = value);
        _reactiveUISubscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUIUpdateSource, source => source.Value)
            .Subscribe(value => _reactiveUIValue = value);
    }

    /// <summary>Disposes the long-lived subscriptions used by update benchmarks.</summary>
    [GlobalCleanup(
        Targets = [
            nameof(DirectUpdate),
            nameof(WhenAnyValueUpdate),
            nameof(ReactiveUIWhenAnyValueUpdate),
        ]
    )]
    public void CleanupUpdateSubscriptions()
    {
        _directSubscription?.Dispose();
        _whenAnySubscription?.Dispose();
        _reactiveUISubscription?.Dispose();
    }

    /// <summary>Measures an equivalent direct PropertyChanged subscription.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateAndDispose")]
    public void DirectCreateAndDispose()
    {
        using var subscription = new DirectPropertyChangedSubscription<PropertySource, int>(
            _directCreateSource,
            static source => source.Value,
            nameof(PropertySource.Value),
            value => _directValue = value
        );
    }

    /// <summary>Measures WhenAnyValue subscription, initial value publication, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void WhenAnyValueCreateAndDispose()
    {
        using var subscription = _whenAnyCreateSource
            .WhenAnyValue(source => source.Value)
            .Subscribe(value => _whenAnyValue = value);
    }

    /// <summary>Measures ReactiveUI WhenAnyValue creation, initial publication, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ReactiveUIWhenAnyValueCreateAndDispose()
    {
        using var subscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUICreateSource, source => source.Value)
            .Subscribe(value => _reactiveUIValue = value);
    }

    /// <summary>Measures direct PropertyChanged delivery.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Update")]
    public void DirectUpdate() => _directUpdateSource.Value = ++_value;

    /// <summary>Measures WhenAnyValue delivery for a direct property.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void WhenAnyValueUpdate() => _whenAnyUpdateSource.Value = ++_value;

    /// <summary>Measures ReactiveUI WhenAnyValue delivery for a direct property.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void ReactiveUIWhenAnyValueUpdate() => _reactiveUIUpdateSource.Value = ++_value;

    private sealed class PropertySource : INotifyPropertyChanged
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
