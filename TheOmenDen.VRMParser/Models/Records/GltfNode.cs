using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.NodePath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfNode;