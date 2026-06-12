namespace TheOmenDen.VRMParser.Glb;

/// <summary>
/// Thrown when binary glTF (<c>.glb</c> / <c>.vrm</c>) data does not conform to the GLB
/// container format — bad magic, an unsupported version, misaligned or truncated chunks,
/// or a missing JSON chunk.
/// </summary>
public sealed class GlbFormatException : FormatException
{
    /// <summary>Initializes a new instance of the <see cref="GlbFormatException"/> class.</summary>
    public GlbFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GlbFormatException"/> class.</summary>
    /// <param name="message">A description of the malformation.</param>
    public GlbFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GlbFormatException"/> class.</summary>
    /// <param name="message">A description of the malformation.</param>
    /// <param name="innerException">The underlying cause.</param>
    public GlbFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
