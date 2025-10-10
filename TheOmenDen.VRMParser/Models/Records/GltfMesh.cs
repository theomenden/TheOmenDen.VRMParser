using Corvus.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.MeshPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfMesh;
[JsonSchemaTypeGenerator($"../../{PathingConstants.MeshPath}.primitive{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct GltfMeshPrimitive;