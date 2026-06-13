namespace TheOmenDen.VRMParser.Benchmarks;

/// <summary>A named GLB/VRM payload fed to the benchmarks as a parameter.</summary>
public sealed class BenchmarkInput
{
    /// <summary>A short label shown in the BenchmarkDotNet summary (the <c>Input</c> column).</summary>
    public required string Name { get; init; }

    /// <summary>The complete <c>.glb</c>/<c>.vrm</c> file contents.</summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }

    public override string ToString() => Name;
}

/// <summary>
/// Loads the benchmark inputs: the small committed fixtures (always available) plus an optional
/// full-size real-world avatar pointed to by <see cref="RealVrmPathEnvVar"/>. The small fixtures are
/// enough to measure per-call parse/write overhead; meaningful GC-pause numbers need the real model.
/// </summary>
public static class BenchmarkInputs
{
    /// <summary>
    /// Environment variable naming a local full-size <c>.vrm</c>/<c>.glb</c>. Shared with the test
    /// project's <c>RealWorldVrmTests</c> so one path drives both suites.
    /// </summary>
    public const string RealVrmPathEnvVar = "VRMPARSER_REAL_VRM_PATH";

    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static readonly string[] FixtureFiles =
    [
        "Box.glb",
        "MinimalVrm0.vrm",
        "MinimalVrm1.vrm",
    ];

    /// <summary>The committed small fixtures plus the optional real-world model (when configured).</summary>
    public static IEnumerable<BenchmarkInput> All()
    {
        foreach (string file in FixtureFiles)
        {
            yield return new BenchmarkInput
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Bytes = File.ReadAllBytes(Path.Combine(FixturesDirectory, file)),
            };
        }

        if (TryLoadRealModel() is { } real)
        {
            yield return real;
        }
    }

    /// <summary>
    /// Loads the real-world model from <see cref="RealVrmPathEnvVar"/>, or <see langword="null"/> when
    /// the variable is unset or the file is missing — so the suite still runs on a fresh clone.
    /// </summary>
    public static BenchmarkInput? TryLoadRealModel()
    {
        string? path = Environment.GetEnvironmentVariable(RealVrmPathEnvVar);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return new BenchmarkInput
        {
            Name = $"Real:{Path.GetFileName(path)}",
            Bytes = File.ReadAllBytes(path),
        };
    }
}
