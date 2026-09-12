using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>Compares nested WhenAnyValue paths with equivalent direct event subscriptions.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class NestedWhenAnyValueBenchmarks
{
    private readonly Root _directCreateRoot = new();
    private readonly Root _whenAnyCreateRoot = new();
    private readonly Root _directUpdateRoot = new();
    private readonly Root _whenAnyUpdateRoot = new();
    private readonly Child _directAlternativeChild = new();
    private readonly Child _whenAnyAlternativeChild = new();
    private IDisposable? _directSubscription;
    private IDisposable? _whenAnySubscription;
    private Child? _directOriginalChild;
    private Child? _whenAnyOriginalChild;
    private bool _useDirectAlternative;
    private bool _useWhenAnyAlternative;
    private int _directResult;
    private int _whenAnyResult;
    private int _value;

    /// <summary>Creates subscriptions used by leaf-update and rebind benchmarks.</summary>
    [GlobalSetup(
        Targets =
        [
            nameof(DirectLeafUpdate),
            nameof(WhenAnyValueLeafUpdate),
            nameof(DirectIntermediateRebind),
            nameof(WhenAnyValueIntermediateRebind),
        ]
    )]
    public void SetupUpdateSubscriptions()
    {
        _directOriginalChild = _directUpdateRoot.Child;
        _whenAnyOriginalChild = _whenAnyUpdateRoot.Child;
        _directSubscription = new DirectNestedSubscription(
            _directUpdateRoot,
            value => _directResult = value
        );
        _whenAnySubscription = _whenAnyUpdateRoot
            .WhenAnyValue(root => root.Child.Value)
            .Subscribe(value => _whenAnyResult = value);
    }

    /// <summary>Disposes subscriptions used by update benchmarks.</summary>
    [GlobalCleanup(
        Targets =
        [
            nameof(DirectLeafUpdate),
            nameof(WhenAnyValueLeafUpdate),
            nameof(DirectIntermediateRebind),
            nameof(WhenAnyValueIntermediateRebind),
        ]
    )]
    public void CleanupUpdateSubscriptions()
    {
        _directSubscription?.Dispose();
        _whenAnySubscription?.Dispose();
    }

    /// <summary>Measures direct nested subscription creation, initial publication, and disposal.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateAndDispose")]
    public void DirectCreateAndDispose()
    {
        using var subscription = new DirectNestedSubscription(
            _directCreateRoot,
            value => _directResult = value
        );
    }

    /// <summary>Measures nested path parsing, reflection subscriptions, initial publication, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void WhenAnyValueCreateAndDispose()
    {
        using var subscription = _whenAnyCreateRoot
            .WhenAnyValue(root => root.Child.Value)
            .Subscribe(value => _whenAnyResult = value);
    }

    /// <summary>Measures a leaf update through direct nested subscriptions.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LeafUpdate")]
    public void DirectLeafUpdate() => _directUpdateRoot.Child.Value = ++_value;

    /// <summary>Measures a leaf update through nested WhenAnyValue.</summary>
    [Benchmark]
    [BenchmarkCategory("LeafUpdate")]
    public void WhenAnyValueLeafUpdate() => _whenAnyUpdateRoot.Child.Value = ++_value;

    /// <summary>Measures direct detachment and reattachment after replacing an intermediate object.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntermediateRebind")]
    public void DirectIntermediateRebind()
    {
        _useDirectAlternative = !_useDirectAlternative;
        var child = _useDirectAlternative ? _directAlternativeChild : _directOriginalChild!;
        child.Value = ++_value;
        _directUpdateRoot.Child = child;
    }

    /// <summary>Measures WhenAnyValue detachment and reattachment after replacing an intermediate object.</summary>
    [Benchmark]
    [BenchmarkCategory("IntermediateRebind")]
    public void WhenAnyValueIntermediateRebind()
    {
        _useWhenAnyAlternative = !_useWhenAnyAlternative;
        var child = _useWhenAnyAlternative ? _whenAnyAlternativeChild : _whenAnyOriginalChild!;
        child.Value = ++_value;
        _whenAnyUpdateRoot.Child = child;
    }

    private sealed class Root : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs ChildChangedEventArgs = new(nameof(Child));
        private Child _child = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public Child Child
        {
            get => _child;
            set
            {
                if (ReferenceEquals(_child, value))
                {
                    return;
                }

                _child = value;
                PropertyChanged?.Invoke(this, ChildChangedEventArgs);
            }
        }
    }

    private sealed class Child : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs ValueChangedEventArgs = new(nameof(Value));
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
                PropertyChanged?.Invoke(this, ValueChangedEventArgs);
            }
        }
    }

    private sealed class DirectNestedSubscription : IDisposable
    {
        private readonly Root _root;
        private readonly Action<int> _onNext;
        private Child? _child;
        private int _lastValue;
        private bool _hasValue;
        private bool _disposed;

        public DirectNestedSubscription(Root root, Action<int> onNext)
        {
            _root = root;
            _onNext = onNext;
            _root.PropertyChanged += OnRootPropertyChanged;
            AttachChild();
            PublishCurrentValue();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _root.PropertyChanged -= OnRootPropertyChanged;
            if (_child is not null)
            {
                _child.PropertyChanged -= OnChildPropertyChanged;
                _child = null;
            }
        }

        private void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == nameof(Root.Child)
            )
            {
                AttachChild();
                PublishCurrentValue();
            }
        }

        private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == nameof(Child.Value)
            )
            {
                PublishCurrentValue();
            }
        }

        private void AttachChild()
        {
            if (_child is not null)
            {
                _child.PropertyChanged -= OnChildPropertyChanged;
            }

            _child = _root.Child;
            _child.PropertyChanged += OnChildPropertyChanged;
        }

        private void PublishCurrentValue()
        {
            var value = _root.Child.Value;
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