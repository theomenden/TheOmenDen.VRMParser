using Corvus.Text.Json;

namespace TheOmenDen.VRMParser.Models.Records;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1MaterialsPath}hdr_emissiveMultiplier.json")]
internal readonly partial struct VrmcMaterialsHdEmissiveMultiplier;

[JsonSchemaTypeGenerator($"../../{PathingConstants.Vrm1MaterialsPath}mtoon{PathingConstants.SchemaJsonSuffix}")]
internal readonly partial struct VrmcMaterialsMtoon;