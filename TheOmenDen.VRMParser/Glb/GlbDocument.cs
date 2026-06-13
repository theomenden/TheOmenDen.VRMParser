using System.Buffers;
using System.Buffers.Binary;
using Corvus.Text.Json;
using DotNext;
using DotNext.IO;
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
    /// <param name="binary">The binary buffer chunk payload, or an empty <see cref="Optional{T}"/> (the default) when the container has no <c>BIN</c> chunk. A present-but-empty payload is distinct from absence and is preserved on write.</param>
    /// <param name="version">The GLB container version. Defaults to <see cref="SupportedVersion"/>.</param>
    public GlbDocument(ReadOnlyMemory<byte> json, Optional<ReadOnlyMemory<byte>> binary = default, uint version = SupportedVersion)
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
    /// Gets the binary buffer (<c>BIN</c>) chunk payload, or an empty <see cref="Optional{T}"/> when the
    /// container has no binary chunk. A present chunk and an absent one are distinct even when the
    /// payload is empty, which keeps a parse → write cycle byte-stable. May include trailing <c>0x00</c>
    /// padding when read from an aligned container.
    /// </summary>
    public Optional<ReadOnlyMemory<byte>> Binary { get; }

    /// <summary>Gets a value indicating whether this container has a binary (<c>BIN</c>) chunk.</summary>
    public bool HasBinary => Binary.HasValue;

    /// <summary>Parses a GLB (<c>.glb</c> / <c>.vrm</c>) container from its bytes.</summary>
    /// <param name="data">The complete GLB file contents.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping the parsed <see cref="GlbDocument"/>, or a failed
    /// result whose error is a <see cref="GlbFormatException"/> describing the malformation. Nothing is
    /// thrown for malformed data; read <see cref="Result{T}.Value"/> to (re)throw, or inspect
    /// <see cref="GlbResultExtensions.ErrorCode"/> to branch on the <see cref="GlbErrorCode"/>.
    /// </returns>
    public static Result<GlbDocument> Parse(ReadOnlyMemory<byte> data)
    {
        ReadOnlySpan<byte> span = data.Span;

        if (span.Length < HeaderSize)
        {
            return new(GlbFormatException.TooShort(span.Length, HeaderSize));
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(span);
        if (magic != Magic)
        {
            return new(GlbFormatException.BadMagic(magic));
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(span[4..]);
        if (version != SupportedVersion)
        {
            return new(GlbFormatException.UnsupportedVersion(version));
        }

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(span[8..]);
        if (declaredLength < HeaderSize)
        {
            return new(GlbFormatException.DeclaredLengthTooSmall(declaredLength, HeaderSize));
        }

        if (declaredLength > (uint)span.Length)
        {
            return new(GlbFormatException.DeclaredLengthExceedsData(declaredLength, span.Length));
        }

        ReadOnlyMemory<byte> json = default;
        bool jsonSeen = false;
        Optional<ReadOnlyMemory<byte>> binary = default;

        int offset = HeaderSize;
        int end = (int)declaredLength;
        int chunkIndex = 0;

        while (offset < end)
        {
            if (end - offset < ChunkHeaderSize)
            {
                return new(GlbFormatException.ChunkHeaderTruncated(offset, end - offset, ChunkHeaderSize));
            }

            uint chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
            uint chunkType = BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 4)..]);
            offset += ChunkHeaderSize;

            if (chunkLength % 4 != 0)
            {
                return new(GlbFormatException.ChunkUnaligned(chunkIndex, chunkLength));
            }

            if (chunkLength > (uint)(end - offset))
            {
                return new(GlbFormatException.ChunkOverrun(chunkIndex, chunkLength, end - offset));
            }

            if (chunkIndex == 0 && chunkType != JsonChunkType)
            {
                return new(GlbFormatException.FirstChunkNotJson(chunkType));
            }

            ReadOnlyMemory<byte> payload = data.Slice(offset, (int)chunkLength);

            switch (chunkType)
            {
                case JsonChunkType when !jsonSeen:
                    json = payload;
                    jsonSeen = true;
                    break;
                case BinaryChunkType when !binary.HasValue:
                    binary = (ReadOnlyMemory<byte>)payload;
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
            return new(GlbFormatException.MissingJsonChunk());
        }

        return new GlbDocument(json, binary, version);
    }

    /// <summary>
    /// Asynchronously parses a GLB (<c>.glb</c> / <c>.vrm</c>) container by streaming it from
    /// <paramref name="source"/>, reading only as far as the header's declared length.
    /// </summary>
    /// <param name="source">The stream positioned at the start of the GLB container.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping the parsed <see cref="GlbDocument"/>, or a failed
    /// result whose error is a <see cref="GlbFormatException"/> describing the malformation. Malformed
    /// data does not throw; only a <see langword="null"/> <paramref name="source"/> does.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="Parse(ReadOnlyMemory{byte})"/> this never buffers the whole file up front; it
    /// reads the 12-byte header, then each chunk header and payload in turn. Chunk payloads are copied
    /// into owned arrays, so the returned document follows the same (non-pooled) ownership model as the
    /// synchronous path.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static async ValueTask<Result<GlbDocument>> ParseAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // A small scratch buffer backs the reader's little-endian integer reads; chunk payloads are
        // read straight into their own arrays and never touch it.
        byte[] scratch = ArrayPool<byte>.Shared.Rent(ChunkHeaderSize);
        try
        {
            IAsyncBinaryReader reader = IAsyncBinaryReader.Create(source, scratch.AsMemory(0, ChunkHeaderSize));

            uint magic, version, declaredLength;
            try
            {
                magic = await reader.ReadLittleEndianAsync<uint>(cancellationToken).ConfigureAwait(false);
                version = await reader.ReadLittleEndianAsync<uint>(cancellationToken).ConfigureAwait(false);
                declaredLength = await reader.ReadLittleEndianAsync<uint>(cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException ex)
            {
                return new(GlbFormatException.IncompleteHeader(HeaderSize, ex));
            }

            if (magic != Magic)
            {
                return new(GlbFormatException.BadMagic(magic));
            }

            if (version != SupportedVersion)
            {
                return new(GlbFormatException.UnsupportedVersion(version));
            }

            if (declaredLength < HeaderSize)
            {
                return new(GlbFormatException.DeclaredLengthTooSmall(declaredLength, HeaderSize));
            }

            ReadOnlyMemory<byte> json = default;
            bool jsonSeen = false;
            Optional<ReadOnlyMemory<byte>> binary = default;

            long offset = HeaderSize;
            long end = declaredLength;
            int chunkIndex = 0;

            while (offset < end)
            {
                if (end - offset < ChunkHeaderSize)
                {
                    return new(GlbFormatException.ChunkHeaderTruncated(offset, end - offset, ChunkHeaderSize));
                }

                uint chunkLength, chunkType;
                try
                {
                    chunkLength = await reader.ReadLittleEndianAsync<uint>(cancellationToken).ConfigureAwait(false);
                    chunkType = await reader.ReadLittleEndianAsync<uint>(cancellationToken).ConfigureAwait(false);
                }
                catch (EndOfStreamException ex)
                {
                    return new(GlbFormatException.IncompleteChunkHeader(offset, ex));
                }

                offset += ChunkHeaderSize;

                if (chunkLength % 4 != 0)
                {
                    return new(GlbFormatException.ChunkUnaligned(chunkIndex, chunkLength));
                }

                if (chunkLength > (uint)(end - offset))
                {
                    return new(GlbFormatException.ChunkOverrun(chunkIndex, chunkLength, end - offset));
                }

                if (chunkIndex == 0 && chunkType != JsonChunkType)
                {
                    return new(GlbFormatException.FirstChunkNotJson(chunkType));
                }

                // Copy the payload into an owned array — the document holds non-owned memory.
                byte[] payload = new byte[(int)chunkLength];
                try
                {
                    await reader.ReadAsync(payload, cancellationToken).ConfigureAwait(false);
                }
                catch (EndOfStreamException ex)
                {
                    return new(GlbFormatException.ChunkPayloadTruncated(chunkIndex, chunkLength, ex));
                }

                switch (chunkType)
                {
                    case JsonChunkType when !jsonSeen:
                        json = payload;
                        jsonSeen = true;
                        break;
                    case BinaryChunkType when !binary.HasValue:
                        binary = (ReadOnlyMemory<byte>)payload;
                        break;
                    default:
                        // Unknown or duplicate chunk — ignored per the glTF 2.0 spec.
                        break;
                }

                offset += chunkLength;
                chunkIndex++;
            }

            if (!jsonSeen)
            {
                return new(GlbFormatException.MissingJsonChunk());
            }

            return new GlbDocument(json, binary, version);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    /// <summary>Serializes this container to a new GLB byte array, padding each chunk to a 4-byte boundary.</summary>
    /// <returns>The complete GLB file contents.</returns>
    public byte[] ToBytes()
    {
        int jsonChunk = Align4(Json.Length);
        int total = HeaderSize + ChunkHeaderSize + jsonChunk;

        int binaryChunk = 0;
        if (Binary.TryGet(out var bin))
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

    /// <summary>Asynchronously serializes this container to the given stream as GLB.</summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <remarks>
    /// Unlike <see cref="WriteTo(Stream)"/> (which materializes the whole file via <see cref="ToBytes"/>
    /// first) this streams the header and each chunk directly to <paramref name="destination"/>, writing
    /// the <see cref="Json"/> and <see cref="Binary"/> payloads without an intermediate full-file copy.
    /// </remarks>
    public async ValueTask WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        int jsonChunk = Align4(Json.Length);
        int total = HeaderSize + ChunkHeaderSize + jsonChunk;

        int binaryChunk = 0;
        if (Binary.TryGet(out var bin))
        {
            binaryChunk = Align4(bin.Length);
            total += ChunkHeaderSize + binaryChunk;
        }

        byte[] scratch = ArrayPool<byte>.Shared.Rent(ChunkHeaderSize);
        try
        {
            IAsyncBinaryWriter writer = IAsyncBinaryWriter.Create(destination, scratch.AsMemory(0, ChunkHeaderSize));

            await writer.WriteLittleEndianAsync<uint>(Magic, cancellationToken).ConfigureAwait(false);
            await writer.WriteLittleEndianAsync<uint>(Version, cancellationToken).ConfigureAwait(false);
            await writer.WriteLittleEndianAsync<uint>((uint)total, cancellationToken).ConfigureAwait(false);

            await writer.WriteLittleEndianAsync<uint>((uint)jsonChunk, cancellationToken).ConfigureAwait(false);
            await writer.WriteLittleEndianAsync<uint>(JsonChunkType, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(Json, null, cancellationToken).ConfigureAwait(false);
            // JSON chunks pad with spaces (0x20); BIN chunks pad with zeros.
            await WritePaddingAsync(writer, jsonChunk - Json.Length, (byte)' ', cancellationToken).ConfigureAwait(false);

            if (Binary.TryGet(out var binary))
            {
                await writer.WriteLittleEndianAsync<uint>((uint)binaryChunk, cancellationToken).ConfigureAwait(false);
                await writer.WriteLittleEndianAsync<uint>(BinaryChunkType, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(binary, null, cancellationToken).ConfigureAwait(false);
                await WritePaddingAsync(writer, binaryChunk - binary.Length, 0x00, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    private static async ValueTask WritePaddingAsync(
        IAsyncBinaryWriter writer, int count, byte value, CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return;
        }

        byte[] padding = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            Array.Fill(padding, value, 0, count);
            await writer.WriteAsync(padding.AsMemory(0, count), null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(padding);
        }
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

        if (Binary.TryGet(out var bin))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)binaryChunk);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(offset + 4)..], BinaryChunkType);
            offset += ChunkHeaderSize;
            bin.Span.CopyTo(span[offset..]);
            // BIN padding is 0x00 — already present from the zeroed buffer.
        }
    }
}
