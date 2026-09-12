using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>
/// Compares BindTo overloads with direct observer subscription and traditional PropertyChanged binding.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BindToBenchmarks
{
    private readonly BenchmarkObservable<int> _directSource = new();
    private readonly BenchmarkObservable<int> _expressionSource = new();
    private readonly BenchmarkObservable<int> _assignmentSource = new();
    private readonly BenchmarkObservable<int> _reactiveUISource = new();
    private readonly BindingTarget _directTarget = new();
    private readonly BindingTarget _expressionTarget = new();
    private readonly BindingTarget _assignmentTarget = new();
    private readonly BindingTarget _reactiveUITarget = new();
    private readonly BindingObject _propertyChangedCreateSource = new();
    private readonly BindingTarget _propertyChangedCreateTarget = new();
    private readonly BindingObject _propertyChangedUpdateSource = new();
    private readonly BindingTarget _propertyChangedUpdateTarget = new();
    private IDisposable? _directBinding;
    private IDisposable? _expressionBinding;
    private IDisposable? _assignmentBinding;
    private IDisposable? _reactiveUIBinding;
    private IDisposable? _propertyChangedBinding;
    private int _value;

    /// <summary>Creates the long-lived bindings used by the update benchmarks.</summary>
    [GlobalSetup]
    public void SetupUpdateBindings()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directBinding = _directSource.Subscribe(new AssignmentObserver(_directTarget));
        _expressionBinding = _expressionSource.BindTo(_expressionTarget, target => target.Value);
        _assignmentBinding = _assignmentSource.BindTo(
            _assignmentTarget,
            static (value, target) => target.Value = value
        );
        _reactiveUIBinding = ReactiveUI.PropertyBindingMixins.BindTo(
            _reactiveUISource,
            _reactiveUITarget,
            target => target.Value
        );
        _propertyChangedBinding = new PropertyChangedBinding(
            _propertyChangedUpdateSource,
            _propertyChangedUpdateTarget
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
        _propertyChangedBinding?.Dispose();
    }

    /// <summary>Measures direct observer creation, subscription, and disposal.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateAndDispose")]
    public void DirectCreateAndDispose()
    {
        using var binding = _directSource.Subscribe(new AssignmentObserver(_directTarget));
    }

    /// <summary>Measures expression parsing, setter compilation, subscription, and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ExpressionCreateAndDispose()
    {
        using var binding = _expressionSource.BindTo(_expressionTarget, target => target.Value);
    }

    /// <summary>Measures assignment-action subscription and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void AssignmentCreateAndDispose()
    {
        using var binding = _assignmentSource.BindTo(
            _assignmentTarget,
            static (value, target) => target.Value = value
        );
    }

    /// <summary>Measures ReactiveUI expression binding creation and disposal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ReactiveUICreateAndDispose()
    {
        using var binding = ReactiveUI.PropertyBindingMixins.BindTo(
            _reactiveUISource,
            _reactiveUITarget,
            target => target.Value
        );
    }

    /// <summary>Measures traditional PropertyChanged handler registration and removal.</summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void PropertyChangedCreateAndDispose()
    {
        using var binding = new PropertyChangedBinding(
            _propertyChangedCreateSource,
            _propertyChangedCreateTarget
        );
    }

    /// <summary>Measures one value delivery through an existing direct observer subscription.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Update")]
    public void DirectUpdate() => _directSource.Emit(++_value);

    /// <summary>Measures one value delivery through an existing expression binding.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void ExpressionUpdate() => _expressionSource.Emit(++_value);

    /// <summary>Measures one value delivery through an existing assignment-action binding.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void AssignmentUpdate() => _assignmentSource.Emit(++_value);

    /// <summary>Measures one value delivery through an existing ReactiveUI binding.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void ReactiveUIUpdate() => _reactiveUISource.Emit(++_value);

    /// <summary>Measures one value delivery through a traditional PropertyChanged handler.</summary>
    [Benchmark]
    [BenchmarkCategory("Update")]
    public void PropertyChangedUpdate() => _propertyChangedUpdateSource.Value = ++_value;

    private sealed class BindingTarget
    {
        public int Value { get; set; }
    }

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

    private sealed class PropertyChangedBinding : IDisposable
    {
        private readonly BindingObject _source;
        private readonly BindingTarget _target;
        private bool _disposed;

        public PropertyChangedBinding(BindingObject source, BindingTarget target)
        {
            _source = source;
            _target = target;
            _source.PropertyChanged += OnSourcePropertyChanged;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _source.PropertyChanged -= OnSourcePropertyChanged;
        }

        private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(BindingObject.Value))
            {
                _target.Value = _source.Value;
            }
        }
    }

    private sealed class AssignmentObserver(BindingTarget target) : IObserver<int>
    {
        public void OnNext(int value) => target.Value = value;

        public void OnError(Exception error) { }

        public void OnCompleted() { }
    }

    private sealed class BenchmarkObservable<T> : IObservable<T>
    {
        private IObserver<T>? _observer;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
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
