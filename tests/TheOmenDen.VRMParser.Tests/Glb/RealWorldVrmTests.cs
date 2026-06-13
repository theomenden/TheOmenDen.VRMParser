using System.Text.Json;
using DotNext;
using Shouldly;
using TheOmenDen.VRMParser.Glb;
using TheOmenDen.VRMParser.Models.Records;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>
/// Opt-in integration tests over a full-size, real-world avatar export that is too large (and
/// licence-encumbered) to commit as a fixture. Point the <c>VRMPARSER_REAL_VRM_PATH</c> environment
/// variable at a local <c>.glb</c>/<c>.vrm</c> file to exercise the parser on genuine exporter output
/// — e.g. a ~37&#160;MB UniVRM/VRoid model. When the variable is unset or the file is missing every
/// test here skips, so CI and a fresh clone stay green without the asset.
/// </summary>
public sealed class RealWorldVrmTests
{
    private const string PathEnvVar = "VRMPARSER_REAL_VRM_PATH";

    private static string? ModelPath
    {
        get
        {
            string? path = Environment.GetEnvironmentVariable(PathEnvVar);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    private static byte[] LoadModelOrSkip()
    {
        string? path = ModelPath;
        Skip.Unless(path is not null, $"Set {PathEnvVar} to a real .vrm/.glb file to run real-world integration tests.");
        Skip.Unless(File.Exists(path), $"{PathEnvVar} is set to '{path}', but no file exists there.");
        return File.ReadAllBytes(path!);
    }

    // Real exporter output must parse and survive a parse -> write cycle byte-for-byte, exactly like
    // the committed Box.glb fixture but at full avatar scale (large BIN chunk, hundreds of accessors).
    [Test]
    public void Parse_ShouldRoundTripByteForByte_WhenReadingRealModel()
    {
        // Arrange
        byte[] original = LoadModelOrSkip();

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(original);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeTrue(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.None),
            () => result.Value.Version.ShouldBe(GlbDocument.SupportedVersion),
            () => result.Value.HasBinary.ShouldBeTrue(),
            () => result.Value.Json.IsEmpty.ShouldBeFalse(),
            () => result.Value.ToBytes().ShouldBe(original));
    }

    // The streaming async path must agree with the synchronous path on a real, large container.
    [Test]
    public async Task ParseAsync_ShouldMatchSyncParse_WhenReadingRealModel()
    {
        // Arrange
        byte[] original = LoadModelOrSkip();
        using var stream = new MemoryStream(original, writable: false);

        // Act
        GlbDocument sync = GlbDocument.Parse(original).Value;
        GlbDocument async = (await GlbDocument.ParseAsync(stream)).Value;

        // Assert
        async.ShouldSatisfyAllConditions(
            () => async.Version.ShouldBe(sync.Version),
            () => async.Json.ToArray().ShouldBe(sync.Json.ToArray()),
            () => async.HasBinary.ShouldBe(sync.HasBinary),
            () => async.Binary.Value.ToArray().ShouldBe(sync.Binary.Value.ToArray()),
            () => async.ToBytes().ShouldBe(original));
    }

    // The JSON chunk must bind to the strongly-typed glTF core model and expose a coherent glTF 2.0
    // asset header — proving the typed bridge works on real, non-synthesized JSON.
    [Test]
    public void ParseGltf_ShouldBindTypedModelWithGltf2Asset_WhenReadingRealModel()
    {
        // Arrange
        GlbDocument document = GlbDocument.Parse(LoadModelOrSkip()).Value;

        // Act
        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        // Assert
        root.Asset.Version.GetString().ShouldBe("2.0");
    }

    // A real avatar carries a VRM extension (0.x VRM or 1.0 VRMC_vrm); whichever it is must be
    // present and preserved verbatim in the JSON chunk so it survives a round-trip.
    [Test]
    public void Parse_ShouldPreserveVrmExtension_WhenReadingRealModel()
    {
        // Arrange
        GlbDocument document = GlbDocument.Parse(LoadModelOrSkip()).Value;

        // Act
        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;
        bool hasExtensions = gltf.TryGetProperty("extensions"u8, out JsonElement extensions);
        bool isVrm0 = hasExtensions && extensions.TryGetProperty("VRM"u8, out _);
        bool isVrm1 = hasExtensions && extensions.TryGetProperty("VRMC_vrm"u8, out _);

        // Assert
        gltf.ShouldSatisfyAllConditions(
            () => hasExtensions.ShouldBeTrue("real avatar export should declare glTF extensions"),
            () => (isVrm0 || isVrm1).ShouldBeTrue("expected a VRM 0.x (VRM) or VRM 1.0 (VRMC_vrm) extension"),
            () => gltf.GetProperty("extensionsUsed"u8).EnumerateArray()
                .Select(e => e.GetString())
                .ShouldContain(isVrm1 ? "VRMC_vrm" : "VRM"));
    }
}
