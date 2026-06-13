using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json;
using TheOmenDen.VRMParser.Models.Records;

namespace TheOmenDen.VRMParser.Glb;

/// <summary>
/// A parsed binary glTF (GLB) container — the on-disk shape of a <c>.glb</c> or <c>.vrm</c> file.
/// </summary>
/// <remarks>
/// <para>
/// A GLB file is a 12-byte header (<c>magic</c>, <c>version</c>, total <c>length</c>) followed by
/// 4-byte-aligned chunks. The first chunk is always the glTF JSON; an optional second chunk holds
/// the binary buffer (<c>BIN</c>). See the
/// <see href="https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#binary-gltf-layout">glTF 2.0
/// binary layout</see>.
/// </para>
/// <para>
/// This type keeps the JSON and binary chunk payloads verbatim so a parse → write cycle is
/// byte-stable for spec-compliant input, and any unmodelled glTF extensions or <c>extras</c>
/// survive a round-trip. Unknown or duplicate chunks are ignored per the spec.
/// </para>
/// </remarks>
public sealed class GlbDocument
{
    /// <summary>The GLB magic value, the little-endian <c>uint</c> for the ASCII <c>"glTF"</c>.</summary>
    public const uint Magic = 0x46546C67;

    /// <summary>The chunk type for the glTF JSON chunk (ASCII <c>"JSON"</c>).</summary>
    public const uint JsonChunkType = 0x4E4F534A;

    /// <summary>The chunk type for the binary buffer chunk (ASCII <c>"BIN\0"</c>).</summary>
    public const uint BinaryChunkType = 0x004E4942;

    /// <summary>The only GLB container version this library reads or writes.</summary>
    public const uint SupportedVersion = 2;

    private const int HeaderSize = 12;
    private const int ChunkHeaderSize = 8;

    /// <summary>Initializes a new <see cref="GlbDocument"/> from chunk payloads.</summary>
    /// <param name="json">The glTF JSON chunk payload (UTF-8). Trailing padding is optional; it is added on write.</param>
    /// <param name="binary">The binary buffer chunk payload, or <see langword="null"/> when the container has no <c>BIN</c> chunk.</param>
    /// <param name="version">The GLB container version. Defaults to <see cref="SupportedVersion"/>.</param>
    public GlbDocument(ReadOnlyMemory<byte> json, ReadOnlyMemory<byte>? binary = null, uint version = SupportedVersion)
    {
        Json = json;
        Binary = binary;
        Version = version;
    }

    /// <summary>Gets the GLB container version (always <see cref="SupportedVersion"/> for parsed documents).</summary>
    public uint Version { get; }

    /// <summary>
    /// Gets the glTF JSON chunk payload as UTF-8 bytes. When read from a 4-byte-aligned container this
    /// may include trailing <c>0x20</c> (space) padding, which is insignificant to a JSON parser.
    /// </summary>
    public ReadOnlyMemory<byte> Json { get; }

    /// <summary>
    /// Gets the binary buffer (<c>BIN</c>) chunk payload, or <see langword="null"/> when the container
    /// has no binary chunk. May include trailing <c>0x00</c> padding when read from an aligned container.
    /// </summary>
    public ReadOnlyMemory<byte>? Binary { get; }

    /// <summary>Gets a value indicating whether this container has a binary (<c>BIN</c>) chunk.</summary>
    [MemberNotNullWhen(true, nameof(Binary))]
    public bool HasBinary => Binary is not null;

    /// <summary>Parses a GLB (<c>.glb</c> / <c>.vrm</c>) container from its bytes.</summary>
    /// <param name="data">The complete GLB file contents.</param>
    /// <returns>The parsed <see cref="GlbDocument"/>.</returns>
    /// <exception cref="GlbFormatException">The data is not a valid GLB container.</exception>
    public static GlbDocument Parse(ReadOnlyMemory<byte> data)
    {
        ReadOnlySpan<byte> span = data.Span;

        if (span.Length < HeaderSize)
        {
            throw new GlbFormatException(
                $"GLB data is too short: {span.Length} bytes, need at least {HeaderSize} for the header.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(span);
        if (magic != Magic)
        {
            throw new GlbFormatException(
                $"Not a GLB container: magic 0x{magic:X8} does not match 0x{Magic:X8} (\"glTF\").");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(span[4..]);
        if (version != SupportedVersion)
        {
            throw new GlbFormatException(
                $"Unsupported GLB version {version}; only version {SupportedVersion} is supported.");
        }

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(span[8..]);
        if (declaredLength < HeaderSize)
        {
            throw new GlbFormatException(
                $"GLB declared length {declaredLength} is smaller than the {HeaderSize}-byte header.");
        }

        if (declaredLength > (uint)span.Length)
        {
            throw new GlbFormatException(
                $"GLB is truncated: the header declares {declaredLength} bytes but only {span.Length} are present.");
        }

        ReadOnlyMemory<byte> json = default;
        bool jsonSeen = false;
        ReadOnlyMemory<byte>? binary = null;

        int offset = HeaderSize;
        int end = (int)declaredLength;
        int chunkIndex = 0;

        while (offset < end)
        {
            if (end - offset < ChunkHeaderSize)
            {
                throw new GlbFormatException(
                    $"GLB is truncated: the chunk header at offset {offset} needs {ChunkHeaderSize} bytes, {end - offset} remain.");
            }

            uint chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
            uint chunkType = BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 4)..]);
            offset += ChunkHeaderSize;

            if (chunkLength % 4 != 0)
            {
                throw new GlbFormatException(
                    $"GLB chunk {chunkIndex} has length {chunkLength}, which is not 4-byte aligned.");
            }

            if (chunkLength > (uint)(end - offset))
            {
                throw new GlbFormatException(
                    $"GLB chunk {chunkIndex} declares {chunkLength} bytes but only {end - offset} remain.");
            }

            if (chunkIndex == 0 && chunkType != JsonChunkType)
            {
                throw new GlbFormatException(
                    $"The first GLB chunk must be JSON (0x{JsonChunkType:X8}) but was 0x{chunkType:X8}.");
            }

            ReadOnlyMemory<byte> payload = data.Slice(offset, (int)chunkLength);

            switch (chunkType)
            {
                case JsonChunkType when !jsonSeen:
                    json = payload;
                    jsonSeen = true;
                    break;
                case BinaryChunkType when binary is null:
                    binary = payload;
                    break;
                default:
                    // Unknown or duplicate chunk — ignored per the glTF 2.0 spec.
                    break;
            }

            offset += (int)chunkLength;
            chunkIndex++;
        }

        if (!jsonSeen)
        {
            throw new GlbFormatException("GLB contains no JSON chunk.");
        }

        return new GlbDocument(json, binary, version);
    }

    /// <summary>Attempts to parse a GLB container, returning <see langword="false"/> instead of throwing on malformed data.</summary>
    /// <param name="data">The complete GLB file contents.</param>
    /// <param name="document">On success, the parsed document; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="data"/> is a valid GLB container.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> data, [NotNullWhen(true)] out GlbDocument? document)
    {
        try
        {
            document = Parse(data);
            return true;
        }
        catch (GlbFormatException)
        {
            document = null;
            return false;
        }
    }

    /// <summary>Serializes this container to a new GLB byte array, padding each chunk to a 4-byte boundary.</summary>
    /// <returns>The complete GLB file contents.</returns>
    public byte[] ToBytes()
    {
        int jsonChunk = Align4(Json.Length);
        int total = HeaderSize + ChunkHeaderSize + jsonChunk;

        int binaryChunk = 0;
        if (Binary is { } bin)
        {
            binaryChunk = Align4(bin.Length);
            total += ChunkHeaderSize + binaryChunk;
        }

        byte[] buffer = new byte[total];
        WriteTo(buffer, jsonChunk, binaryChunk, total);
        return buffer;
    }

    /// <summary>Serializes this container to the given stream as GLB.</summary>
    /// <param name="destination">The stream to write to.</param>
    public void WriteTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(ToBytes());
    }

    /// <summary>
    /// Parses the JSON chunk into the strongly-typed glTF model. The returned document owns pooled
    /// memory and must be disposed; read <see cref="ParsedJsonDocument{T}.RootElement"/> for the model.
    /// </summary>
    /// <remarks>Internal until the generated glTF model is part of the public API surface.</remarks>
    internal ParsedJsonDocument<GltfRoot> ParseGltf() => ParsedJsonDocument<GltfRoot>.Parse(Json);

    private static int Align4(int length) => (length + 3) & ~3;

    private void WriteTo(Span<byte> span, int jsonChunk, int binaryChunk, int total)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], Version);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)total);

        int offset = HeaderSize;

        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)jsonChunk);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(offset + 4)..], JsonChunkType);
        offset += ChunkHeaderSize;
        Json.Span.CopyTo(span[offset..]);
        // JSON chunks pad with spaces (0x20); the binary array is already zero-filled.
        span[(offset + Json.Length)..(offset + jsonChunk)].Fill((byte)' ');
        offset += jsonChunk;

        if (Binary is { } bin)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)binaryChunk);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(offset + 4)..], BinaryChunkType);
            offset += ChunkHeaderSize;
            bin.Span.CopyTo(span[offset..]);
            // BIN padding is 0x00 — already present from the zeroed buffer.
        }
    }
}
