using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>Compares expression-setter and assignment-action TwoWayBind overloads.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TwoWayBindBenchmarks
{
    private readonly BindingObject _expressionCreateSource = new();
    private readonly BindingObject _expressionCreateTarget = new();
    private readonly BindingObject _assignmentCreateSource = new();
    private readonly BindingObject _assignmentCreateTarget = new();
    private readonly BindingObject _expressionUpdateSource = new();
    private readonly BindingObject _expressionUpdateTarget = new();
    private readonly BindingObject _assignmentUpdateSource = new();
    private readonly BindingObject _assignmentUpdateTarget = new();
    private IDisposable? _expressionBinding;
    private IDisposable? _assignmentBinding;
    private int _value;

    /// <summary>Creates the long-lived bindings used by the update benchmarks.</summary>
    [GlobalSetup(
        Targets =
        [
            nameof(ExpressionSourceToTargetUpdate),
            nameof(AssignmentSourceToTargetUpdate),
            nameof(ExpressionTargetToSourceUpdate),
            nameof(AssignmentTargetToSourceUpdate),
        ]
    )]
    public void SetupUpdateBindings()
    {
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
    }

    /// <summary>Disposes the long-lived bindings used by the update benchmarks.</summary>
    [GlobalCleanup(
        Targets =
        [
            nameof(ExpressionSourceToTargetUpdate),
            nameof(AssignmentSourceToTargetUpdate),
            nameof(ExpressionTargetToSourceUpdate),
            nameof(AssignmentTargetToSourceUpdate),
        ]
    )]
    public void CleanupUpdateBindings()
    {
        _expressionBinding?.Dispose();
        _assignmentBinding?.Dispose();
    }

    /// <summary>Measures binding creation, two setter compilations, subscriptions, and disposal.</summary>
    [Benchmark(Baseline = true)]
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

    /// <summary>Measures one source-to-target update through an existing expression binding.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SourceToTargetUpdate")]
    public void ExpressionSourceToTargetUpdate() => _expressionUpdateSource.Value = ++_value;

    /// <summary>Measures one source-to-target update through an existing assignment binding.</summary>
    [Benchmark]
    [BenchmarkCategory("SourceToTargetUpdate")]
    public void AssignmentSourceToTargetUpdate() => _assignmentUpdateSource.Value = ++_value;

    /// <summary>Measures one target-to-source update through an existing expression binding.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TargetToSourceUpdate")]
    public void ExpressionTargetToSourceUpdate() => _expressionUpdateTarget.Value = ++_value;

    /// <summary>Measures one target-to-source update through an existing assignment binding.</summary>
    [Benchmark]
    [BenchmarkCategory("TargetToSourceUpdate")]
    public void AssignmentTargetToSourceUpdate() => _assignmentUpdateTarget.Value = ++_value;

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
}