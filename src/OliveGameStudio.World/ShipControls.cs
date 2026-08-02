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
        Thrust = Axis(thrust);
        Turn = Axis(turn);
    }

    /// <summary>
    /// Brings whatever a device reported for an axis into <c>[-1, 1]</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Math.Clamp(double, double, double)"/> does not hold for <c>NaN</c> — it returns
    /// <c>NaN</c> — and <c>NaN</c> then propagates through every arithmetic path in the physics.
    /// One unreadable frame would put it in the heading, then the velocity, then the position, and
    /// nothing after it recovers: the ship stops moving for good, every quest distance measured
    /// against that position is <c>NaN</c> so no trigger fires again, and the state is saved. So an
    /// axis that cannot be read is treated as an axis asking for nothing, which is the same thing
    /// an unbound device reports and the one reading that is always safe to fly on.
    /// </remarks>
    /// <param name="value">What the device reported.</param>
    /// <returns>The axis, clamped, with an unreadable value read as neutral.</returns>
    static double Axis(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, -1, 1);

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

    /// <summary>
    /// Gets a value indicating whether the pilot is asking for nothing at all.
    /// </summary>
    /// <remarks>
    /// How a device says "I am not the one being used", which is what lets two devices be bound at
    /// once without their answers being added together. The comparison is exact, and that is only
    /// safe because <see cref="Axis"/> has already read an unreadable axis as 0: <c>NaN == 0</c> is
    /// false, so without that a device nobody can read would claim to be the one in use and shut
    /// out the device that works. A dead zone is not what makes this safe — it zeroes a stick that
    /// is resting, and says nothing about one that cannot be read.
    /// </remarks>
    public bool IsNeutral => Thrust == 0 && Turn == 0;

    /// <summary>
    /// Reads four held keys as the two analogue axes.
    /// </summary>
    /// <remarks>
    /// A key is all or nothing, so it becomes a 1. Opposite keys held together cancel rather than
    /// one winning: a player rolling their hand across two keys gets the ship to stop asking,
    /// which is what the keys literally say, and not a direction that depends on which key the
    /// code happened to check first.
    /// </remarks>
    /// <param name="ahead">Whether full ahead is held.</param>
    /// <param name="astern">Whether full astern is held.</param>
    /// <param name="port">Whether the helm is held to port.</param>
    /// <param name="starboard">Whether the helm is held to starboard.</param>
    /// <returns>The controls those keys ask for.</returns>
    public static ShipControls FromKeys(bool ahead, bool astern, bool port, bool starboard) =>
        new(
            thrust: (ahead ? 1 : 0) - (astern ? 1 : 0),
            turn: (starboard ? 1 : 0) - (port ? 1 : 0));

    /// <summary>
    /// Reads an analogue stick as the two axes, ignoring anything inside its dead zone.
    /// </summary>
    /// <remarks>
    /// Past the dead zone the remaining travel is stretched back over the full range, so the first
    /// millimetre of real movement asks for a little and the stick still reaches 1 at its stop. A
    /// dead zone that merely zeroed the middle would make the ship jump from nothing to
    /// <paramref name="deadZone"/> the moment the stick crossed it.
    /// </remarks>
    /// <param name="thrust">The stick's raw thrust axis, 1 being full ahead.</param>
    /// <param name="turn">The stick's raw helm axis, 1 being hard to starboard.</param>
    /// <param name="deadZone">
    /// How far the stick must move before it is believed, from 0 to 1. A worn stick rests slightly
    /// off centre, and without this it would fly the ship on its own.
    /// </param>
    /// <returns>The controls the stick asks for.</returns>
    public static ShipControls FromStick(double thrust, double turn, double deadZone) =>
        new(PastDeadZone(thrust, deadZone), PastDeadZone(turn, deadZone));

    /// <summary>
    /// Zeroes an axis inside the dead zone and rescales what is left over the full range.
    /// </summary>
    /// <remarks>
    /// The <c>NaN</c> guard is not the same one the constructor holds, and it has to be here.
    /// <see cref="Math.Sign(double)"/> <em>throws</em> on <c>NaN</c> rather than answering 0, and
    /// the dead zone test above it does not catch one — an ordered comparison against <c>NaN</c> is
    /// false either way round, so an unreadable axis falls straight through into the throw. A
    /// gamepad is read with the platform's own dead zone off, precisely so this one can be applied
    /// instead, which means raw driver floats arrive here with nothing in between. Reading an axis
    /// nobody can read as an axis asking for nothing is the answer the rest of the type already
    /// gives; throwing on the frame path takes the game down mid-flight.
    /// </remarks>
    static double PastDeadZone(double value, double deadZone)
    {
        if (double.IsNaN(value) || double.IsNaN(deadZone))
        {
            return 0;
        }

        double magnitude = Math.Abs(value);
        double travel = 1 - deadZone;

        // a dead zone of 1 or more leaves nowhere for the stick to go; treat it as unusable
        // rather than dividing by zero and flying the ship on an infinity
        if (magnitude <= deadZone || travel <= 0)
        {
            return 0;
        }

        return Math.Sign(value) * (magnitude - deadZone) / travel;
    }
}
