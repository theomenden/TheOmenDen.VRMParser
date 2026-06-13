# TheOmenDen.VRMParser.Benchmarks

Performance and runtime-diagnostics harness for the VRM parser. Two complementary tools:

1. **BenchmarkDotNet micro-benchmarks** — statistical timing of the read/write/bind paths with
   allocation, GC-gen, and threading columns.
2. **GC-pause harness** — a DotNext [`GCNotification`](https://dotnet.github.io/dotNext/features/core/gcnotif.html)
   sampler that reports actual GC pause durations, heap size, and fragmentation during a large parse —
   the one diagnostic `MemoryDiagnoser` does not expose.

## Diagnostic coverage

| Diagnostic       | Where it comes from |
|------------------|---------------------|
| File IO speed    | `GlbParseBenchmarks`, `GlbWriteBenchmarks` (sync vs streaming) |
| Memory usage     | `MemoryDiagnoser` (Allocated) + GC harness peak heap size |
| Allocations      | `MemoryDiagnoser` (Gen0/1/2, Allocated/op) |
| GC pauses        | GC harness — `GCMemoryInfo.PauseDurations` / `PauseTimePercentage` |
| Core utilization | `ThreadingDiagnoser` (work items, lock contention) + opt-in Windows ETW hardware counters |

## Running the benchmarks

Always run in **Release** — BenchmarkDotNet rejects Debug builds.

```bash
# Everything
dotnet run -c Release --project benchmarks/TheOmenDen.VRMParser.Benchmarks -- --filter '*'

# One class, quick pass
dotnet run -c Release --project benchmarks/TheOmenDen.VRMParser.Benchmarks -- --filter '*GlbParse*' --job short
```

Results (GitHub markdown + full JSON) land in `BenchmarkDotNet.Artifacts/results/`.

### Adding the real-world model

The bundled fixtures (`Box.glb`, `MinimalVrm0.vrm`, `MinimalVrm1.vrm`) are ~1.5 KB — fine for
per-call overhead, too small for realistic IO/GC. Point at a full-size avatar to add a `Real:<file>`
input (same env var the test project's `RealWorldVrmTests` uses):

```powershell
$env:VRMPARSER_REAL_VRM_PATH = "C:\path\to\Model.vrm"
dotnet run -c Release --project benchmarks/TheOmenDen.VRMParser.Benchmarks -- --filter '*'
```

### GC-pause harness

```powershell
$env:VRMPARSER_REAL_VRM_PATH = "C:\path\to\Model.vrm"
dotnet run -c Release --project benchmarks/TheOmenDen.VRMParser.Benchmarks -- diagnostics --iterations 500
```

Prints total/max/average pause, collection counts by generation, peak heap, and peak fragmentation.
With the env var unset it prints a short note and exits 0 (the small fixtures produce no useful GC
signal).

### Windows hardware counters (opt-in)

`--hw` adds ETW hardware counters (cache misses, branch mispredictions) and an `EtwProfiler` trace.
**Windows only, run as administrator.** Ignored on other platforms.

```powershell
dotnet run -c Release --project benchmarks/TheOmenDen.VRMParser.Benchmarks -- --hw --filter '*GlbParse*'
```

## CI

`.github/workflows/benchmarks.yml` (`workflow_dispatch`) runs the suite on Linux and uploads
`BenchmarkDotNet.Artifacts` as the `benchmark-results` artifact. No real-world model or hardware
counters in CI, so the run stays self-contained.
