namespace TheOmenDen.VRMParser.Results;

/// <summary>
/// The common contract implemented by every fault this library produces. Faults flow through
/// <c>DotNext.Result&lt;T&gt;</c> as exceptions, but they are constructed (not thrown) on the failure
/// path, so reading <see cref="Code"/>, <see cref="Category"/>, and <see cref="Metadata"/> never
/// pays for a stack walk.
/// </summary>
/// <remarks>
/// The members are intentionally logging-shaped: <see cref="Code"/> is a stable machine-readable
/// identifier, <see cref="Category"/> groups faults for triage, and <see cref="Metadata"/> carries
/// the positional detail (byte offsets, chunk indices, expected/actual values). A composition root
/// can destructure these straight into structured logs (Serilog et al.) without this library
/// referencing any logging sink.
/// </remarks>
public interface IVrmError
{
    /// <summary>A stable, machine-readable identifier for this fault (e.g. <c>"glb.bad_magic"</c>).</summary>
    string Code { get; }

    /// <summary>The coarse classification used to group faults for reporting and triage.</summary>
    VrmErrorCategory Category { get; }

    /// <summary>A human-readable description of the fault.</summary>
    string Message { get; }

    /// <summary>Structured, log-friendly detail about the fault. Never <see langword="null"/>.</summary>
    IReadOnlyDictionary<string, object?> Metadata { get; }
}
