using BenchmarkDotNet.Configs;
#if WINDOWS
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
#endif

namespace TheOmenDen.VRMParser.Benchmarks.Configs;

/// <summary>
/// Extends <see cref="DiagnosticsConfig"/> with per-core CPU detail via ETW hardware counters
/// (cache misses, branch mispredictions) and an <c>EtwProfiler</c> trace. These are Windows + ETW
/// only and require an elevated (administrator) shell, so the additions compile and apply only under
/// the <c>WINDOWS</c> constant; everywhere else this is identical to the default config.
/// </summary>
public static class HardwareCountersConfig
{
    public static ManualConfig Create()
    {
        ManualConfig config = DiagnosticsConfig.Create();
#if WINDOWS
        config.AddDiagnoser(new EtwProfiler());
        config.AddHardwareCounters(
            HardwareCounter.CacheMisses,
            HardwareCounter.BranchMispredictions,
            HardwareCounter.BranchInstructions);
#endif
        return config;
    }
}
