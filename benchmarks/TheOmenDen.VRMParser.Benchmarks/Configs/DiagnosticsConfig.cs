using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;

namespace TheOmenDen.VRMParser.Benchmarks.Configs;

/// <summary>
/// The cross-platform default config: the standard BenchmarkDotNet columns/loggers plus the
/// <see cref="MemoryDiagnoser"/> (allocations, GC gen counts) and <see cref="ThreadingDiagnoser"/>
/// (completed work items, lock contention) that cover memory, allocation, and core-utilization
/// reporting. The default config already emits GitHub-flavoured markdown; full JSON is added so CI
/// can publish machine-readable results.
/// </summary>
public static class DiagnosticsConfig
{
    public static ManualConfig Create() =>
        ManualConfig.Create(DefaultConfig.Instance)
            .AddDiagnoser(MemoryDiagnoser.Default, ThreadingDiagnoser.Default)
            .AddExporter(JsonExporter.Full);
}
