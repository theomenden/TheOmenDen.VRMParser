using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1HumanoidPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcHumanoid;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1HumanoidPath}.humanBones{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcHumanoidHumanBones;
[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1HumanoidPath}.humanBones.humanBone{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcHumanoidHumanBonesHumanBone;