using DotNext;

namespace TheOmenDen.VRMParser.Glb;

/// <summary>
/// Null-hostile accessors over the <see cref="Result{T}"/> returned by <see cref="GlbDocument.Parse"/>
/// and <see cref="GlbDocument.ParseAsync"/>. They let callers branch on a fault without touching
/// <see langword="null"/>: a successful result yields the <see cref="GlbErrorCode.None"/> sentinel and
/// an empty <see cref="Optional{T}"/> rather than a null error.
/// </summary>
public static class GlbResultExtensions
{
    /// <summary>
    /// Gets the <see cref="GlbErrorCode"/> describing why a parse failed, or
    /// <see cref="GlbErrorCode.None"/> when it succeeded. Never inspects a <see langword="null"/> error.
    /// </summary>
    public static GlbErrorCode ErrorCode<T>(this in Result<T> result) =>
        result.Error is GlbFormatException glb ? glb.Code : GlbErrorCode.None;

    /// <summary>
    /// Gets the <see cref="GlbFormatException"/> a failed parse produced, as an <see cref="Optional{T}"/>
    /// that is empty for a successful result — a null-object alternative to a nullable error reference.
    /// </summary>
    public static Optional<GlbFormatException> GlbError<T>(this in Result<T> result) =>
        result.Error is GlbFormatException glb ? glb : Optional<GlbFormatException>.None;
}
