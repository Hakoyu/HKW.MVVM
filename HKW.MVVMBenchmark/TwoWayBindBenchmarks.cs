using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>Compares TwoWayBind overloads with equivalent direct PropertyChanged handlers.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TwoWayBindBenchmarks
{
    private readonly BindingObject _directCreateSource = new();
    private readonly BindingObject _directCreateTarget = new();
    private readonly BindingObject _expressionCreateSource = new();
    private readonly BindingObject _expressionCreateTarget = new();
    private readonly BindingObject _assignmentCreateSource = new();
    private readonly BindingObject _assignmentCreateTarget = new();
    private readonly BindingObject _reactiveUICreateSource = new();
    private readonly ReactiveUIView _reactiveUICreateTarget = new();
    private readonly BindingObject _directUpdateSource = new();
    private readonly BindingObject _directUpdateTarget = new();
    private readonly BindingObject _expressionUpdateSource = new();
    private readonly BindingObject _expressionUpdateTarget = new();
    private readonly BindingObject _assignmentUpdateSource = new();
    private readonly BindingObject _assignmentUpdateTarget = new();
    private readonly BindingObject _reactiveUIUpdateSource = new();
    private readonly ReactiveUIView _reactiveUIUpdateTarget = new();
    private IDisposable? _directBinding;
    private IDisposable? _expressionBinding;
    private IDisposable? _assignmentBinding;
    private IDisposable? _reactiveUIBinding;
    private int _value;

    /// <summary>Creates the long-lived bindings used by the update benchmarks.</summary>
    [GlobalSetup]
    public void SetupUpdateBindings()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directBinding = new DirectTwoWayBinding(_directUpdateSource, _directUpdateTarget);
        _expressionBinding = _expressionUpdateTarget.TwoWayBind(
            _expressionUpdateSource,
            source => source.Value,
            target => target.Value
        );
        _assignmentBinding = _assignmentUpdateTarget.TwoWayBind(
            _assignmentUpdateSource,
            source => source.Value,
            target => target.Value,
            static (value, target) => target.Value = value,
            static (value, source) => source.Value = value
        );
        _reactiveUIBinding = ReactiveUI.PropertyBindingMixins.Bind(
            _reactiveUIUpdateTarget,
            _reactiveUIUpdateSource,
            source => source.Value,
            target => target.Value
        );
    }

    /// <summary>Disposes the long-lived bindings used by the update benchmarks.</summary>
    [GlobalCleanup]
    public void CleanupUpdateBindings()
    {
        _directBinding?.Dispose();
        _expressionBinding?.Dispose();
        _assignmentBinding?.Dispose();
        _reactiveUIBinding?.Dispose();
    }

    /// <summary>Measures direct event-handler binding creation, initial synchronization, and disposal.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateAndDispose")]
    public void DirectCreateAndDispose()
    {
        using var binding = new DirectTwoWayBinding(_directCreateSource, _directCreateTarget);
    }

    /// <summary>Measures binding creation, two setter compilations, subscriptions, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ExpressionCreateAndDispose()
    {
        using var binding = _expressionCreateTarget.TwoWayBind(
            _expressionCreateSource,
            source => source.Value,
            target => target.Value
        );
    }

    /// <summary>Measures assignment-action binding creation, subscriptions, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void AssignmentCreateAndDispose()
    {
        using var binding = _assignmentCreateTarget.TwoWayBind(
            _assignmentCreateSource,
            source => source.Value,
            target => target.Value,
            static (value, target) => target.Value = value,
            static (value, source) => source.Value = value
        );
    }

    /// <summary>Measures ReactiveUI two-way binding creation and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ReactiveUICreateAndDispose()
    {
        using var binding = ReactiveUI.PropertyBindingMixins.Bind(
            _reactiveUICreateTarget,
            _reactiveUICreateSource,
            source => source.Value,
            target => target.Value
        );
    }

    /// <summary>Measures one source-to-target update through direct event handlers.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SourceToTargetUpdate")]
    public void DirectSourceToTargetUpdate() => _directUpdateSource.Value = ++_value;

    /// <summary>Measures one source-to-target update through an existing expression binding.</summary>
    [Benchmark]
    [BenchmarkCategory("SourceToTargetUpdate")]
    public void ExpressionSourceToTargetUpdate() => _expressionUpdateSource.Value = ++_value;

    /// <summary>Measures one source-to-target update through an existing assignment binding.</summary>
    [Benchmark]
    [BenchmarkCategory("SourceToTargetUpdate")]
    public void AssignmentSourceToTargetUpdate() => _assignmentUpdateSource.Value = ++_value;

    /// <summary>Measures one source-to-target update through an existing ReactiveUI binding.</summary>
    [Benchmark]
    [BenchmarkCategory("SourceToTargetUpdate")]
    public void ReactiveUISourceToTargetUpdate() => _reactiveUIUpdateSource.Value = ++_value;

    /// <summary>Measures one target-to-source update through direct event handlers.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TargetToSourceUpdate")]
    public void DirectTargetToSourceUpdate() => _directUpdateTarget.Value = ++_value;

    /// <summary>Measures one target-to-source update through an existing expression binding.</summary>
    [Benchmark]
    [BenchmarkCategory("TargetToSourceUpdate")]
    public void ExpressionTargetToSourceUpdate() => _expressionUpdateTarget.Value = ++_value;

    /// <summary>Measures one target-to-source update through an existing assignment binding.</summary>
    [Benchmark]
    [BenchmarkCategory("TargetToSourceUpdate")]
    public void AssignmentTargetToSourceUpdate() => _assignmentUpdateTarget.Value = ++_value;

    /// <summary>Measures one target-to-source update through an existing ReactiveUI binding.</summary>
    [Benchmark]
    [BenchmarkCategory("TargetToSourceUpdate")]
    public void ReactiveUITargetToSourceUpdate() => _reactiveUIUpdateTarget.Value = ++_value;

    private sealed class BindingObject : INotifyPropertyChanged
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

    private sealed class ReactiveUIView
        : ReactiveUI.ReactiveObject,
            ReactiveUI.IViewFor<BindingObject>
    {
        private BindingObject? _viewModel;
        private int _value;

        public BindingObject? ViewModel
        {
            get => _viewModel;
            set =>
                ReactiveUI.IReactiveObjectExtensions.RaiseAndSetIfChanged(
                    this,
                    ref _viewModel,
                    value
                );
        }

        object? ReactiveUI.IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (BindingObject?)value;
        }

        public int Value
        {
            get => _value;
            set =>
                ReactiveUI.IReactiveObjectExtensions.RaiseAndSetIfChanged(this, ref _value, value);
        }
    }

    private sealed class DirectTwoWayBinding : IDisposable
    {
        private readonly BindingObject _source;
        private readonly BindingObject _target;
        private bool _updatingSource;
        private bool _updatingTarget;
        private bool _disposed;

        public DirectTwoWayBinding(BindingObject source, BindingObject target)
        {
            _source = source;
            _target = target;
            _target.Value = _source.Value;
            _source.PropertyChanged += OnSourcePropertyChanged;
            _target.PropertyChanged += OnTargetPropertyChanged;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _source.PropertyChanged -= OnSourcePropertyChanged;
            _target.PropertyChanged -= OnTargetPropertyChanged;
        }

        private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (_updatingSource || eventArgs.PropertyName != nameof(BindingObject.Value))
            {
                return;
            }

            try
            {
                _updatingTarget = true;
                _target.Value = _source.Value;
            }
            finally
            {
                _updatingTarget = false;
            }
        }

        private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (_updatingTarget || eventArgs.PropertyName != nameof(BindingObject.Value))
            {
                return;
            }

            try
            {
                _updatingSource = true;
                _source.Value = _target.Value;
            }
            finally
            {
                _updatingSource = false;
            }
        }
    }
}
