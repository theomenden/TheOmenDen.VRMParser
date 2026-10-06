using System.Buffers.Binary;
using System.Text;
using Bogus;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>Helpers and GLB layout constants for assembling raw GLB byte buffers in tests.</summary>
internal static class GlbTestData
{
    /// <summary>The glTF asset version every test document declares.</summary>
    public const string GltfAssetVersion = "2.0";

    /// <summary>A minimal valid glTF 2.0 JSON document.</summary>
    public const string MinimalGltfJson = $$$"""{"asset":{"version":"{{{GltfAssetVersion}}}"}}""";

    /// <summary>Size of the GLB header: magic, version, declared length (three <c>uint</c>s).</summary>
    public const int HeaderSize = 12;

    /// <summary>Size of a chunk header: chunk length and chunk type (two <c>uint</c>s).</summary>
    public const int ChunkHeaderSize = 8;

    /// <summary>Every chunk is padded to a multiple of this many bytes.</summary>
    public const int ChunkAlignment = 4;

    /// <summary>Byte offset of the header's container version.</summary>
    public const int VersionOffset = sizeof(uint);

    /// <summary>Byte offset of the header's declared total length.</summary>
    public const int DeclaredLengthOffset = VersionOffset + sizeof(uint);

    /// <summary>Byte offset of the first (JSON) chunk's length field.</summary>
    public const int FirstChunkLengthOffset = HeaderSize;

    /// <summary>Byte offset of the first (JSON) chunk's type field.</summary>
    public const int FirstChunkTypeOffset = FirstChunkLengthOffset + sizeof(uint);

    /// <summary>A deterministic pseudo-random payload; the length doubles as the seed so runs are reproducible.</summary>
    public static byte[] Payload(int length) => new Faker { Random = new Randomizer(length + 1) }.Random.Bytes(length);

    /// <summary>
    /// Builds a spec-compliant GLB byte buffer with a JSON chunk and an optional BIN chunk,
    /// padding both chunks to a 4-byte boundary (JSON with spaces, BIN with zeros).
    /// </summary>
    public static byte[] Build(string json, byte[]? binary = null, uint version = GlbDocument.SupportedVersion)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        int jsonChunk = Align(jsonBytes.Length);

        int total = HeaderSize + ChunkHeaderSize + jsonChunk;
        int binaryChunk = 0;
        if (binary is not null)
        {
            binaryChunk = Align(binary.Length);
            total += ChunkHeaderSize + binaryChunk;
        }

        byte[] buffer = new byte[total];
        Span<byte> span = buffer;

        BinaryPrimitives.WriteUInt32LittleEndian(span, GlbDocument.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[VersionOffset..], version);
        BinaryPrimitives.WriteUInt32LittleEndian(span[DeclaredLengthOffset..], (uint)total);

        int offset = HeaderSize;
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)jsonChunk);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(offset + sizeof(uint))..], GlbDocument.JsonChunkType);
        offset += ChunkHeaderSize;
        jsonBytes.CopyTo(span[offset..]);
        span[(offset + jsonBytes.Length)..(offset + jsonChunk)].Fill((byte)' ');
        offset += jsonChunk;

        if (binary is not null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)binaryChunk);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(offset + sizeof(uint))..], GlbDocument.BinaryChunkType);
            offset += ChunkHeaderSize;
            binary.CopyTo(span[offset..]);
        }

        return buffer;
    }

    private static int Align(int length) => (length + ChunkAlignment - 1) & ~(ChunkAlignment - 1);
}
