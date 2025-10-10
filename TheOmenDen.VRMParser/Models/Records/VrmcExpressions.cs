using Corvus.Json;

namespace TheOmenDen.VRMParser.Models.Records;


[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1ExpressionsPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcExpressions;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1ExpressionsExpressionPath}{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcExpressionsExpression;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1ExpressionsExpressionPath}.materialColorBind{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcExpressionsExpressionMaterialColorBind;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1ExpressionsExpressionPath}.morphTargetBind{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcExpressionsExpressionMorphTargetBind;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1ExpressionsExpressionPath}.textureTransformBind{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcExpressionsExpressionTextureTransformBind;