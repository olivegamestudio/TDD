using System.Runtime.CompilerServices;
using nkast.Aether.Physics2D.Dynamics;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace OliveGameStudio;

/// <summary>
/// The ship's physics. Once a frame it turns the pilot's controls into velocity, and velocity into
/// a change in the player's position.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here knows about quests, and nothing here is asked to. Systems that react to the player
/// read the position the ship leaves behind; they do not get to say how it got there.
/// </para>
/// <para>
/// Thrust and drag are Aether.Physics2D: the ship is a one-body, gravity-free <c>World</c>, thrust
/// is a force applied to it every physics step, and drag is the body's linear damping. Turning is
/// not — the helm was never modelled with inertia, only a rate the pilot commands, so it stays the
/// hand-rolled kinematic update it always was; only translation needed a rigid body under it.
/// </para>
/// <para>
/// Aether steps in fixed increments rather than by whatever <paramref name="frameTime"/>
/// Update happens to be handed — a variable-length step is what makes an iterative solver's answer
/// depend on how the frame was sliced, the opposite of what pillar 1 asks for. An accumulator
/// banks each frame's ticks and drains them in whole <see cref="FixedTimeStep"/> steps, so the
/// number of steps taken to cover a stretch of game time depends only on the time covered, never
/// on how many <c>Update</c> calls delivered it.
/// </para>
/// <para>
/// The body's own <c>Position</c> is zeroed at the start of every <c>Update</c> and only ever holds
/// one frame's travel: Aether's positions are <see cref="float"/>, and a ship that can fly an
/// unbounded world would eventually outrun single precision. <see cref="Player.Position"/> stays
/// the <see cref="double"/> record of where the ship actually is; the body exists only to work out
/// how far it just moved.
/// </para>
/// </remarks>
public sealed class ShipMovement
{
    /// <summary>
    /// How often Aether steps, independent of the render frame rate. 1000 Hz divides a second of
    /// <see cref="TimeSpan"/> ticks exactly, so an accumulator draining it never carries a rounding
    /// remainder from one second to the next.
    /// </summary>
    /// <remarks>
    /// Aether's damping is a per-step Padé approximation of the exact exponential decay the old
    /// closed-form model used, not the exact answer itself — it converges towards the same
    /// terminal speed as the step gets smaller, but never quite reaches the old model's numbers at
    /// any step rate a real frame budget can afford. 1000 Hz is one body with no fixtures and
    /// nothing to collide against, cheap enough to not be a frame-budget question yet, and close
    /// enough to the exact answer that the gap is a tuning-invisible fraction of a percent rather
    /// than something a pilot could feel.
    /// </remarks>
    static readonly TimeSpan FixedTimeStep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 1000);

    readonly ShipHandling _handling;
    readonly World _world = new(AetherVector2.Zero);
    readonly Body _body;

    long _pendingTicks;

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

        _body = _world.CreateBody(AetherVector2.Zero, rotation: 0f, BodyType.Dynamic);

        // Unit mass, so a force of Handling.Acceleration produces exactly that acceleration
        // (F = ma) — the number content authors under "Acceleration" keeps the meaning its XML
        // doc already promises, rather than being scaled by a hull mass nothing has decided yet.
        _body.Mass = 1f;
        _body.LinearDamping = (float)handling.Drag;

        // The ship has no shapes to sleep a frame after — it is flown every frame this screen is
        // up — and a sleeping body ignores the force applied to wake it up cleanly this same step.
        _body.SleepingAllowed = false;
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
    public Velocity Velocity => new(_body.LinearVelocity.X, _body.LinearVelocity.Y);

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
        _body.LinearVelocity = AetherVector2.Zero;
        _body.Position = AetherVector2.Zero;
        _pendingTicks = 0;
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

        if (frameTime <= TimeSpan.Zero)
        {
            // a paused or zero length frame, or one going backwards: holding still is the whole
            // point of it, and Aether has nothing to step
            return;
        }

        // computed once for the whole frame rather than once a physics step, exactly as the helm
        // always has been — the frame's turn is one decision, not one made fresh every fixed step
        Heading = Wrap(Heading + (controls.Turn * _handling.TurnRate * frameTime.TotalSeconds));

        // thrust is applied along the way the ship is pointing, so turning is how you go somewhere
        double thrust = controls.Thrust * _handling.Acceleration;
        AetherVector2 force = new(
            (float)(Math.Sin(Heading) * thrust),
            (float)(Math.Cos(Heading) * thrust));

        _body.Position = AetherVector2.Zero;

        _pendingTicks += frameTime.Ticks;
        long stepTicks = FixedTimeStep.Ticks;
        while (_pendingTicks >= stepTicks)
        {
            // Aether clears a body's accumulated force at the end of every step, so it is
            // reapplied before each one rather than once for the whole frame
            _body.ApplyForce(force);
            _world.Step(FixedTimeStep);
            _pendingTicks -= stepTicks;
        }

        // the body's position is this frame's travel and nothing else — see the remarks on why
        player.MoveBy(_body.Position.X, _body.Position.Y);
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
