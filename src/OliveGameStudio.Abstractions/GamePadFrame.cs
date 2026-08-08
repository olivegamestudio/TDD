namespace OliveGameStudio;

/// <summary>
/// One frame of a gamepad, as the platform host read it.
/// </summary>
/// <param name="Connected">Whether a pad is plugged in at all.</param>
/// <param name="Thrust">The stick's thrust axis, nominally -1 to 1, before any dead zone.</param>
/// <param name="Turn">The stick's helm axis, nominally -1 to 1, before any dead zone.</param>
/// <param name="Confirm">Whether the confirm button is held — the one that works a menu button.</param>
/// <param name="Strafe">
/// A second stick's sideways axis, nominally -1 to 1, before any dead zone. Defaults to
/// <c>0</c>, trailing behind <paramref name="Confirm"/> rather than beside <paramref name="Turn"/>,
/// so every positional construction written before strafing existed still compiles unchanged.
/// </param>
/// <remarks>
/// The axes arrive raw, with the platform's own dead zone turned off, because the dead zone is a
/// decision about how the game should feel rather than something a driver should be making on the
/// game's behalf. It is applied once, where the stick is turned into
/// <c>ShipControls</c>, from one stated number.
/// <para>
/// <paramref name="Connected"/> is carried rather than inferred from the axes reading zero. A pad
/// that is not plugged in and a pad being held still are the same numbers and are not the same
/// thing — the first cannot be flown with, and a game locked to it needs to be able to say so.
/// </para>
/// </remarks>
public readonly record struct GamePadFrame(
    bool Connected,
    double Thrust,
    double Turn,
    bool Confirm,
    double Strafe = 0)
{
    /// <summary>
    /// No pad at all. What a host with nothing plugged in reports.
    /// </summary>
    public static readonly GamePadFrame None = default;
}
