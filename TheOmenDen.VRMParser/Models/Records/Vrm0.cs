using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0Path}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0Root;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0BlendShapePath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0Blendshape;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0BlendShapePath}.bind{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0BlendshapeBind;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0BlendShapePath}.group{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0BlendshapeGroup;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0BlendShapePath}.materialbind{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0BlendshapeMaterialBind;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0FirstPersonPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0FirstPerson;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0FirstPersonPath}.degreemap{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0FirstPersonDegreeMap;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0FirstPersonPath}.meshannotation{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0FirstPersonMeshAnnotation;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0HumanoidPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0Humanoid;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0HumanoidPath}.bone{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0HumanoidBone;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0Path}.material{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0Material;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0Path}.meta{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0Meta;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0SecondaryAnimationPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0SecondaryAnimation;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0SecondaryAnimationPath}.collidergroup{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0SecondaryAnimationColliderGroup;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm0SecondaryAnimationPath}.spring{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct Vrm0SecondaryAnimationSpring;