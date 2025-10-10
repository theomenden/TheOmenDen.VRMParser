using Corvus.Json;

namespace TheOmenDen.VRMParser.Models.Records;
[JsonSchemaTypeGenerator($"../../{PathingConstants.TexturePath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfTexture;

[JsonSchemaTypeGenerator($"../../{PathingConstants.TexturePath}Info{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfTextureInfo;