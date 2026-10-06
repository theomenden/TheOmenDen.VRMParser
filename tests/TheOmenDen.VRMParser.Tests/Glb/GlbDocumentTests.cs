using System.Buffers.Binary;
using System.Text;
using DotNext;
using TUnit.Assertions.Enums;
using TheOmenDen.VRMParser.Glb;
using TheOmenDen.VRMParser.Models.Records;

namespace TheOmenDen.VRMParser.Tests.Glb;

public sealed partial class GlbDocumentTests
{
    // Asserts a parse failed with a GlbFormatException carrying the expected code, on both the error
    // itself and the null-safe ErrorCode() accessor. The code (not message text) is the contract.
    private static async Task AssertFailsWith(Result<GlbDocument> result, GlbErrorCode expected)
    {
        await Assert.That(result.IsSuccessful).IsFalse();
        GlbFormatException? error = await Assert.That(result.Error).IsTypeOf<GlbFormatException>();

        using (Assert.Multiple())
        {
            await Assert.That(error!.Code).IsEqualTo(expected);
            await Assert.That(result.ErrorCode()).IsEqualTo(expected);
            await Assert.That(error.Message).IsNotEmpty();
        }
    }

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
    public async Task Parse_ShouldRoundTripBinaryPayloadByteForByte_WhenLengthVaries(int length)
    {
        // Arrange
        byte[] binary = GlbTestData.Payload(length);
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        // Act
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.HasBinary).IsTrue();
            await Assert.That(document.Binary.Value.Span[..length].ToArray()).IsEquivalentTo(binary, CollectionOrdering.Matching);
            // A spec-compliant container survives a parse -> write cycle byte-for-byte.
            await Assert.That(document.ToBytes()).IsEquivalentTo(glb, CollectionOrdering.Matching);
            await Assert.That(document.Binary.Value.Length % GlbTestData.ChunkAlignment).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Parse_ShouldExposeVersionAndJson_WhenContainerIsJsonOnly()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await Assert.That(result.IsSuccessful).IsTrue();
        GlbDocument document = result.Value;
        using (Assert.Multiple())
        {
            await Assert.That(result.ErrorCode()).IsEqualTo(GlbErrorCode.None);
            await Assert.That(document.Version).IsEqualTo(GlbDocument.SupportedVersion);
            await Assert.That(document.HasBinary).IsFalse();
            await Assert.That(document.Binary.HasValue).IsFalse();
            // JSON chunk is padded to 4 bytes; trimming the space padding yields the original text.
            await Assert.That(Encoding.UTF8.GetString(document.Json.Span).TrimEnd()).IsEqualTo(GlbTestData.MinimalGltfJson);
        }
    }

    [Test]
    public async Task Parse_ShouldExposeBinaryPayload_WhenContainerHasBinaryChunk()
    {
        // Arrange — one byte past a 4-byte boundary, so the chunk carries zero padding.
        byte[] binary = GlbTestData.Payload(GlbTestData.ChunkAlignment + 1);
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);

        // Act
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.HasBinary).IsTrue();
            // The leading bytes are the data, the rest zero padding.
            await Assert.That(document.Binary.Value.Span[..binary.Length].ToArray()).IsEquivalentTo(binary, CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task ToBytes_ShouldBeByteIdentical_WhenInputIsCompliant()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, GlbTestData.Payload(GlbTestData.ChunkAlignment));

        // Act
        byte[] roundTripped = GlbDocument.Parse(glb).Value.ToBytes();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(roundTripped).IsEquivalentTo(glb, CollectionOrdering.Matching);
            await Assert.That(roundTripped.Length).IsEqualTo(glb.Length);
        }
    }

    [Test]
    public async Task ParseToBytesParse_ShouldProduceStableModel_WhenReparsed()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, GlbTestData.Payload(GlbTestData.ChunkAlignment - 1));

        // Act
        GlbDocument first = GlbDocument.Parse(glb).Value;
        GlbDocument second = GlbDocument.Parse(first.ToBytes()).Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(second.Version).IsEqualTo(first.Version);
            await Assert.That(second.Json.ToArray()).IsEquivalentTo(first.Json.ToArray(), CollectionOrdering.Matching);
            await Assert.That(second.Binary.Value.ToArray()).IsEquivalentTo(first.Binary.Value.ToArray(), CollectionOrdering.Matching);
            await Assert.That(second.HasBinary).IsEqualTo(first.HasBinary);
        }
    }

    [Test]
    public async Task ToBytes_ShouldPadAndRoundTrip_WhenConstructedFromUnpaddedJson()
    {
        // Arrange — the minimal JSON is not 4-byte aligned, so the writer must add padding.
        var document = new GlbDocument(Encoding.UTF8.GetBytes(GlbTestData.MinimalGltfJson));

        // Act
        GlbDocument reparsed = GlbDocument.Parse(document.ToBytes()).Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Encoding.UTF8.GetString(reparsed.Json.Span).TrimEnd()).IsEqualTo(GlbTestData.MinimalGltfJson);
            await Assert.That(reparsed.HasBinary).IsFalse();
        }
    }

    [Test]
    public async Task ParseGltf_ShouldBindToTypedGltfRoot_WhenJsonIsValid()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Act
        using var parsed = document.ParseGltf();
        GltfRoot root = parsed.RootElement;

        // Assert — read through the strongly-typed Corvus model the bridge exposes.
        await Assert.That(root.Asset.Version.GetString()).IsEqualTo(GlbTestData.GltfAssetVersion);
    }

    [Test]
    public async Task Parse_ShouldReturnFailureWithoutThrowing_WhenDataIsGarbage()
    {
        // Arrange — a zeroed, header-sized buffer clears the length check, so the magic mismatch is what fails it.
        byte[] garbage = new byte[GlbTestData.HeaderSize];

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(garbage);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.BadMagic);
    }

    [Test]
    public async Task Parse_ShouldReturnTooShortError_WhenDataShorterThanHeader()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson)[..(GlbTestData.HeaderSize - 1)];

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.TooShort);
    }

    [Test]
    public async Task Parse_ShouldReturnBadMagicError_WhenMagicIsWrong()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb, ~GlbDocument.Magic);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.BadMagic);
    }

    [Test]
    public async Task Parse_ShouldReturnUnsupportedVersionError_WhenVersionIsNotSupported()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, version: GlbDocument.SupportedVersion - 1);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.UnsupportedVersion);
    }

    [Test]
    public async Task Parse_ShouldReturnFirstChunkNotJsonError_WhenFirstChunkIsNotJson()
    {
        // Arrange — overwrite the first chunk's type with the BIN type.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(GlbTestData.FirstChunkTypeOffset), GlbDocument.BinaryChunkType);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.FirstChunkNotJson);
    }

    [Test]
    public async Task Parse_ShouldReturnChunkOverrunError_WhenChunkLengthExceedsBuffer()
    {
        // Arrange — inflate the JSON chunk length past the end of the buffer, kept 4-aligned (the built
        // buffer length is aligned) so the overrun check — not the alignment check — is what fails.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(GlbTestData.FirstChunkLengthOffset), (uint)glb.Length);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.ChunkOverrun);
    }

    [Test]
    public async Task Parse_ShouldReturnChunkUnalignedError_WhenChunkLengthNotFourByteAligned()
    {
        // Arrange — 4-aligned JSON chunk length minus 1 => not a multiple of 4.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        Span<byte> lengthField = glb.AsSpan(GlbTestData.FirstChunkLengthOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(lengthField, BinaryPrimitives.ReadUInt32LittleEndian(lengthField) - 1);

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.ChunkUnaligned);
    }

    [Test]
    public async Task Parse_ShouldReturnDeclaredLengthExceedsDataError_WhenHeaderLengthExceedsActual()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(
            glb.AsSpan(GlbTestData.DeclaredLengthOffset), (uint)(glb.Length + GlbTestData.ChunkAlignment));

        // Act
        Result<GlbDocument> result = GlbDocument.Parse(glb);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.DeclaredLengthExceedsData);
    }
}
