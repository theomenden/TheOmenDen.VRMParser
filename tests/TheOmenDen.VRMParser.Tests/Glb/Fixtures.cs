namespace TheOmenDen.VRMParser.Tests.Glb;

/// <summary>The committed binary fixtures in <c>Fixtures/</c> and the facts the tests assert about them.</summary>
internal static class Fixtures
{
    /// <summary>Real CC0 Khronos exporter output (glTF only, has a BIN chunk).</summary>
    public const string Box = "Box.glb";

    /// <summary>Synthesized VRM 0.x avatar (see <c>generate_fixtures.py</c>).</summary>
    public const string MinimalVrm0 = "MinimalVrm0.vrm";

    /// <summary>Synthesized VRM 1.0 avatar (see <c>generate_fixtures.py</c>).</summary>
    public const string MinimalVrm1 = "MinimalVrm1.vrm";

    /// <summary>Every committed fixture.</summary>
    public static readonly string[] All = [Box, MinimalVrm0, MinimalVrm1];

    // Values written by generate_fixtures.py — keep in sync with REQUIRED_BONES and the meta name there.

    /// <summary>The <c>meta.name</c> of the VRM 1.0 fixture.</summary>
    public const string AvatarName = "TheOmenDen Test Avatar";

    /// <summary>The number of VRM 1.0 required human bones both VRM fixtures map.</summary>
    public const int RequiredHumanBoneCount = 15;

    private const string Directory = "Fixtures";

    /// <summary>The absolute path of a fixture copied to the test output directory.</summary>
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, Directory, name);
}

/// <summary>glTF / VRM JSON names the tests navigate through.</summary>
internal static class VrmJson
{
    /// <summary>VRM 0.x extension name.</summary>
    public const string Vrm0Extension = "VRM";

    /// <summary>VRM 1.0 extension name.</summary>
    public const string Vrm1Extension = "VRMC_vrm";

    /// <summary>VRM 0.x <c>specVersion</c>.</summary>
    public const string Vrm0SpecVersion = "0.0";

    /// <summary>VRM 1.0 <c>specVersion</c>.</summary>
    public const string Vrm1SpecVersion = "1.0";

    public static ReadOnlySpan<byte> Extensions => "extensions"u8;
    public static ReadOnlySpan<byte> ExtensionsUsed => "extensionsUsed"u8;
    public static ReadOnlySpan<byte> SpecVersion => "specVersion"u8;
    public static ReadOnlySpan<byte> Meta => "meta"u8;
    public static ReadOnlySpan<byte> Name => "name"u8;
    public static ReadOnlySpan<byte> Humanoid => "humanoid"u8;
    public static ReadOnlySpan<byte> HumanBones => "humanBones"u8;
}
