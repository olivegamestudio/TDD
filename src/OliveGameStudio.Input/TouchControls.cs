namespace OliveGameStudio;

/// <summary>
/// What the fingers on the overlay are asking for this frame.
/// </summary>
/// <param name="Ship">What the helm circle is asking the ship to do.</param>
/// <param name="Firing">Whether the fire circle is being held.</param>
/// <remarks>
/// Two answers in one, because one frame of touch decides both and they are read together. The
/// flying half is a <see cref="ShipControls"/> and nothing else: the physics must not be able to
/// tell that a thumb rather than a stick produced it, which is the whole reason the overlay exists
/// on this side of the seam.
/// <para>
/// <paramref name="Firing"/> is reported and, for now, goes nowhere: the engine has no weapons to
/// point it at. It is here rather than left out because the right-hand circle is half of the
/// overlay, and an overlay that silently dropped it would read as a fire control that does not
/// work rather than as one whose other end has not been built.
/// </para>
/// </remarks>
public readonly record struct TouchControls(ShipControls Ship, bool Firing)
{
    /// <summary>
    /// Hands off both controls. What an overlay nobody is touching reports.
    /// </summary>
    public static readonly TouchControls Neutral = new(ShipControls.Neutral, false);
}
