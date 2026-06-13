using BenchmarkDotNet.Attributes;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Benchmarks.Glb;

/// <summary>
/// File-IO / write-path benchmarks: the full-buffer <see cref="GlbDocument.ToBytes"/> versus the
/// buffer-then-write <see cref="GlbDocument.WriteTo(Stream)"/> and the DotNext.IO streaming
/// <see cref="GlbDocument.WriteToAsync(Stream, CancellationToken)"/>. A reused, pre-sized sink stream
/// keeps the measurement on the serializer rather than on stream growth.
/// </summary>
public class GlbWriteBenchmarks
{
    private GlbDocument _document = null!;
    private MemoryStream _sink = null!;

    [ParamsSource(nameof(Inputs))]
    public BenchmarkInput Input { get; set; } = null!;

    public IEnumerable<BenchmarkInput> Inputs => BenchmarkInputs.All();

    [GlobalSetup]
    public void Setup()
    {
        _document = GlbDocument.Parse(Input.Bytes).Value;
        // Pre-size the sink to the serialized length so its capacity never grows mid-benchmark.
        _sink = new MemoryStream(_document.ToBytes().Length);
    }

    [GlobalCleanup]
    public void Cleanup() => _sink.Dispose();

    /// <summary>Serialize to a freshly allocated byte array (full materialization).</summary>
    [Benchmark(Baseline = true)]
    public byte[] ToBytes() => _document.ToBytes();

    /// <summary>Serialize via <see cref="GlbDocument.ToBytes"/> then a single synchronous stream write.</summary>
    [Benchmark]
    public void WriteToStream()
    {
        _sink.Position = 0;
        _document.WriteTo(_sink);
    }

    /// <summary>Stream each chunk straight to the destination via the DotNext.IO async writer.</summary>
    [Benchmark]
    public async Task WriteToStreamAsync()
    {
        _sink.Position = 0;
        await _document.WriteToAsync(_sink);
    }
}
