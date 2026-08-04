using System.Runtime.CompilerServices;

namespace OliveGameStudio;

/// <summary>
/// The ship's physics. Once a frame it turns the pilot's controls into velocity, and velocity into
/// a change in the player's position.
/// </summary>
/// <remarks>
/// Nothing here knows about quests, and nothing here is asked to. Systems that react to the player
/// read the position the ship leaves behind; they do not get to say how it got there.
///
/// Drag is integrated exactly rather than stepped a frame at a time, because <c>e^(-k·a)·e^(-k·b)</c>
/// is <c>e^(-k·(a+b))</c>: two half frames land exactly where one whole frame does, so a ship
/// covers the same ground at 30 frames a second as at 144. A ship whose reach depends on the frame
/// rate is a bug against "flying feels good", not a tuning detail.
/// </remarks>
public sealed class ShipMovement
{
    readonly ShipHandling _handling;

    /// <summary>
    /// Creates the physics for a ship that handles the given way.
    /// </summary>
    /// <param name="handling">The ship's acceleration, drag and turn rate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The handling could not be flown: a ship with no acceleration never moves, and one with no
    /// drag never stops accelerating and has no top speed. A value that is not a finite number is
    /// refused for the same reason and named the same way — infinite acceleration makes
    /// <see cref="ShipHandling.MaximumSpeed"/> infinite and the ship leaves the world in a frame or
    /// two, infinite drag makes it zero so full thrust moves the ship nowhere, and an infinite turn
    /// rate leaves the heading at whatever wrapping infinity happens to produce. All are content
    /// mistakes that would otherwise only show up as a ship that feels wrong, at a distance from
    /// the numbers that caused it.
    /// </exception>
    public ShipMovement(ShipHandling handling)
    {
        ArgumentNullException.ThrowIfNull(handling);

        // Ordered before the sign guards so that negative infinity and NaN are named by the rule
        // they actually break. Both already fail the guards below, but NaN only because the sign
        // bit of double.NaN happens to be set — incidental behaviour to lean on for the one value
        // that would otherwise spread from the heading into every position the ship reports.
        ThrowIfNotFinite(handling.Acceleration);
        ThrowIfNotFinite(handling.Drag);
        ThrowIfNotFinite(handling.TurnRate);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(handling.Acceleration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(handling.Drag);
        ArgumentOutOfRangeException.ThrowIfNegative(handling.TurnRate);

        _handling = handling;
    }

    /// <summary>
    /// Refuses a handling value that is not a finite number, naming the value rather than the
    /// handling it came from.
    /// </summary>
    /// <remarks>
    /// The caller expression is captured so the exception says <c>handling.Drag</c> rather than
    /// <c>handling</c>, matching what the framework's own guards report for the same argument. A
    /// ship is built from authored content, and an exception naming only the record leaves whoever
    /// wrote the profile reading three numbers to work out which one was meant.
    /// </remarks>
    static void ThrowIfNotFinite(
        double value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "A ship's handling must be flown with finite numbers.");
        }
    }

    /// <summary>
    /// Gets the direction the ship is pointing, in radians in the range <c>[0, 2π)</c>. Zero points
    /// along the positive Y axis — forward — and the angle increases clockwise, towards positive X.
    /// </summary>
    public double Heading { get; private set; }

    /// <summary>
    /// Gets how fast the ship is travelling, and in which direction.
    /// </summary>
    /// <remarks>
    /// Not the same as where it is pointing. A ship that turns keeps the momentum it had, which is
    /// most of what separates flying from being dragged around by the controls.
    /// </remarks>
    public Velocity Velocity { get; private set; } = Velocity.Stationary;

    /// <summary>
    /// Brings the ship to rest facing forward, for a game that is starting or being resumed.
    /// </summary>
    /// <remarks>
    /// The save carries position, not momentum, so a resumed game begins stationary rather than
    /// picking up mid-manoeuvre at whatever speed the last session happened to leave in memory.
    /// </remarks>
    public void Reset()
    {
        Heading = 0;
        Velocity = Velocity.Stationary;
    }

    /// <summary>
    /// Flies the ship for one frame: applies the helm, burns the engine, and moves the player by
    /// the distance actually covered.
    /// </summary>
    /// <param name="player">The player whose position the ship carries.</param>
    /// <param name="controls">What the pilot is asking for this frame.</param>
    /// <param name="frameTime">How much game time this frame covers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="player"/> is <c>null</c>.</exception>
    public void Update(Player player, ShipControls controls, TimeSpan frameTime)
    {
        ArgumentNullException.ThrowIfNull(player);

        double seconds = frameTime.TotalSeconds;
        if (seconds <= 0)
        {
            // a paused or zero length frame: holding still is the whole point of it, and the
            // maths below would divide a frame's worth of nothing into a heading change
            return;
        }

        Heading = Wrap(Heading + (controls.Turn * _handling.TurnRate * seconds));

        // thrust is applied along the way the ship is pointing, so turning is how you go somewhere
        double thrust = controls.Thrust * _handling.Acceleration;
        double accelerationX = Math.Sin(Heading) * thrust;
        double accelerationY = Math.Cos(Heading) * thrust;

        // v(t) = terminal + (v₀ - terminal)·e^(-drag·t): the exact answer for a constant thrust
        // against linear drag. Linear drag leaves the axes independent, so each is solved alone.
        double decay = Math.Exp(-_handling.Drag * seconds);
        double terminalX = accelerationX / _handling.Drag;
        double terminalY = accelerationY / _handling.Drag;

        Velocity opening = Velocity;
        Velocity = new Velocity(
            terminalX + ((opening.X - terminalX) * decay),
            terminalY + ((opening.Y - terminalY) * decay));

        // and the integral of that velocity over the frame, which is the ground actually covered.
        // Stepping position by the closing velocity instead would overshoot every frame.
        player.MoveBy(
            (terminalX * seconds) + ((opening.X - terminalX) * (1 - decay) / _handling.Drag),
            (terminalY * seconds) + ((opening.Y - terminalY) * (1 - decay) / _handling.Drag));
    }

    /// <summary>
    /// Brings a heading back into <c>[0, 2π)</c>, so a ship that keeps turning one way reports the
    /// same heading each time round rather than an ever growing angle.
    /// </summary>
    static double Wrap(double heading)
    {
        double wrapped = heading % Math.Tau;

        return wrapped < 0 ? wrapped + Math.Tau : wrapped;
    }
}
