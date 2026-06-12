using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1Path}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm1Root;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1Path}.meta{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm1Meta;