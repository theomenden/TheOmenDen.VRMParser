namespace TheOmenDen.VRMParser.Results;

/// <summary>
/// A coarse, framework-neutral classification for every fault this library reports through its
/// <c>DotNext.Result&lt;T&gt;</c> pipeline. Kept deliberately small and stable so it can be projected
/// onto transport-specific status codes (HTTP, gRPC, …) by a consuming application without this
/// library taking a dependency on any of them.
/// </summary>
public enum VrmErrorCategory
{
    /// <summary>The category is unknown or not applicable (the sentinel/default).</summary>
    Unknown = 0,

    /// <summary>The bytes do not conform to the expected container or file structure.</summary>
    Format,

    /// <summary>A recognized but unsupported version or feature was encountered.</summary>
    Unsupported,

    /// <summary>The input ended before a required structure could be completed.</summary>
    Truncated,

    /// <summary>The data is well-formed but violates a semantic or schema rule.</summary>
    Validation,

    /// <summary>An underlying input/output operation failed.</summary>
    Io,
}
