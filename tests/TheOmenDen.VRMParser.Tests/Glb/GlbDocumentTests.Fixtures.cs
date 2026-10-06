using System.Text.Json;
using DotNext;
using TUnit.Assertions.Enums;
using TheOmenDen.VRMParser.Glb;
using TheOmenDen.VRMParser.Models.Records;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>Integration tests over the binary fixtures in <c>Fixtures/</c>.</summary>
public sealed partial class GlbDocumentTests
{
    private static byte[] Load(string name) => File.ReadAllBytes(Fixtures.PathOf(name));

    private static List<string?> ExtensionsUsed(JsonElement gltf) =>
        gltf.GetProperty(VrmJson.ExtensionsUsed).EnumerateArray().Select(e => e.GetString()).ToList();

    // Every fixture must parse and survive a parse -> write cycle byte-for-byte. Box.glb is real
    // exporter output, so this is a genuine round-trip correctness check, not a self-test.
    [Test]
    [Arguments(Fixtures.Box)]
    [Arguments(Fixtures.MinimalVrm0)]
    [Arguments(Fixtures.MinimalVrm1)]
    public async Task Parse_ShouldRoundTripByteForByte_WhenReadingFixture(string name)
    {
        // Arrange
        byte[] original = Load(name);

        // Act
        GlbDocument document = GlbDocument.Parse(original).Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.Version).IsEqualTo(GlbDocument.SupportedVersion);
            await Assert.That(document.ToBytes()).IsEquivalentTo(original, CollectionOrdering.Matching);
            await Assert.That(document.Json.IsEmpty).IsFalse();
        }
    }

    [Test]
    public async Task Parse_ShouldExposeBinaryAndBindTypedModel_WhenReadingBoxFixture()
    {
        // Arrange & Act
        GlbDocument document = GlbDocument.Parse(Load(Fixtures.Box)).Value;
        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.HasBinary).IsTrue();
            await Assert.That(root.Asset.Version.GetString()).IsEqualTo(GlbTestData.GltfAssetVersion);
        }
    }

    [Test]
    public async Task Parse_ShouldExposeVrmcVrmExtensionMetadata_WhenReadingVrm1Fixture()
    {
        // Arrange & Act
        GlbDocument document = GlbDocument.Parse(Load(Fixtures.MinimalVrm1)).Value;
        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;
        JsonElement vrm = gltf.GetProperty(VrmJson.Extensions).GetProperty(VrmJson.Vrm1Extension);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ExtensionsUsed(gltf)).Contains(VrmJson.Vrm1Extension);
            await Assert.That(vrm.GetProperty(VrmJson.SpecVersion).GetString()).IsEqualTo(VrmJson.Vrm1SpecVersion);
            await Assert.That(vrm.GetProperty(VrmJson.Meta).GetProperty(VrmJson.Name).GetString()).IsEqualTo(Fixtures.AvatarName);
            await Assert.That(vrm.GetProperty(VrmJson.Humanoid).GetProperty(VrmJson.HumanBones).EnumerateObject().Count())
                .IsEqualTo(Fixtures.RequiredHumanBoneCount);
            await Assert.That(document.HasBinary).IsTrue();
        }
    }

    [Test]
    public async Task Parse_ShouldExposeVrmExtension_WhenReadingVrm0Fixture()
    {
        // Arrange & Act
        GlbDocument document = GlbDocument.Parse(Load(Fixtures.MinimalVrm0)).Value;
        using JsonDocument json = JsonDocument.Parse(document.Json);
        JsonElement gltf = json.RootElement;
        JsonElement vrm = gltf.GetProperty(VrmJson.Extensions).GetProperty(VrmJson.Vrm0Extension);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ExtensionsUsed(gltf)).Contains(VrmJson.Vrm0Extension);
            await Assert.That(vrm.GetProperty(VrmJson.SpecVersion).GetString()).IsEqualTo(VrmJson.Vrm0SpecVersion);
            await Assert.That(vrm.GetProperty(VrmJson.Humanoid).GetProperty(VrmJson.HumanBones).GetArrayLength())
                .IsEqualTo(Fixtures.RequiredHumanBoneCount);
        }
    }

    [Test]
    public async Task Parse_ShouldSucceed_WhenReadingAnyFixture()
    {
        foreach (string name in Fixtures.All)
        {
            // Arrange & Act
            Result<GlbDocument> result = GlbDocument.Parse(Load(name));

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccessful).IsTrue();
                await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.None);
            }
        }
    }
}
