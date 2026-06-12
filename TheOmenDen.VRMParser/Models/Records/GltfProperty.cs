using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.GltfPath}Property{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfProperty;