using Corvus.Json;

namespace TheOmenDen.VRMParser.Models.Records;
[JsonSchemaTypeGenerator($"../../{PathingConstants.AccessorPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfAccessor;

[JsonSchemaTypeGenerator($"../../{PathingConstants.AccessorPath}.sparse{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfSparseAccessor;

[JsonSchemaTypeGenerator($"../../{PathingConstants.AccessorPath}.sparse.indices{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfSparseAccessorIndices;

[JsonSchemaTypeGenerator($"../../{PathingConstants.AccessorPath}.sparse.values{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfSparseAccessorValues;