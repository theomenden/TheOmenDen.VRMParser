using System.Buffers.Binary;
using System.Text;
using Bogus;
using DotNext;
using Shouldly;
using TheOmenDen.VRMParser.Glb;
using TheOmenDen.VRMParser.Models.Records;

namespace TheOmenDen.VRMParser.Tests.Glb;

public sealed class GlbDocumentTests
{
    // Cover lengths on, just below, and just above 4-byte boundaries so chunk padding is exercised.
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(7)]
    [Arguments(16)]
    [Arguments(255)]
    [Arguments(1024)]
    public async Task RoundTrip_PreservesRandomBinaryPayload(int length)
    {
        // Deterministic seed keeps the test reproducible run-to-run.
        byte[] binary = new Faker { Random = new Randomizer(length + 1) }.Random.Bytes(length);
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        GlbDocument document = GlbDocument.Parse(glb).Value;

        document.HasBinary.ShouldBeTrue();
        document.Binary.Value.Span[..length].ToArray().ShouldBe(binary);
        // A spec-compliant container survives a parse -> write cycle byte-for-byte.
        document.ToBytes().ShouldBe(glb);

        await Assert.That(document.Binary.Value.Length % 4).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_MinimalJsonOnly_ExposesVersionAndJson()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeTrue();
        result.ErrorCode().ShouldBe(GlbErrorCode.None);

        GlbDocument document = result.Value;
        document.Version.ShouldBe(GlbDocument.SupportedVersion);
        document.HasBinary.ShouldBeFalse();
        document.Binary.HasValue.ShouldBeFalse();
        // JSON chunk is padded to 4 bytes; trimming the space padding yields the original text.
        Encoding.UTF8.GetString(document.Json.Span).TrimEnd().ShouldBe(GlbTestData.MinimalGltfJson);

        await Assert.That(document).IsNotNull();
    }

    [Test]
    public async Task Parse_WithBinaryChunk_ExposesBinaryPayload()
    {
        byte[] binary = [1, 2, 3, 4, 5];
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        GlbDocument document = GlbDocument.Parse(glb).Value;

        document.HasBinary.ShouldBeTrue();
        // Payload is padded to 8 bytes (5 -> 8); the leading bytes are the data, the rest zero padding.
        document.Binary.Value.Span[..5].ToArray().ShouldBe(binary);

        await Assert.That(document.HasBinary).IsTrue();
    }

    [Test]
    public async Task ToBytes_IsByteIdenticalForCompliantInput()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, [9, 8, 7, 6]);

        byte[] roundTripped = GlbDocument.Parse(glb).Value.ToBytes();

        roundTripped.ShouldBe(glb);
        await Assert.That(roundTripped.Length).IsEqualTo(glb.Length);
    }

    [Test]
    public async Task ParseToBytesParse_IsStable()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, [42, 42, 42]);

        GlbDocument first = GlbDocument.Parse(glb).Value;
        GlbDocument second = GlbDocument.Parse(first.ToBytes()).Value;

        second.Version.ShouldBe(first.Version);
        second.Json.ToArray().ShouldBe(first.Json.ToArray());
        second.Binary.Value.ToArray().ShouldBe(first.Binary.Value.ToArray());
        await Assert.That(second.HasBinary).IsEqualTo(first.HasBinary);
    }

    [Test]
    public async Task ConstructThenWrite_PadsAndRoundTrips()
    {
        // Construct from unpadded JSON (27 bytes, not 4-aligned) and assert it reads back cleanly.
        var document = new GlbDocument(Encoding.UTF8.GetBytes(GlbTestData.MinimalGltfJson));

        GlbDocument reparsed = GlbDocument.Parse(document.ToBytes()).Value;

        Encoding.UTF8.GetString(reparsed.Json.Span).TrimEnd().ShouldBe(GlbTestData.MinimalGltfJson);
        await Assert.That(reparsed.HasBinary).IsFalse();
    }

    [Test]
    public async Task ParseGltf_BindsToTypedGltfRoot()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        GlbDocument document = GlbDocument.Parse(glb).Value;

        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        // Read through the strongly-typed Corvus model the bridge exposes.
        string? version = root.Asset.Version.GetString();
        version.ShouldBe("2.0");

        await Assert.That(version).IsEqualTo("2.0");
    }

    [Test]
    public async Task Parse_Garbage_ReturnsFailureWithoutThrowing()
    {
        Result<GlbDocument> result = GlbDocument.Parse(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });

        result.IsSuccessful.ShouldBeFalse();
        // 12 bytes clears the header-length check, so the magic mismatch is what fails it.
        result.ErrorCode().ShouldBe(GlbErrorCode.BadMagic);
        await Assert.That(result.IsSuccessful).IsFalse();
    }

    [Test]
    public async Task Parse_TooShort_ReturnsFailure()
    {
        Result<GlbDocument> result = GlbDocument.Parse(new byte[] { 0x67, 0x6C, 0x54 });

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.Code.ShouldBe(GlbErrorCode.TooShort);
        error.Message.ShouldContain("too short");
        await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.TooShort);
    }

    [Test]
    public async Task Parse_BadMagic_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        glb[0] ^= 0xFF; // corrupt the magic

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.Code.ShouldBe(GlbErrorCode.BadMagic);
        error.Message.ShouldContain("magic");
        await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.BadMagic);
    }

    [Test]
    public async Task Parse_UnsupportedVersion_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, version: 1);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.Code.ShouldBe(GlbErrorCode.UnsupportedVersion);
        error.Message.ShouldContain("version");
        await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.UnsupportedVersion);
    }

    [Test]
    public async Task Parse_FirstChunkNotJson_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        // Overwrite the first chunk's type (at offset 16) with the BIN type.
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), GlbDocument.BinaryChunkType);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.Code.ShouldBe(GlbErrorCode.FirstChunkNotJson);
        error.Message.ShouldContain("first GLB chunk must be JSON");
        await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.FirstChunkNotJson);
    }

    [Test]
    public async Task Parse_ChunkLengthOverrunsBuffer_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        // Inflate the JSON chunk length (at offset 12) past the end of the buffer, kept 4-aligned so the
        // overrun check — not the alignment check — is what fails.
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), 0xFFFCu);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        result.ErrorCode().ShouldBe(GlbErrorCode.ChunkOverrun);
        await Assert.That(result.IsSuccessful).IsFalse();
    }

    [Test]
    public async Task Parse_UnalignedChunkLength_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        // 4-aligned JSON chunk length minus 1 => not a multiple of 4.
        uint current = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), current - 1);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.Code.ShouldBe(GlbErrorCode.ChunkUnaligned);
        error.Message.ShouldContain("4-byte aligned");
        await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.ChunkUnaligned);
    }

    [Test]
    public async Task Parse_DeclaredLengthExceedsActual_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)glb.Length + 16);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.Code.ShouldBe(GlbErrorCode.DeclaredLengthExceedsData);
        error.Message.ShouldContain("truncated");
        await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.DeclaredLengthExceedsData);
    }
}
