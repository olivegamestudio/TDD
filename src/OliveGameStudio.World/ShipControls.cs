namespace OliveGameStudio;

/// <summary>
/// What the pilot is asking the ship to do this frame.
/// </summary>
/// <remarks>
/// Two analogue axes rather than key presses, because that is what both a keyboard and a stick can
/// express: a key is a 1, a stick is whatever it is pushed to. The physics never learns which
/// device the pilot used.
/// </remarks>
public readonly record struct ShipControls
{
    /// <summary>
    /// Asks the ship for the given thrust and helm.
    /// </summary>
    /// <param name="thrust">
    /// How hard the engine is being asked to burn: 1 is full ahead, -1 is full astern, 0 coasts.
    /// </param>
    /// <param name="turn">
    /// Which way the ship is being turned: -1 is hard to port, 1 is hard to starboard, 0 straight.
    /// </param>
    /// <remarks>
    /// Both are clamped rather than rejected. A miscalibrated stick, or a device that reports its
    /// axes in some other range, must not be able to fly the ship harder than it is rated for —
    /// but it also must not be able to crash the game between one frame and the next.
    /// </remarks>
    public ShipControls(double thrust, double turn)
    {
        Thrust = Math.Clamp(thrust, -1, 1);
        Turn = Math.Clamp(turn, -1, 1);
    }

    /// <summary>
    /// Hands off the controls. What an unbound input device reports, and what a frame with no
    /// pilot input looks like.
    /// </summary>
    public static readonly ShipControls Neutral = new(0, 0);

    /// <summary>
    /// Gets how hard the engine is being asked to burn, from -1 full astern to 1 full ahead.
    /// </summary>
    public double Thrust { get; }

    /// <summary>
    /// Gets which way the ship is being turned, from -1 hard to port to 1 hard to starboard.
    /// </summary>
    public double Turn { get; }
}
