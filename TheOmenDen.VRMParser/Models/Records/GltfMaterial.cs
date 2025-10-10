using Corvus.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.MaterialPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfMaterial;

[JsonSchemaTypeGenerator($"../../{PathingConstants.MaterialPath}.normalTextureInfo{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfMaterialNormalTextureInfo;

[JsonSchemaTypeGenerator($"../../{PathingConstants.MaterialPath}.occlusionTextureInfo{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfMaterialOcclusionTextureInfo;

[JsonSchemaTypeGenerator($"../../{PathingConstants.MaterialPath}.pbrMetallicRoughness{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfMaterialPbrMetallicRoughness;