namespace OliveGameStudio;

/// <summary>
/// How a ship handles: the numbers that decide what flying it feels like.
/// </summary>
/// <remarks>
/// The engine owns the physics; a game owns these. A different ship, or a ship the player has
/// upgraded, is a different instance of this record rather than a different physics.
/// </remarks>
/// <param name="Acceleration">
/// How hard the ship accelerates at full thrust, in world units per second per second. Must be
/// greater than zero.
/// </param>
/// <param name="Drag">
/// How much of its velocity the ship sheds per second, as a rate rather than a proportion —
/// velocity decays by <c>e^(-Drag)</c> over a second of coasting. Must be greater than zero:
/// without drag the ship would accelerate forever and never settle at a top speed.
/// </param>
/// <param name="TurnRate">How fast the ship turns at full helm, in radians per second.</param>
public sealed record ShipHandling(double Acceleration, double Drag, double TurnRate)
{
    /// <summary>
    /// Gets the speed the ship settles at under full thrust, in world units per second.
    /// </summary>
    /// <remarks>
    /// Derived rather than configured. A separate top speed could be set to disagree with the
    /// acceleration and drag that produce it, and then the ship would either never reach its
    /// stated top speed or hit an invisible wall short of the one the physics implies.
    /// </remarks>
    public double MaximumSpeed => Acceleration / Drag;

    /// <summary>
    /// Gets how long the ship takes to turn all the way round at full helm, in seconds, or
    /// <see cref="double.PositiveInfinity"/> for a ship with no helm at all.
    /// </summary>
    /// <remarks>
    /// Derived for the same reason as <see cref="MaximumSpeed"/>, and for a reason that has already
    /// happened once: how quickly a ship comes about is what the number is chosen for, but the
    /// number itself is radians a second, so a turn rate, a test asserting a time and a comment
    /// describing the feel drifted into three different claims about the same ship. There is one
    /// place to say it now, and it is computed rather than written down.
    /// </remarks>
    public double SecondsForAFullCircle => Math.Tau / TurnRate;
}
