using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.ScenePath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfScene;