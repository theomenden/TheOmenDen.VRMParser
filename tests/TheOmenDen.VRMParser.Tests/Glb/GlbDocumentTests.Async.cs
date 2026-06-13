using System.Buffers.Binary;
using Bogus;
using DotNext;
using Shouldly;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>
/// Covers the DotNext.IO-backed streaming <see cref="GlbDocument.ParseAsync"/> /
/// <see cref="GlbDocument.WriteToAsync"/> paths and their parity with the synchronous span path.
/// </summary>
public sealed partial class GlbDocumentTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // ParseAsync must produce exactly what the synchronous Parse does for real container input.
    [Test]
    [Arguments("Box.glb")]
    [Arguments("MinimalVrm0.vrm")]
    [Arguments("MinimalVrm1.vrm")]
    public async Task ParseAsync_ShouldMatchSyncParse_WhenReadingFixture(string name)
    {
        // Arrange
        byte[] original = await File.ReadAllBytesAsync(FixturePath(name));
        GlbDocument sync = GlbDocument.Parse(original).Value;

        // Act
        await using var stream = new MemoryStream(original, writable: false);
        GlbDocument streamed = (await GlbDocument.ParseAsync(stream)).Value;

        // Assert
        streamed.ShouldSatisfyAllConditions(
            () => streamed.Version.ShouldBe(sync.Version),
            () => streamed.Json.ToArray().ShouldBe(sync.Json.ToArray()),
            () => streamed.HasBinary.ShouldBe(sync.HasBinary),
            () =>
            {
                if (sync.HasBinary)
                {
                    streamed.Binary.Value.ToArray().ShouldBe(sync.Binary.Value.ToArray());
                }
            });
    }

    // Streaming parse -> streaming write must round-trip every fixture byte-for-byte.
    [Test]
    [Arguments("Box.glb")]
    [Arguments("MinimalVrm0.vrm")]
    [Arguments("MinimalVrm1.vrm")]
    public async Task ParseAsyncThenWriteToAsync_ShouldRoundTripByteForByte_WhenReadingFixture(string name)
    {
        // Arrange
        byte[] original = await File.ReadAllBytesAsync(FixturePath(name));

        // Act
        await using var source = new MemoryStream(original, writable: false);
        GlbDocument document = (await GlbDocument.ParseAsync(source)).Value;

        await using var destination = new MemoryStream();
        await document.WriteToAsync(destination);

        // Assert
        destination.ToArray().ShouldBe(original);
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
        byte[] binary = new Faker { Random = new Randomizer(length + 1) }.Random.Bytes(length);
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, binary);
        GlbDocument document = GlbDocument.Parse(glb).Value;

        // Act
        await using var destination = new MemoryStream();
        await document.WriteToAsync(destination);

        // Assert
        destination.ShouldSatisfyAllConditions(
            () => destination.ToArray().ShouldBe(document.ToBytes()),
            () => destination.ToArray().ShouldBe(glb));
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
        document.ShouldSatisfyAllConditions(
            () => document.Version.ShouldBe(GlbDocument.SupportedVersion),
            () => document.HasBinary.ShouldBeFalse(),
            () => document.Binary.HasValue.ShouldBeFalse(),
            () => document.Json.IsEmpty.ShouldBeFalse());
    }

    [Test]
    public async Task ParseAsync_ShouldReturnTooShortError_WhenStreamShorterThanHeader()
    {
        // Arrange
        await using var stream = new MemoryStream([0x67, 0x6C, 0x54]); // 3 bytes

        // Act
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.TooShort));
    }

    [Test]
    public async Task ParseAsync_ShouldReturnBadMagicError_WhenMagicIsWrong()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb, 0xDEADBEEF);

        // Act
        await using var stream = new MemoryStream(glb, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.BadMagic));
    }

    [Test]
    public async Task ParseAsync_ShouldReturnUnsupportedVersionError_WhenVersionIsNotTwo()
    {
        // Arrange
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 1);

        // Act
        await using var stream = new MemoryStream(glb, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.UnsupportedVersion));
    }

    [Test]
    public async Task ParseAsync_ShouldReturnChunkPayloadTruncatedError_WhenStreamEndsMidPayload()
    {
        // Arrange — drop the trailing BIN payload bytes while leaving the header's declared length intact.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson, [1, 2, 3, 4]);
        byte[] truncated = glb[..^4];

        // Act
        await using var stream = new MemoryStream(truncated, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.ChunkPayloadTruncated));
    }

    [Test]
    public async Task ParseAsync_ShouldReturnChunkUnalignedError_WhenChunkLengthNotFourByteAligned()
    {
        // Arrange — corrupt the JSON chunk length (at offset 12) to a value that is not 4-byte aligned.
        byte[] glb = GlbTestData.Build(GlbTestData.MinimalGltfJson);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), 7);

        // Act
        await using var stream = new MemoryStream(glb, writable: false);
        Result<GlbDocument> result = await GlbDocument.ParseAsync(stream);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccessful.ShouldBeFalse(),
            () => result.ErrorCode().ShouldBe(GlbErrorCode.ChunkUnaligned));
    }
}
