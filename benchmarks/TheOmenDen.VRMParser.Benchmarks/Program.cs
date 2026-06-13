using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using TheOmenDen.VRMParser.Benchmarks.Configs;
using TheOmenDen.VRMParser.Benchmarks.Diagnostics;

// `diagnostics [--iterations N]` runs the DotNext GCNotification pause/memory-pressure harness over a
// real-world VRM instead of the BenchmarkDotNet micro-benchmarks.
if (args.Length > 0 && string.Equals(args[0], "diagnostics", StringComparison.OrdinalIgnoreCase))
{
    return GcPauseHarness.Run(args[1..]);
}

// `--hw` opts into the Windows-only ETW hardware counters (cache misses, branch mispredictions);
// everywhere else it is a no-op and the cross-platform config is used.
bool hardwareCounters = args.Any(a => string.Equals(a, "--hw", StringComparison.OrdinalIgnoreCase));
string[] switcherArgs = args.Where(a => !string.Equals(a, "--hw", StringComparison.OrdinalIgnoreCase)).ToArray();

ManualConfig config = hardwareCounters
    ? HardwareCountersConfig.Create()
    : DiagnosticsConfig.Create();

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(switcherArgs, config);

return 0;
