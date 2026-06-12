using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1NodeConstraintsPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcNodeConstraintRoot;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1NodeConstraintsPath}.constraint{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcNodeConstraint;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1NodeConstraintsPath}.aimConstraint{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcNodeConstraintAimConstraint;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1NodeConstraintsPath}.rotationConstraint{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcNodeConstraintRotationConstraint;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1NodeConstraintsPath}.rollConstraint{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcNodeConstraintRollConstraint;