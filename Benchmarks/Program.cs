using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = DefaultConfig.Instance;
            config = config.With(ConfigOptions.DisableOptimizationsValidator);
            var summary = BenchmarkRunner.Run<MultipleThreadsScenario>(config);
        }
    }
}