using TheOmenDen.VRMParser.Results;

namespace TheOmenDen.VRMParser.Glb;

/// <summary>
/// The rich fault produced when binary glTF (<c>.glb</c> / <c>.vrm</c>) data does not conform to the
/// GLB container format — bad magic, an unsupported version, misaligned or truncated chunks, or a
/// missing JSON chunk.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GlbDocument.Parse"/> / <see cref="GlbDocument.ParseAsync"/> do not throw this on
/// malformed input; they return a failed <c>DotNext.Result&lt;GlbDocument&gt;</c> whose
/// <see cref="System.Exception"/> is an instance of this type. The instance is <em>constructed, not
/// thrown</em>, so inspecting <see cref="Code"/>, <see cref="Category"/>, and <see cref="Metadata"/>
/// never captures a stack trace. Reading the result's <c>Value</c> is what (re)throws it, for callers
/// who prefer exceptions.
/// </para>
/// <para>Build instances through the static factories — they populate the code, category, message, and
/// structured metadata together so faults are reported consistently.</para>
/// </remarks>
public sealed class GlbFormatException : FormatException, IVrmError
{
    /// <summary>Metadata key for a byte offset within the container.</summary>
    public const string OffsetKey = "offset";

    /// <summary>Metadata key for a zero-based chunk index.</summary>
    public const string ChunkIndexKey = "chunkIndex";

    /// <summary>Metadata key for the expected value of a checked field.</summary>
    public const string ExpectedKey = "expected";

    /// <summary>Metadata key for the actual value of a checked field.</summary>
    public const string ActualKey = "actual";

    /// <summary>Metadata key for the number of bytes remaining at the point of failure.</summary>
    public const string RemainingKey = "remaining";

    /// <summary>Initializes a new instance with no diagnostic code (the <see cref="GlbErrorCode.None"/> sentinel).</summary>
    public GlbFormatException()
        : this(GlbErrorCode.None, VrmErrorCategory.Format, "The GLB container is malformed.", innerException: null, metadata: null)
    {
    }

    /// <summary>Initializes a new instance with a message and no diagnostic code.</summary>
    /// <param name="message">A description of the malformation.</param>
    public GlbFormatException(string message)
        : this(GlbErrorCode.None, VrmErrorCategory.Format, message, innerException: null, metadata: null)
    {
    }

    /// <summary>Initializes a new instance with a message and an underlying cause.</summary>
    /// <param name="message">A description of the malformation.</param>
    /// <param name="innerException">The underlying cause.</param>
    public GlbFormatException(string message, Exception innerException)
        : this(GlbErrorCode.None, VrmErrorCategory.Format, message, innerException, metadata: null)
    {
    }

    private GlbFormatException(
        GlbErrorCode code,
        VrmErrorCategory category,
        string message,
        Exception? innerException,
        Dictionary<string, object?>? metadata)
        : base(message, innerException)
    {
        Code = code;
        Category = category;

        Dictionary<string, object?> meta = metadata ?? [];
        meta["code"] = ToStableCode(code);
        meta["category"] = category.ToString();
        Metadata = meta;
    }

    /// <summary>The status code classifying this fault.</summary>
    public GlbErrorCode Code { get; }

    /// <inheritdoc />
    public VrmErrorCategory Category { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    /// <inheritdoc />
    string IVrmError.Code => ToStableCode(Code);

    /// <summary>The data is shorter than the 12-byte GLB header (synchronous path, length known).</summary>
    public static GlbFormatException TooShort(int actualLength, int required) =>
        new(GlbErrorCode.TooShort, VrmErrorCategory.Truncated,
            $"GLB data is too short: {actualLength} bytes, need at least {required} for the header.",
            innerException: null,
            new() { [ActualKey] = actualLength, [ExpectedKey] = required });

    /// <summary>The stream ended before the 12-byte header could be read (asynchronous path).</summary>
    public static GlbFormatException IncompleteHeader(int required, Exception cause) =>
        new(GlbErrorCode.TooShort, VrmErrorCategory.Truncated,
            $"GLB data is too short: need at least {required} bytes for the header.",
            cause,
            new() { [ExpectedKey] = required });

    /// <summary>The leading magic value is not <c>"glTF"</c>.</summary>
    public static GlbFormatException BadMagic(uint magic) =>
        new(GlbErrorCode.BadMagic, VrmErrorCategory.Format,
            $"Not a GLB container: magic 0x{magic:X8} does not match 0x{GlbDocument.Magic:X8} (\"glTF\").",
            innerException: null,
            new() { [ActualKey] = magic, [ExpectedKey] = GlbDocument.Magic });

    /// <summary>The container version is recognized but unsupported.</summary>
    public static GlbFormatException UnsupportedVersion(uint version) =>
        new(GlbErrorCode.UnsupportedVersion, VrmErrorCategory.Unsupported,
            $"Unsupported GLB version {version}; only version {GlbDocument.SupportedVersion} is supported.",
            innerException: null,
            new() { [ActualKey] = version, [ExpectedKey] = GlbDocument.SupportedVersion });

    /// <summary>The header's declared length is smaller than the header itself.</summary>
    public static GlbFormatException DeclaredLengthTooSmall(uint declaredLength, int headerSize) =>
        new(GlbErrorCode.DeclaredLengthTooSmall, VrmErrorCategory.Format,
            $"GLB declared length {declaredLength} is smaller than the {headerSize}-byte header.",
            innerException: null,
            new() { [ActualKey] = declaredLength, [ExpectedKey] = headerSize });

    /// <summary>The header declares more bytes than the input actually contains.</summary>
    public static GlbFormatException DeclaredLengthExceedsData(uint declaredLength, int actualLength) =>
        new(GlbErrorCode.DeclaredLengthExceedsData, VrmErrorCategory.Truncated,
            $"GLB is truncated: the header declares {declaredLength} bytes but only {actualLength} are present.",
            innerException: null,
            new() { [ExpectedKey] = declaredLength, [ActualKey] = actualLength });

    /// <summary>Not enough bytes remain within the declared length for a chunk header (synchronous path).</summary>
    public static GlbFormatException ChunkHeaderTruncated(long offset, long remaining, int required) =>
        new(GlbErrorCode.ChunkHeaderTruncated, VrmErrorCategory.Truncated,
            $"GLB is truncated: the chunk header at offset {offset} needs {required} bytes, {remaining} remain.",
            innerException: null,
            new() { [OffsetKey] = offset, [RemainingKey] = remaining, [ExpectedKey] = required });

    /// <summary>The stream ended before a chunk header could be read (asynchronous path).</summary>
    public static GlbFormatException IncompleteChunkHeader(long offset, Exception cause) =>
        new(GlbErrorCode.ChunkHeaderTruncated, VrmErrorCategory.Truncated,
            $"GLB is truncated: the chunk header at offset {offset} could not be read.",
            cause,
            new() { [OffsetKey] = offset });

    /// <summary>A chunk length is not a multiple of four bytes.</summary>
    public static GlbFormatException ChunkUnaligned(int chunkIndex, uint chunkLength) =>
        new(GlbErrorCode.ChunkUnaligned, VrmErrorCategory.Format,
            $"GLB chunk {chunkIndex} has length {chunkLength}, which is not 4-byte aligned.",
            innerException: null,
            new() { [ChunkIndexKey] = chunkIndex, [ActualKey] = chunkLength });

    /// <summary>A chunk declares more bytes than remain in the container.</summary>
    public static GlbFormatException ChunkOverrun(int chunkIndex, uint chunkLength, long remaining) =>
        new(GlbErrorCode.ChunkOverrun, VrmErrorCategory.Format,
            $"GLB chunk {chunkIndex} declares {chunkLength} bytes but only {remaining} remain.",
            innerException: null,
            new() { [ChunkIndexKey] = chunkIndex, [ActualKey] = chunkLength, [RemainingKey] = remaining });

    /// <summary>The stream ended before a chunk's declared payload could be read (asynchronous path).</summary>
    public static GlbFormatException ChunkPayloadTruncated(int chunkIndex, uint chunkLength, Exception cause) =>
        new(GlbErrorCode.ChunkPayloadTruncated, VrmErrorCategory.Truncated,
            $"GLB is truncated: chunk {chunkIndex} declares {chunkLength} bytes but the stream ended early.",
            cause,
            new() { [ChunkIndexKey] = chunkIndex, [ActualKey] = chunkLength });

    /// <summary>The first chunk is not the required glTF JSON chunk.</summary>
    public static GlbFormatException FirstChunkNotJson(uint actualType) =>
        new(GlbErrorCode.FirstChunkNotJson, VrmErrorCategory.Format,
            $"The first GLB chunk must be JSON (0x{GlbDocument.JsonChunkType:X8}) but was 0x{actualType:X8}.",
            innerException: null,
            new() { [ActualKey] = actualType, [ExpectedKey] = GlbDocument.JsonChunkType });

    /// <summary>The container has no JSON chunk.</summary>
    public static GlbFormatException MissingJsonChunk() =>
        new(GlbErrorCode.MissingJsonChunk, VrmErrorCategory.Validation,
            "GLB contains no JSON chunk.",
            innerException: null,
            metadata: null);

    private static string ToStableCode(GlbErrorCode code) => code switch
    {
        GlbErrorCode.TooShort => "glb.too_short",
        GlbErrorCode.BadMagic => "glb.bad_magic",
        GlbErrorCode.UnsupportedVersion => "glb.unsupported_version",
        GlbErrorCode.DeclaredLengthTooSmall => "glb.declared_length_too_small",
        GlbErrorCode.DeclaredLengthExceedsData => "glb.declared_length_exceeds_data",
        GlbErrorCode.ChunkHeaderTruncated => "glb.chunk_header_truncated",
        GlbErrorCode.ChunkUnaligned => "glb.chunk_unaligned",
        GlbErrorCode.ChunkOverrun => "glb.chunk_overrun",
        GlbErrorCode.ChunkPayloadTruncated => "glb.chunk_payload_truncated",
        GlbErrorCode.FirstChunkNotJson => "glb.first_chunk_not_json",
        GlbErrorCode.MissingJsonChunk => "glb.missing_json_chunk",
        _ => "glb.none",
    };
}
