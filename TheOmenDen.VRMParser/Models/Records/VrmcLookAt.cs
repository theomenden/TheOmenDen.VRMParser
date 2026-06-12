using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1LookAtPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcLookAt;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1LookAtPath}.rangeMap{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcLookAtRangeMap;