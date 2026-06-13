using System.Text.Json;
using DotNext;
using Shouldly;
using TheOmenDen.VRMParser.Glb;
using TheOmenDen.VRMParser.Models.Records;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>Integration tests over the binary fixtures in <c>Fixtures/</c>.</summary>
public sealed class GlbFixtureTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static byte[] Load(string name) => File.ReadAllBytes(FixturePath(name));

    // Every fixture must parse and survive a parse -> write cycle byte-for-byte. Box.glb is real
    // exporter output, so this is a genuine round-trip correctness check, not a self-test.
    [Test]
    [Arguments("Box.glb")]
    [Arguments("MinimalVrm0.vrm")]
    [Arguments("MinimalVrm1.vrm")]
    public void Fixture_ParsesAndRoundTripsByteForByte(string name)
    {
        byte[] original = Load(name);

        GlbDocument document = GlbDocument.Parse(original).Value;

        document.ShouldSatisfyAllConditions(
            () => document.Version.ShouldBe(GlbDocument.SupportedVersion),
            () => document.ToBytes().ShouldBe(original),
            () => document.Json.IsEmpty.ShouldBeFalse());
    }

    [Test]
    public void Box_HasBinaryChunk_AndBindsToTypedModel()
    {
        GlbDocument document = GlbDocument.Parse(Load("Box.glb")).Value;

        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        document.ShouldSatisfyAllConditions(
            () => document.HasBinary.ShouldBeTrue(),
            () => root.Asset.Version.GetString().ShouldBe("2.0"));
    }

    [Test]
    public void Vrm1_DeclaresVrmcVrmExtensionWithRequiredMetadata()
    {
        GlbDocument document = GlbDocument.Parse(Load("MinimalVrm1.vrm")).Value;

        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;
        JsonElement vrm = gltf.GetProperty("extensions"u8).GetProperty("VRMC_vrm"u8);

        gltf.ShouldSatisfyAllConditions(
            () => gltf.GetProperty("extensionsUsed"u8).EnumerateArray()
                .Select(e => e.GetString()).ShouldContain("VRMC_vrm"),
            () => vrm.GetProperty("specVersion"u8).GetString().ShouldBe("1.0"),
            () => vrm.GetProperty("meta"u8).GetProperty("name"u8).GetString().ShouldBe("TheOmenDen Test Avatar"),
            () => vrm.GetProperty("humanoid"u8).GetProperty("humanBones"u8).EnumerateObject().Count().ShouldBe(15),
            () => document.HasBinary.ShouldBeTrue());
    }

    [Test]
    public void Vrm0_DeclaresVrmExtension()
    {
        GlbDocument document = GlbDocument.Parse(Load("MinimalVrm0.vrm")).Value;

        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;
        JsonElement vrm = gltf.GetProperty("extensions"u8).GetProperty("VRM"u8);

        gltf.ShouldSatisfyAllConditions(
            () => gltf.GetProperty("extensionsUsed"u8).EnumerateArray()
                .Select(e => e.GetString()).ShouldContain("VRM"),
            () => vrm.GetProperty("specVersion"u8).GetString().ShouldBe("0.0"),
            () => vrm.GetProperty("humanoid"u8).GetProperty("humanBones"u8).GetArrayLength().ShouldBe(15),
            () => gltf.TryGetProperty("extensions"u8, out _).ShouldBeTrue());
    }

    [Test]
    public void Parse_SucceedsForEveryFixture()
    {
        foreach (string name in new[] { "Box.glb", "MinimalVrm0.vrm", "MinimalVrm1.vrm" })
        {
            Result<GlbDocument> result = GlbDocument.Parse(Load(name));

            result.ShouldSatisfyAllConditions(
                () => result.IsSuccessful.ShouldBeTrue(),
                () => result.ErrorCode().ShouldBe(GlbErrorCode.None));
        }
    }
}
