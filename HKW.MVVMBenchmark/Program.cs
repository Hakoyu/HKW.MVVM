using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace HKW.MVVMBenchmark;

internal static class Program
{
    private static void Main(string[] args)
    {
        var config = DefaultConfig.Instance.WithOption(ConfigOptions.JoinSummary, true);

        BenchmarkRunner.Run<BindToBenchmarks>(config, args);
        BenchmarkRunner.Run<TwoWayBindBenchmarks>(config, args);
        BenchmarkRunner.Run<WhenAnyValueBenchmarks>(config, args);
        BenchmarkRunner.Run<NestedWhenAnyValueBenchmarks>(config, args);
        BenchmarkRunner.Run<MultiPropertyWhenAnyValueBenchmarks>(config, args);
        BenchmarkRunner.Run<ToPropertyBenchmarks>(config, args);
        BenchmarkRunner.Run<ObservableOperatorBenchmarks>(config, args);
    }
}
