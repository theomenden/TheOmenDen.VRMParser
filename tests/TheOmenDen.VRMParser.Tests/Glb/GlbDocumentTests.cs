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
    public void RoundTrip_PreservesRandomBinaryPayload(int length)
    {
        // Deterministic seed keeps the test reproducible run-to-run.
        byte[] binary = new Faker { Random = new Randomizer(length + 1) }.Random.Bytes(length);
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        GlbDocument document = GlbDocument.Parse(glb).Value;

        document.ShouldSatisfyAllConditions(
            () => document.HasBinary.ShouldBeTrue(),
            () => document.Binary.Value.Span[..length].ToArray().ShouldBe(binary),
            // A spec-compliant container survives a parse -> write cycle byte-for-byte.
            () => document.ToBytes().ShouldBe(glb),
            () => (document.Binary.Value.Length % 4).ShouldBe(0));
    }

    [Test]
    public void Parse_MinimalJsonOnly_ExposesVersionAndJson()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeTrue();
        GlbDocument document = result.Value;
        document.ShouldSatisfyAllConditions(
            () => result.ErrorCode().ShouldBe(GlbErrorCode.None),
            () => document.Version.ShouldBe(GlbDocument.SupportedVersion),
            () => document.HasBinary.ShouldBeFalse(),
            () => document.Binary.HasValue.ShouldBeFalse(),
            // JSON chunk is padded to 4 bytes; trimming the space padding yields the original text.
            () => Encoding.UTF8.GetString(document.Json.Span).TrimEnd().ShouldBe(GlbTestData.MinimalGltfJson));
    }

    [Test]
    public void Parse_WithBinaryChunk_ExposesBinaryPayload()
    {
        byte[] binary = [1, 2, 3, 4, 5];
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        GlbDocument document = GlbDocument.Parse(glb).Value;

        document.ShouldSatisfyAllConditions(
            () => document.HasBinary.ShouldBeTrue(),
            // Payload is padded to 8 bytes (5 -> 8); the leading bytes are the data, the rest zero padding.
            () => document.Binary.Value.Span[..5].ToArray().ShouldBe(binary));
    }

    [Test]
    public void ToBytes_IsByteIdenticalForCompliantInput()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, [9, 8, 7, 6]);

        byte[] roundTripped = GlbDocument.Parse(glb).Value.ToBytes();

        roundTripped.ShouldSatisfyAllConditions(
            () => roundTripped.ShouldBe(glb),
            () => roundTripped.Length.ShouldBe(glb.Length));
    }

    [Test]
    public void ParseToBytesParse_IsStable()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, [42, 42, 42]);

        GlbDocument first = GlbDocument.Parse(glb).Value;
        GlbDocument second = GlbDocument.Parse(first.ToBytes()).Value;

        second.ShouldSatisfyAllConditions(
            () => second.Version.ShouldBe(first.Version),
            () => second.Json.ToArray().ShouldBe(first.Json.ToArray()),
            () => second.Binary.Value.ToArray().ShouldBe(first.Binary.Value.ToArray()),
            () => second.HasBinary.ShouldBe(first.HasBinary));
    }

    [Test]
    public void ConstructThenWrite_PadsAndRoundTrips()
    {
        // Construct from unpadded JSON (27 bytes, not 4-aligned) and assert it reads back cleanly.
        var document = new GlbDocument(Encoding.UTF8.GetBytes(GlbTestData.MinimalGltfJson));

        GlbDocument reparsed = GlbDocument.Parse(document.ToBytes()).Value;

        reparsed.ShouldSatisfyAllConditions(
            () => Encoding.UTF8.GetString(reparsed.Json.Span).TrimEnd().ShouldBe(GlbTestData.MinimalGltfJson),
            () => reparsed.HasBinary.ShouldBeFalse());
    }

    [Test]
    public void ParseGltf_BindsToTypedGltfRoot()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        GlbDocument document = GlbDocument.Parse(glb).Value;

        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        // Read through the strongly-typed Corvus model the bridge exposes.
        root.Asset.Version.GetString().ShouldBe("2.0");
    }

    [Test]
    public void Parse_Garbage_ReturnsFailureWithoutThrowing()
    {
        Result<GlbDocument> result = GlbDocument.Parse(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });

        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            // 12 bytes clears the header-length check, so the magic mismatch is what fails it.
            () => result.ErrorCode().ShouldBe(GlbErrorCode.BadMagic));
    }

    [Test]
    public void Parse_TooShort_ReturnsFailure()
    {
        Result<GlbDocument> result = GlbDocument.Parse(new byte[] { 0x67, 0x6C, 0x54 });

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.TooShort),
            () => error.Message.ShouldContain("too short"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.TooShort));
    }

    [Test]
    public void Parse_BadMagic_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        glb[0] ^= 0xFF; // corrupt the magic

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.BadMagic),
            () => error.Message.ShouldContain("magic"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.BadMagic));
    }

    [Test]
    public void Parse_UnsupportedVersion_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, version: 1);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.UnsupportedVersion),
            () => error.Message.ShouldContain("version"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.UnsupportedVersion));
    }

    [Test]
    public void Parse_FirstChunkNotJson_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        // Overwrite the first chunk's type (at offset 16) with the BIN type.
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), GlbDocument.BinaryChunkType);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.FirstChunkNotJson),
            () => error.Message.ShouldContain("first GLB chunk must be JSON"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.FirstChunkNotJson));
    }

    [Test]
    public void Parse_ChunkLengthOverrunsBuffer_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        // Inflate the JSON chunk length (at offset 12) past the end of the buffer, kept 4-aligned so the
        // overrun check — not the alignment check — is what fails.
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), 0xFFFCu);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.ChunkOverrun));
    }

    [Test]
    public void Parse_UnalignedChunkLength_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        // 4-aligned JSON chunk length minus 1 => not a multiple of 4.
        uint current = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), current - 1);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.ChunkUnaligned),
            () => error.Message.ShouldContain("4-byte aligned"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.ChunkUnaligned));
    }

    [Test]
    public void Parse_DeclaredLengthExceedsActual_ReturnsFailure()
    {
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)glb.Length + 16);

        Result<GlbDocument> result = GlbDocument.Parse(glb);

        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.DeclaredLengthExceedsData),
            () => error.Message.ShouldContain("truncated"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.DeclaredLengthExceedsData));
    }
}
