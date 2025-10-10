using Corvus.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBone;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}.collider{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBoneCollider;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}.colliderGroup{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBoneColliderGroup;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}.joint{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBoneJoint;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}.shape{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBoneShape;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}.spring{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBoneSpring;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}_extended_collider{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBoneExtendedCollider;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1SpringBonePath}_extended_collider.shape{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcSpringBoneExtendedColliderShape;