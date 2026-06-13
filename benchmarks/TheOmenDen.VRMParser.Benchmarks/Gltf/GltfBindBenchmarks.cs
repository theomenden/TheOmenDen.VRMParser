using BenchmarkDotNet.Attributes;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Benchmarks.Gltf;

/// <summary>
/// Isolates the cost of binding the JSON chunk to the strongly-typed Corvus glTF model
/// (<c>ParseGltf</c>) from the container parse measured in <see cref="Glb.GlbParseBenchmarks"/>.
/// Each iteration disposes the parsed document so its pooled memory is returned, keeping the
/// allocation numbers attributable to the bind itself. Reaches the internal typed bridge through the
/// library's <c>InternalsVisibleTo</c> grant.
/// </summary>
public class GltfBindBenchmarks
{
    private GlbDocument _document = null!;

    [ParamsSource(nameof(Inputs))]
    public BenchmarkInput Input { get; set; } = null!;

    public IEnumerable<BenchmarkInput> Inputs => BenchmarkInputs.All();

    [GlobalSetup]
    public void Setup() => _document = GlbDocument.Parse(Input.Bytes).Value;

    /// <summary>Bind the JSON chunk to the typed model and touch the asset header.</summary>
    [Benchmark]
    public int BindGltf()
    {
        using var parsed = _document.ParseGltf();
        return parsed.RootElement.Asset.Version.GetString()?.Length ?? 0;
    }
}
