using BenchmarkDotNet.Attributes;
using DotNext;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Benchmarks.Glb;

/// <summary>
/// File-IO / read-path benchmarks for the GLB container: the synchronous span parse versus the
/// DotNext.IO streaming async parse. The sync path is the baseline so the summary's <c>Ratio</c>
/// column shows the streaming overhead, while <c>MemoryDiagnoser</c> exposes the per-chunk array
/// allocations the async path makes.
/// </summary>
public class GlbParseBenchmarks
{
    private MemoryStream _stream = null!;

    [ParamsSource(nameof(Inputs))]
    public BenchmarkInput Input { get; set; } = null!;

    public IEnumerable<BenchmarkInput> Inputs => BenchmarkInputs.All();

    [GlobalSetup]
    public void Setup() => _stream = new MemoryStream(Input.Bytes.ToArray(), writable: false);

    [GlobalCleanup]
    public void Cleanup() => _stream.Dispose();

    /// <summary>Synchronous, single-pass parse over the in-memory bytes (no streaming).</summary>
    [Benchmark(Baseline = true)]
    public GlbDocument ParseSync() => GlbDocument.Parse(Input.Bytes).Value;

    /// <summary>Streaming parse over a <see cref="Stream"/> via DotNext.IO's async binary reader.</summary>
    [Benchmark]
    public async Task<GlbDocument> ParseAsyncStream()
    {
        _stream.Position = 0;
        Result<GlbDocument> result = await GlbDocument.ParseAsync(_stream);
        return result.Value;
    }
}
