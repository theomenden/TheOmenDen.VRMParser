using TheOmenDen.VRMParser.Glb;
using TheOmenDen.VRMParser.Results;
using static VerifyTUnit.Verifier;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>Pins the logging contract every <see cref="GlbFormatException"/> factory exposes via <see cref="IVrmError"/>.</summary>
public sealed class GlbFormatExceptionTests
{
    // Stable codes, categories and metadata keys are what consumers log and alert on — a snapshot
    // makes any change to them a reviewed diff. Messages are excluded: they're prose, not contract.
    [Test]
    public Task Factories_ShouldExposeStableErrorContract_WhenEachFaultIsCreated()
    {
        // Arrange
        var cause = new EndOfStreamException();
        const int ChunkIndex = 1;
        uint chunkLength = GlbTestData.OversizedChunkLength;

        IVrmError[] errors =
        [
            GlbFormatException.TooShort(GlbTestData.HeaderSize - 1, GlbTestData.HeaderSize),
            GlbFormatException.IncompleteHeader(GlbTestData.HeaderSize, cause),
            GlbFormatException.BadMagic(~GlbDocument.Magic),
            GlbFormatException.UnsupportedVersion(GlbDocument.SupportedVersion + 1),
            GlbFormatException.DeclaredLengthTooSmall(GlbTestData.HeaderSize - 1, GlbTestData.HeaderSize),
            GlbFormatException.DeclaredLengthExceedsData(GlbTestData.MaxDeclaredLength, GlbTestData.HeaderSize),
            GlbFormatException.ChunkHeaderTruncated(GlbTestData.HeaderSize, GlbTestData.ChunkAlignment, GlbTestData.ChunkHeaderSize),
            GlbFormatException.IncompleteChunkHeader(GlbTestData.HeaderSize, cause),
            GlbFormatException.ChunkUnaligned(ChunkIndex, chunkLength - 1),
            GlbFormatException.ChunkOverrun(ChunkIndex, chunkLength, GlbTestData.ChunkAlignment),
            GlbFormatException.ChunkPayloadTruncated(ChunkIndex, chunkLength, cause),
            GlbFormatException.FirstChunkNotJson(GlbDocument.BinaryChunkType),
            GlbFormatException.MissingJsonChunk(),
            GlbFormatException.ChunkTooLarge(ChunkIndex, chunkLength),
        ];

        // Act
        var contract = errors.Select(e => new { e.Code, e.Category, e.Metadata });

        // Assert
        return Verify(contract);
    }
}
