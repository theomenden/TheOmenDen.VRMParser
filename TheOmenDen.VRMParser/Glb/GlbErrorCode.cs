namespace TheOmenDen.VRMParser.Glb;

/// <summary>
/// The machine-readable status codes for every way GLB (<c>.glb</c> / <c>.vrm</c>) parsing can fail.
/// Carried by <see cref="GlbFormatException"/> and surfaced through the <c>DotNext.Result&lt;T&gt;</c>
/// returned by <see cref="GlbDocument.Parse"/> / <see cref="GlbDocument.ParseAsync"/>.
/// </summary>
public enum GlbErrorCode
{
    /// <summary>No error — the null-object sentinel for a successful parse. See <see cref="GlbResultExtensions.ErrorCode"/>.</summary>
    None = 0,

    /// <summary>The data is shorter than the 12-byte GLB header.</summary>
    TooShort,

    /// <summary>The leading magic value is not <c>"glTF"</c>.</summary>
    BadMagic,

    /// <summary>The container version is recognized but unsupported.</summary>
    UnsupportedVersion,

    /// <summary>The header's declared length is smaller than the 12-byte header itself.</summary>
    DeclaredLengthTooSmall,

    /// <summary>The header declares more bytes than the input actually contains.</summary>
    DeclaredLengthExceedsData,

    /// <summary>The input ended before a full 8-byte chunk header could be read.</summary>
    ChunkHeaderTruncated,

    /// <summary>A chunk length is not a multiple of four bytes.</summary>
    ChunkUnaligned,

    /// <summary>A chunk declares more bytes than remain in the container.</summary>
    ChunkOverrun,

    /// <summary>The input ended before a chunk's declared payload could be read.</summary>
    ChunkPayloadTruncated,

    /// <summary>The first chunk is not the required glTF JSON chunk.</summary>
    FirstChunkNotJson,

    /// <summary>The container has no JSON chunk.</summary>
    MissingJsonChunk,
}
