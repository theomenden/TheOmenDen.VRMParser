using System.Buffers.Binary;
using System.Text;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>Helpers for assembling raw GLB byte buffers for tests.</summary>
internal static class GlbTestData
{
    /// <summary>A minimal valid glTF 2.0 JSON document.</summary>
    public const string MinimalGltfJson = """{"asset":{"version":"2.0"}}""";

    /// <summary>
    /// Builds a spec-compliant GLB byte buffer with a JSON chunk and an optional BIN chunk,
    /// padding both chunks to a 4-byte boundary (JSON with spaces, BIN with zeros).
    /// </summary>
    public static byte[] Build(string json, byte[]? binary = null, uint version = GlbDocument.SupportedVersion)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        int jsonChunk = Align4(jsonBytes.Length);

        int total = 12 + 8 + jsonChunk;
        int binaryChunk = 0;
        if (binary is not null)
        {
            binaryChunk = Align4(binary.Length);
            total += 8 + binaryChunk;
        }

        byte[] buffer = new byte[total];
        Span<byte> span = buffer;

        BinaryPrimitives.WriteUInt32LittleEndian(span, GlbDocument.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], version);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)total);

        int offset = 12;
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)jsonChunk);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(offset + 4)..], GlbDocument.JsonChunkType);
        offset += 8;
        jsonBytes.CopyTo(span[offset..]);
        span[(offset + jsonBytes.Length)..(offset + jsonChunk)].Fill((byte)' ');
        offset += jsonChunk;

        if (binary is not null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)binaryChunk);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(offset + 4)..], GlbDocument.BinaryChunkType);
            offset += 8;
            binary.CopyTo(span[offset..]);
        }

        return buffer;
    }

    private static int Align4(int length) => (length + 3) & ~3;
}
