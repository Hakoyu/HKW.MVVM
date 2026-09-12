using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using ReactiveUI.Builder;

namespace HKW.MVVMBenchmark;

internal static class Program
{
    private static void Main(string[] args)
    {
        var config = DefaultConfig.Instance.AddJob(
            Job.Default.WithWarmupCount(5).WithIterationCount(20)
        );

        //BenchmarkRunner.Run<BindToBenchmarks>(config);
        //BenchmarkRunner.Run<TwoWayBindBenchmarks>(config);
        //BenchmarkRunner.Run<WhenAnyValueBenchmarks>(config);
        //BenchmarkRunner.Run<NestedWhenAnyValueBenchmarks>(config);
        BenchmarkRunner.Run<MultiPropertyWhenAnyValueBenchmarks>(config);
        //BenchmarkRunner.Run<ToPropertyBenchmarks>(config);
        //BenchmarkRunner.Run<ObservableOperatorBenchmarks>(config);
    }
}
