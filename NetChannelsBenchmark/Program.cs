using BenchmarkDotNet.Running;

namespace NetChannelsBenchmark
{
    internal static class Program
    {
        // Pick a benchmark interactively, or pass a filter, e.g.:
        //   dotnet run -c Release -- --filter "*ObjectChannelBenchmark*"
        private static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
