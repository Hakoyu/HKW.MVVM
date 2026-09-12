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
    private readonly Root _reactiveUICreateRoot = new();
    private readonly Root _directUpdateRoot = new();
    private readonly Root _whenAnyUpdateRoot = new();
    private readonly Root _reactiveUIUpdateRoot = new();
    private readonly Child _directAlternativeChild = new();
    private readonly Child _whenAnyAlternativeChild = new();
    private readonly Child _reactiveUIAlternativeChild = new();
    private IDisposable? _directSubscription;
    private IDisposable? _whenAnySubscription;
    private IDisposable? _reactiveUISubscription;
    private Child? _directOriginalChild;
    private Child? _whenAnyOriginalChild;
    private Child? _reactiveUIOriginalChild;
    private bool _useDirectAlternative;
    private bool _useWhenAnyAlternative;
    private bool _useReactiveUIAlternative;
    private int _directResult;
    private int _whenAnyResult;
    private int _reactiveUIResult;
    private int _value;

    [GlobalSetup]
    public void ReactiveUIBuildApp()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
    }

    /// <summary>Creates subscriptions used by leaf-update and rebind benchmarks.</summary>
    [GlobalSetup(
        Targets = [
            nameof(DirectLeafUpdate),
            nameof(WhenAnyValueLeafUpdate),
            nameof(ReactiveUIWhenAnyValueLeafUpdate),
            nameof(DirectIntermediateRebind),
            nameof(WhenAnyValueIntermediateRebind),
            nameof(ReactiveUIWhenAnyValueIntermediateRebind),
        ]
    )]
    public void SetupUpdateSubscriptions()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directOriginalChild = _directUpdateRoot.Child;
        _whenAnyOriginalChild = _whenAnyUpdateRoot.Child;
        _reactiveUIOriginalChild = _reactiveUIUpdateRoot.Child;
        _directSubscription = new DirectNestedSubscription(
            _directUpdateRoot,
            value => _directResult = value
        );
        _whenAnySubscription = _whenAnyUpdateRoot
            .WhenAnyValue(root => root.Child.Value)
            .Subscribe(value => _whenAnyResult = value);
        _reactiveUISubscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUIUpdateRoot, root => root.Child.Value)
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>Disposes subscriptions used by update benchmarks.</summary>
    [GlobalCleanup(
        Targets = [
            nameof(DirectLeafUpdate),
            nameof(WhenAnyValueLeafUpdate),
            nameof(ReactiveUIWhenAnyValueLeafUpdate),
            nameof(DirectIntermediateRebind),
            nameof(WhenAnyValueIntermediateRebind),
            nameof(ReactiveUIWhenAnyValueIntermediateRebind),
        ]
    )]
    public void CleanupUpdateSubscriptions()
    {
        _directSubscription?.Dispose();
        _whenAnySubscription?.Dispose();
        _reactiveUISubscription?.Dispose();
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

    /// <summary>Measures ReactiveUI nested WhenAnyValue creation and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ReactiveUIWhenAnyValueCreateAndDispose()
    {
        using var subscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUICreateRoot, root => root.Child.Value)
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>Measures a leaf update through direct nested subscriptions.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LeafUpdate")]
    public void DirectLeafUpdate() => _directUpdateRoot.Child.Value = ++_value;

    /// <summary>Measures a leaf update through nested WhenAnyValue.</summary>
    [Benchmark]
    [BenchmarkCategory("LeafUpdate")]
    public void WhenAnyValueLeafUpdate() => _whenAnyUpdateRoot.Child.Value = ++_value;

    /// <summary>Measures a leaf update through ReactiveUI nested WhenAnyValue.</summary>
    [Benchmark]
    [BenchmarkCategory("LeafUpdate")]
    public void ReactiveUIWhenAnyValueLeafUpdate() => _reactiveUIUpdateRoot.Child.Value = ++_value;

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

    /// <summary>Measures ReactiveUI re-subscription after replacing an intermediate object.</summary>
    [Benchmark]
    [BenchmarkCategory("IntermediateRebind")]
    public void ReactiveUIWhenAnyValueIntermediateRebind()
    {
        _useReactiveUIAlternative = !_useReactiveUIAlternative;
        var child = _useReactiveUIAlternative
            ? _reactiveUIAlternativeChild
            : _reactiveUIOriginalChild!;
        child.Value = ++_value;
        _reactiveUIUpdateRoot.Child = child;
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
