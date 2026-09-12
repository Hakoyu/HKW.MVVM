using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>Compares BindTo overloads with an equivalent direct observer subscription.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BindToBenchmarks
{
    private readonly BenchmarkObservable<int> _directSource = new();
    private readonly BenchmarkObservable<int> _expressionSource = new();
    private readonly BenchmarkObservable<int> _assignmentSource = new();
    private readonly BindingTarget _directTarget = new();
    private readonly BindingTarget _expressionTarget = new();
    private readonly BindingTarget _assignmentTarget = new();
    private IDisposable? _directBinding;
    private IDisposable? _expressionBinding;
    private IDisposable? _assignmentBinding;
    private int _value;

    /// <summary>Creates the long-lived bindings used by the update benchmarks.</summary>
    [GlobalSetup(
        Targets = [nameof(DirectUpdate), nameof(ExpressionUpdate), nameof(AssignmentUpdate)]
    )]
    public void SetupUpdateBindings()
    {
        _directBinding = _directSource.Subscribe(new AssignmentObserver(_directTarget));
        _expressionBinding = _expressionSource.BindTo(_expressionTarget, target => target.Value);
        _assignmentBinding = _assignmentSource.BindTo(
            _assignmentTarget,
            static (value, target) => target.Value = value
        );
    }

    /// <summary>Disposes the long-lived bindings used by the update benchmarks.</summary>
    [GlobalCleanup(
        Targets = [nameof(DirectUpdate), nameof(ExpressionUpdate), nameof(AssignmentUpdate)]
    )]
    public void CleanupUpdateBindings()
    {
        _directBinding?.Dispose();
        _expressionBinding?.Dispose();
        _assignmentBinding?.Dispose();
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

    private sealed class BindingTarget
    {
        public int Value { get; set; }
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

        private sealed class Subscription(
            BenchmarkObservable<T> source,
            IObserver<T> observer
        ) : IDisposable
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