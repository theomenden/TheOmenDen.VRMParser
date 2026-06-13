using System.Buffers.Binary;
using System.Text;
using Bogus;
using DotNext;
using Shouldly;
using TheOmenDen.VRMParser.Glb;
using TheOmenDen.VRMParser.Models.Records;

namespace TheOmenDen.VRMParser.Tests.Glb;

public sealed partial class GlbDocumentTests
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
    public void Parse_ShouldRoundTripBinaryPayloadByteForByte_WhenLengthVaries(int length)
    {
        // Arrange — deterministic seed keeps the test reproducible run-to-run.
        byte[] binary = new Faker { Random = new Randomizer(length + 1) }.Random.Bytes(length);
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        // Act
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Assert
        document.ShouldSatisfyAllConditions(
            () => document.HasBinary.ShouldBeTrue(),
            () => document.Binary.Value.Span[..length].ToArray().ShouldBe(binary),
            // A spec-compliant container survives a parse -> write cycle byte-for-byte.
            () => document.ToBytes().ShouldBe(glb),
            () => (document.Binary.Value.Length % 4).ShouldBe(0));
    }

    [Test]
    public void Parse_ShouldExposeVersionAndJson_WhenContainerIsJsonOnly()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
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
    public void Parse_ShouldExposeBinaryPayload_WhenContainerHasBinaryChunk()
    {
        // Arrange
        byte[] binary = [1, 2, 3, 4, 5];
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        // Act
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Assert
        document.ShouldSatisfyAllConditions(
            () => document.HasBinary.ShouldBeTrue(),
            // Payload is padded to 8 bytes (5 -> 8); the leading bytes are the data, the rest zero padding.
            () => document.Binary.Value.Span[..5].ToArray().ShouldBe(binary));
    }

    [Test]
    public void ToBytes_ShouldBeByteIdentical_WhenInputIsCompliant()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, [9, 8, 7, 6]);

        // Act
        byte[] roundTripped = GlbDocument.Parse(glb).Value.ToBytes();

        // Assert
        roundTripped.ShouldSatisfyAllConditions(
            () => roundTripped.ShouldBe(glb),
            () => roundTripped.Length.ShouldBe(glb.Length));
    }

    [Test]
    public void ParseToBytesParse_ShouldProduceStableModel_WhenReparsed()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, [42, 42, 42]);

        // Act
        GlbDocument first = GlbDocument.Parse(glb).Value;
        GlbDocument second = GlbDocument.Parse(first.ToBytes()).Value;

        // Assert
        second.ShouldSatisfyAllConditions(
            () => second.Version.ShouldBe(first.Version),
            () => second.Json.ToArray().ShouldBe(first.Json.ToArray()),
            () => second.Binary.Value.ToArray().ShouldBe(first.Binary.Value.ToArray()),
            () => second.HasBinary.ShouldBe(first.HasBinary));
    }

    [Test]
    public void ToBytes_ShouldPadAndRoundTrip_WhenConstructedFromUnpaddedJson()
    {
        // Arrange — construct from unpadded JSON (27 bytes, not 4-aligned).
        var document = new GlbDocument(Encoding.UTF8.GetBytes(GlbTestData.MinimalGltfJson));

        // Act
        GlbDocument reparsed = GlbDocument.Parse(document.ToBytes()).Value;

        // Assert
        reparsed.ShouldSatisfyAllConditions(
            () => Encoding.UTF8.GetString(reparsed.Json.Span).TrimEnd().ShouldBe(GlbTestData.MinimalGltfJson),
            () => reparsed.HasBinary.ShouldBeFalse());
    }

    [Test]
    public void ParseGltf_ShouldBindToTypedGltfRoot_WhenJsonIsValid()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Act
        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        // Assert — read through the strongly-typed Corvus model the bridge exposes.
        root.Asset.Version.GetString().ShouldBe("2.0");
    }

    [Test]
    public void Parse_ShouldReturnFailureWithoutThrowing_WhenDataIsGarbage()
    {
        // Arrange & Act
        Result<GlbDocument> result = GlbDocument.Parse(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            // 12 bytes clears the header-length check, so the magic mismatch is what fails it.
            () => result.ErrorCode().ShouldBe(GlbErrorCode.BadMagic));
    }

    [Test]
    public void Parse_ShouldReturnTooShortError_WhenDataShorterThanHeader()
    {
        // Arrange & Act
        Result<GlbDocument> result = GlbDocument.Parse(new byte[] { 0x67, 0x6C, 0x54 });

        // Assert
        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.TooShort),
            () => error.Message.ShouldContain("too short"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.TooShort));
    }

    [Test]
    public void Parse_ShouldReturnBadMagicError_WhenMagicIsWrong()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        glb[0] ^= 0xFF; // corrupt the magic

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.BadMagic),
            () => error.Message.ShouldContain("magic"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.BadMagic));
    }

    [Test]
    public void Parse_ShouldReturnUnsupportedVersionError_WhenVersionIsNotTwo()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, version: 1);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.UnsupportedVersion),
            () => error.Message.ShouldContain("version"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.UnsupportedVersion));
    }

    [Test]
    public void Parse_ShouldReturnFirstChunkNotJsonError_WhenFirstChunkIsNotJson()
    {
        // Arrange — overwrite the first chunk's type (at offset 16) with the BIN type.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), GlbDocument.BinaryChunkType);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.FirstChunkNotJson),
            () => error.Message.ShouldContain("first GLB chunk must be JSON"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.FirstChunkNotJson));
    }

    [Test]
    public void Parse_ShouldReturnChunkOverrunError_WhenChunkLengthExceedsBuffer()
    {
        // Arrange — inflate the JSON chunk length (at offset 12) past the end of the buffer, kept
        // 4-aligned so the overrun check — not the alignment check — is what fails.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), 0xFFFCu);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.ChunkOverrun));
    }

    [Test]
    public void Parse_ShouldReturnChunkUnalignedError_WhenChunkLengthNotFourByteAligned()
    {
        // Arrange — 4-aligned JSON chunk length minus 1 => not a multiple of 4.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        uint current = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), current - 1);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.ChunkUnaligned),
            () => error.Message.ShouldContain("4-byte aligned"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.ChunkUnaligned));
    }

    [Test]
    public void Parse_ShouldReturnDeclaredLengthExceedsDataError_WhenHeaderLengthExceedsActual()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)glb.Length + 16);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        result.IsSuccessful.ShouldBeFalse();
        GlbFormatException error = result.Error.ShouldBeOfType<GlbFormatException>();
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(GlbErrorCode.DeclaredLengthExceedsData),
            () => error.Message.ShouldContain("truncated"),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.DeclaredLengthExceedsData));
    }
}
