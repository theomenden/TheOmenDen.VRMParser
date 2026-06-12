using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1FirstPersonPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcFirstPerson;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1FirstPersonPath}.meshAnnotation{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcFirstPersonMeshAnnotation;