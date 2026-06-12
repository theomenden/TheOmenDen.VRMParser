using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1AnimationPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcAnimation;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1AnimationExpressionsPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcAnimationExpressions;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1AnimationExpressionsPath}.expression{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcAnimationExpressionsExpression;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1AnimationPath}.lookAt{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcAnimationLookAt;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1AnimationHumanoidPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcAnimationHumanoid;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1AnimationHumanoidPath}.humanBones{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcAnimationHumanoidHumanBones;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1AnimationHumanoidPath}.humanBones.humanBone{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcAnimationHumanoidHumanBonesHumanBone;