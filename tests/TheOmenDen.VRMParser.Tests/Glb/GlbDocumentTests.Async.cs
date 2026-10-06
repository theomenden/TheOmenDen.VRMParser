using System.Buffers.Binary;
using DotNext;
using TUnit;
using TUnit.Assertions;
using TUnit.Assertions.Should;
using TUnit.Assertions.Enums;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>
/// Covers the DotNext.IO-backed streaming <see cref="GlbDocument.ParseAsync"/> /
/// <see cref="GlbDocument.WriteToAsync"/> paths and their parity with the synchronous span path.
/// </summary>
public sealed partial class GlbDocumentTests
{
    // ParseAsync must produce exactly what the synchronous Parse does for real container input.
    [Test]
    [Arguments(Fixtures.Box)]
    [Arguments(Fixtures.MinimalVrm0)]
    [Arguments(Fixtures.MinimalVrm1)]
    public async Task ParseAsync_ShouldMatchSyncParse_WhenReadingFixture(string name)
    {
        // Arrange
        byte[] original = await File.ReadAllBytesAsync(Fixtures.PathOf(name));
        GlbDocument sync = GlbDocument.Parse(original).Value;

        // Act
        await using var stream = new MemoryStream(original, writable: false);
        GlbDocument streamed = (await GlbDocument.ParseAsync(stream)).Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(streamed.Version).IsEqualTo(sync.Version);
            await Assert.That(streamed.Json.ToArray()).IsEquivalentTo(sync.Json.ToArray(), CollectionOrdering.Matching);
            await Assert.That(streamed.HasBinary).IsEqualTo(sync.HasBinary);
            if (sync.HasBinary)
            {
                await Assert.That(streamed.Binary.Value.ToArray()).IsEquivalentTo(sync.Binary.Value.ToArray(), CollectionOrdering.Matching);
            }
        }
    }

    // Streaming parse -> streaming write must round-trip every fixture byte-for-byte.
    [Test]
    [Arguments(Fixtures.Box)]
    [Arguments(Fixtures.MinimalVrm0)]
    [Arguments(Fixtures.MinimalVrm1)]
    public async Task ParseAsyncThenWriteToAsync_ShouldRoundTripByteForByte_WhenReadingFixture(string name)
    {
        // Arrange
        byte[] original = await File.ReadAllBytesAsync(Fixtures.PathOf(name));

        // Act
        await using var source = new MemoryStream(original, writable: false);
        GlbDocument document = (await GlbDocument.ParseAsync(source)).Value;

        await using var destination = new MemoryStream();
        await document.WriteToAsync(destination);

        // Assert
        await Assert.That(destination.ToArray()).IsEquivalentTo(original, CollectionOrdering.Matching);
    }

    // WriteToAsync must emit the same bytes as the synchronous ToBytes() for arbitrary payloads,
    // including the on/around 4-byte boundary padding cases.
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(7)]
    [Arguments(255)]
    [Arguments(1024)]
    public async Task WriteToAsync_ShouldMatchSyncToBytes_WhenPayloadLengthVaries(int length)
    {
        // Arrange
        byte[] binary = GlbTestData.Payload(length);
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Act
        await using var destination = new MemoryStream();
        await document.WriteToAsync(destination);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(destination.ToArray()).IsEquivalentTo(document.ToBytes(), CollectionOrdering.Matching);
            await Assert.That(destination.ToArray()).IsEquivalentTo(glb, CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task ParseAsync_ShouldHaveNoBinary_WhenContainerIsJsonOnly()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);

        // Act
        await using var stream = new MemoryStream(glb, writable: false);
        GlbDocument document = (await GlbDocument.ParseAsync(stream)).Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.Version).IsEqualTo(GlbDocument.SupportedVersion);
            await Assert.That(document.HasBinary).IsFalse();
            await Assert.That(document.Binary.HasValue).IsFalse();
            await Assert.That(document.Json.IsEmpty).IsFalse();
        }
    }

    [Test]
    public async Task ParseAsync_ShouldReturnTooShortError_WhenStreamShorterThanHeader()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson)[..(GlbTestData.HeaderSize - 1)];
        await using var stream = new MemoryStream(glb, writable: false);

        // Act
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.TooShort);
    }

    [Test]
    public async Task ParseAsync_ShouldReturnBadMagicError_WhenMagicIsWrong()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb, ~GlbDocument.Magic);

        // Act
        await using var stream = new MemoryStream(glb, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.BadMagic);
    }

    [Test]
    public async Task ParseAsync_ShouldReturnUnsupportedVersionError_WhenVersionIsNotSupported()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, version: GlbDocument.SupportedVersion - 1);

        // Act
        await using var stream = new MemoryStream(glb, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.UnsupportedVersion);
    }

    [Test]
    public async Task ParseAsync_ShouldReturnChunkPayloadTruncatedError_WhenStreamEndsMidPayload()
    {
        // Arrange — drop the trailing BIN payload bytes while leaving the header's declared length intact.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, GlbTestData.Payload(GlbTestData.ChunkAlignment));
        byte[] truncated = glb[..^GlbTestData.ChunkAlignment];

        // Act
        await using var stream = new MemoryStream(truncated, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.ChunkPayloadTruncated);
    }

    [Test]
    public async Task ParseAsync_ShouldReturnChunkUnalignedError_WhenChunkLengthNotFourByteAligned()
    {
        // Arrange — 4-aligned JSON chunk length minus 1 => not a multiple of 4.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        Span<byte> lengthField = glb.AsSpan(GlbTestData.FirstChunkLengthOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(lengthField, BinaryPrimitives.ReadUInt32LittleEndian(lengthField) - 1);

        // Act
        await using var stream = new MemoryStream(glb, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        await AssertFailsWith(result, GlbErrorCode.ChunkUnaligned);
    }
}
