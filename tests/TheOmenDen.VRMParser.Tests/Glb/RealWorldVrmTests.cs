using System.Text.Json;
using DotNext;
using TUnit.Assertions.Enums;
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
    public async Task Parse_ShouldRoundTripByteForByte_WhenReadingRealModel()
    {
        // Arrange
        byte[] original = LoadModelOrSkip();

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsSuccessful).IsTrue();
            await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.None);
            await Assert.That(result.Value.Version).IsEqualTo(GlbDocument.SupportedVersion);
            await Assert.That(result.Value.HasBinary).IsTrue();
            await Assert.That(result.Value.Json.IsEmpty).IsFalse();
            await Assert.That(result.Value.ToBytes()).IsEquivalentTo(original, CollectionOrdering.Matching);
        }
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
        using (Assert.Multiple())
        {
            await Assert.That(async.Version).IsEqualTo(sync.Version);
            await Assert.That(async.Json.ToArray()).IsEquivalentTo(sync.Json.ToArray(), CollectionOrdering.Matching);
            await Assert.That(async.HasBinary).IsEqualTo(sync.HasBinary);
            await Assert.That(async.Binary.Value.ToArray()).IsEquivalentTo(sync.Binary.Value.ToArray(), CollectionOrdering.Matching);
            await Assert.That(async.ToBytes()).IsEquivalentTo(original, CollectionOrdering.Matching);
        }
    }

    // The JSON chunk must bind to the strongly-typed glTF core model and expose a coherent glTF 2.0
    // asset header — proving the typed bridge works on real, non-synthesized JSON.
    [Test]
    public async Task ParseGltf_ShouldBindTypedModelWithGltf2Asset_WhenReadingRealModel()
    {
        // Arrange
        GlbDocument document = GlbDocument.Parse(LoadModelOrSkip()).Value;

        // Act
        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        // Assert
        await Assert.That(root.Asset.Version.GetString()).IsEqualTo(GlbTestData.GltfAssetVersion);
    }

    // A real avatar carries a VRM extension (0.x VRM or 1.0 VRMC_vrm); whichever it is must be
    // present and preserved verbatim in the JSON chunk so it survives a round-trip.
    [Test]
    public async Task Parse_ShouldPreserveVrmExtension_WhenReadingRealModel()
    {
        // Arrange
        GlbDocument document = GlbDocument.Parse(LoadModelOrSkip()).Value;

        // Act
        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;
        bool hasExtensions = gltf.TryGetProperty(VrmJson.Extensions, out JsonElement extensions);
        bool isVrm0 = hasExtensions && extensions.TryGetProperty(VrmJson.Vrm0Extension, out _);
        bool isVrm1 = hasExtensions && extensions.TryGetProperty(VrmJson.Vrm1Extension, out _);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(hasExtensions).IsTrue().Because("real avatar export should declare glTF extensions");
            await Assert.That(isVrm0 || isVrm1).IsTrue()
                .Because($"expected a VRM 0.x ({VrmJson.Vrm0Extension}) or VRM 1.0 ({VrmJson.Vrm1Extension}) extension");
            await Assert.That(gltf.GetProperty(VrmJson.ExtensionsUsed).EnumerateArray()
                .Select(e => e.GetString()).ToList()).Contains(isVrm1 ? VrmJson.Vrm1Extension : VrmJson.Vrm0Extension);
        }
    }
}
