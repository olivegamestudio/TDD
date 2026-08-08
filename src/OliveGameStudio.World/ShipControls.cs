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
    /// Asks the ship for the given thrust, helm and strafe.
    /// </summary>
    /// <param name="thrust">
    /// How hard the engine is being asked to burn: 1 is full ahead, -1 is full astern, 0 coasts.
    /// </param>
    /// <param name="turn">
    /// Which way the ship is being turned: -1 is hard to port, 1 is hard to starboard, 0 straight.
    /// </param>
    /// <param name="strafe">
    /// How hard the ship is being pushed sideways, independent of where it is pointed: -1 is hard
    /// to port, 1 is hard to starboard, 0 is none. Defaults to none, so every caller written before
    /// strafing existed still asks for exactly what it did before.
    /// </param>
    /// <remarks>
    /// All three are clamped rather than rejected. A miscalibrated stick, or a device that reports
    /// its axes in some other range, must not be able to fly the ship harder than it is rated for —
    /// but it also must not be able to crash the game between one frame and the next.
    /// </remarks>
    public ShipControls(double thrust, double turn, double strafe = 0)
    {
        Thrust = Axis(thrust);
        Turn = Axis(turn);
        Strafe = Axis(strafe);
    }

    /// <summary>
    /// Brings one axis into the range the ship is rated for, taking an axis that could not be read
    /// as hands off.
    /// </summary>
    /// <remarks>
    /// <see cref="Math.Clamp(double, double, double)"/> does not hold for <c>NaN</c> — it returns
    /// it unchanged — and a device that cannot read an axis reports exactly that. One unreadable
    /// frame would put a <c>NaN</c> in the heading, then the velocity, then the position, and
    /// nothing after it recovers: the ship is still being drawn and flown, and no longer has a
    /// place in the world. Zeroing it here costs one frame of input and is the only point every
    /// device passes through.
    /// </remarks>
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
    /// Gets how hard the ship is being pushed sideways, from -1 hard to port to 1 hard to
    /// starboard, independent of <see cref="Turn"/> and of which way the ship is pointed.
    /// </summary>
    public double Strafe { get; }

    /// <summary>
    /// Gets whether nobody is flying: no axis is asking for anything.
    /// </summary>
    /// <remarks>
    /// An exact comparison against zero, which is safe because <see cref="Axis"/> normalises every
    /// axis at construction — there is no path, constructor or <c>default</c>, that puts a
    /// <c>NaN</c> in any property, and <c>NaN == 0</c> is false. That guarantee is what holds
    /// this up: were it removed, a device reporting an axis it could not read would claim to be
    /// the one being used and shut every device behind it out.
    /// </remarks>
    public bool IsNeutral => Thrust == 0 && Turn == 0 && Strafe == 0;

    /// <summary>
    /// Reads the controls from six keys — the digital case, where a key is either held or it is
    /// not and there is no travel in between.
    /// </summary>
    /// <param name="ahead">Whether the ahead key is held.</param>
    /// <param name="astern">Whether the astern key is held.</param>
    /// <param name="port">Whether the turn-to-port key is held.</param>
    /// <param name="starboard">Whether the turn-to-starboard key is held.</param>
    /// <param name="strafePort">
    /// Whether the strafe-to-port key is held. Defaults to <c>false</c>, so a caller written
    /// before strafing existed still gets exactly the turn it always asked for.
    /// </param>
    /// <param name="strafeStarboard">
    /// Whether the strafe-to-starboard key is held. Defaults to <c>false</c>, for the same reason
    /// <paramref name="strafePort"/> does.
    /// </param>
    /// <returns>The controls those keys are asking for.</returns>
    /// <remarks>
    /// Every opposite pair is summed rather than tested in order, so a player holding both gets a
    /// straight cancel — and, more to the point, the answer does not depend on which of the two
    /// the code happened to look at first. A hand flat on every key therefore reads as hands off,
    /// and reports itself that way, so it does not win the arbitration against a device somebody
    /// is actually using.
    /// </remarks>
    public static ShipControls FromKeys(
        bool ahead,
        bool astern,
        bool port,
        bool starboard,
        bool strafePort = false,
        bool strafeStarboard = false) =>
        new((ahead ? 1 : 0) - (astern ? 1 : 0),
            (starboard ? 1 : 0) - (port ? 1 : 0),
            (strafeStarboard ? 1 : 0) - (strafePort ? 1 : 0));

    /// <summary>
    /// Reads the controls from an analogue stick, absorbing the drift of a stick nobody is
    /// touching.
    /// </summary>
    /// <param name="thrust">The stick's thrust axis, nominally -1 to 1.</param>
    /// <param name="turn">The stick's helm axis, nominally -1 to 1.</param>
    /// <param name="deadZone">
    /// How far the stick must travel before it counts as being pushed, from 0 to 1.
    /// </param>
    /// <param name="strafe">
    /// A second stick's sideways axis, nominally -1 to 1. Defaults to <c>0</c> — no travel to
    /// speak of, which the dead zone would floor to nothing anyway — so a caller written before
    /// strafing existed still reads the same two axes it always did.
    /// </param>
    /// <returns>The controls the stick is asking for.</returns>
    /// <remarks>
    /// The dead zone is taken off the bottom of the travel and the rest is stretched back over the
    /// full range, rather than simply subtracted. Subtracting would cost the player the top of the
    /// range — they could push the stick to its stop and never quite ask for full thrust, which is
    /// the kind of bug that is felt long before it is found. Stretching also means crossing the
    /// edge asks for a little rather than jumping straight to a fifth of the engine. The same dead
    /// zone is applied to <paramref name="strafe"/> as to the other two, on the same reasoning: a
    /// stick that has not been touched does not return to dead centre on any axis.
    /// </remarks>
    public static ShipControls FromStick(double thrust, double turn, double deadZone, double strafe = 0) =>
        new(PastDeadZone(thrust, deadZone), PastDeadZone(turn, deadZone), PastDeadZone(strafe, deadZone));

    /// <summary>
    /// How far one axis is past its dead zone, as a fraction of the travel that is left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unreadable cases are taken first and deliberately. <see cref="Math.Sign(double)"/>
    /// <em>throws</em> on <c>NaN</c> rather than returning 0, and an ordered comparison against
    /// <c>NaN</c> is false either way round — so the dead zone test below does not catch one, and
    /// without this line a driver reporting an axis it could not read takes the game down on the
    /// frame path. The host reads its pad with the dead zone off, so raw driver floats arrive here
    /// with nothing in between.
    /// </para>
    /// <para>
    /// A negative dead zone is floored rather than refused. Left alone it inverts the feature into
    /// a sensitivity boost — at -0.5 a stick 1% off centre would ask for a third of the engine,
    /// which is precisely the drift the dead zone exists to absorb. Refusing it would be the other
    /// defensible answer, but this runs once a frame per axis, and a miscalibrated device should
    /// not be able to stop the game.
    /// </para>
    /// </remarks>
    static double PastDeadZone(double value, double deadZone)
    {
        if (double.IsNaN(value) || double.IsNaN(deadZone))
        {
            return 0;
        }

        deadZone = Math.Max(deadZone, 0);

        double travel = 1 - deadZone;
        double magnitude = Math.Abs(value);

        // travel is gone once the dead zone reaches the stop, and a stick that can never be
        // pushed far enough to count is one nobody can fly with — hands off rather than a divide
        if (travel <= 0 || magnitude <= deadZone)
        {
            return 0;
        }

        return Math.Sign(value) * (magnitude - deadZone) / travel;
    }
}
