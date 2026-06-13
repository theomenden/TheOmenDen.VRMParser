using System.Text.Json;
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
    public async Task Fixture_ParsesAndRoundTripsByteForByte(string name)
    {
        byte[] original = Load(name);

        GlbDocument document = GlbDocument.Parse(original);

        document.Version.ShouldBe(GlbDocument.SupportedVersion);
        document.ToBytes().ShouldBe(original);

        await Assert.That(document.Json.IsEmpty).IsFalse();
    }

    [Test]
    public async Task Box_HasBinaryChunk_AndBindsToTypedModel()
    {
        GlbDocument document = GlbDocument.Parse(Load("Box.glb"));

        document.HasBinary.ShouldBeTrue();

        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;
        root.Asset.Version.GetString().ShouldBe("2.0");

        await Assert.That(document.HasBinary).IsTrue();
    }

    [Test]
    public async Task Vrm1_DeclaresVrmcVrmExtensionWithRequiredMetadata()
    {
        GlbDocument document = GlbDocument.Parse(Load("MinimalVrm1.vrm"));

        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;

        gltf.GetProperty("extensionsUsed"u8).EnumerateArray()
            .Select(e => e.GetString()).ShouldContain("VRMC_vrm");

        JsonElement vrm = gltf.GetProperty("extensions"u8).GetProperty("VRMC_vrm"u8);
        vrm.GetProperty("specVersion"u8).GetString().ShouldBe("1.0");
        vrm.GetProperty("meta"u8).GetProperty("name"u8).GetString().ShouldBe("TheOmenDen Test Avatar");
        vrm.GetProperty("humanoid"u8).GetProperty("humanBones"u8).EnumerateObject().Count().ShouldBe(15);

        await Assert.That(document.HasBinary).IsTrue();
    }

    [Test]
    public async Task Vrm0_DeclaresVrmExtension()
    {
        GlbDocument document = GlbDocument.Parse(Load("MinimalVrm0.vrm"));

        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;

        gltf.GetProperty("extensionsUsed"u8).EnumerateArray()
            .Select(e => e.GetString()).ShouldContain("VRM");

        JsonElement vrm = gltf.GetProperty("extensions"u8).GetProperty("VRM"u8);
        vrm.GetProperty("specVersion"u8).GetString().ShouldBe("0.0");
        vrm.GetProperty("humanoid"u8).GetProperty("humanBones"u8).GetArrayLength().ShouldBe(15);

        await Assert.That(gltf.TryGetProperty("extensions"u8, out _)).IsTrue();
    }

    [Test]
    public async Task TryParse_SucceedsForEveryFixture()
    {
        foreach (string name in new[] { "Box.glb", "MinimalVrm0.vrm", "MinimalVrm1.vrm" })
        {
            bool parsed = GlbDocument.TryParse(Load(name), out GlbDocument? document);

            parsed.ShouldBeTrue();
            document.ShouldNotBeNull();
            await Assert.That(parsed).IsTrue();
        }
    }
}
