using System.Runtime.CompilerServices;
using nkast.Aether.Physics2D.Dynamics;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace OliveGameStudio;

/// <summary>
/// The ship's physics. Once a frame it turns the pilot's controls into velocity, and velocity into
/// a change in the player's position — and, now the ship has a hull, into whatever it collides
/// with along the way.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here knows about quests, and nothing here is asked to. Systems that react to the player
/// read the position the ship leaves behind; they do not get to say how it got there.
/// </para>
/// <para>
/// Thrust and drag are Aether.Physics2D: the ship is a one-body <c>World</c>, thrust is a force
/// applied to it every physics step, and drag is the body's linear damping. Turning is not — the
/// helm was never modelled with inertia, only a rate the pilot commands, so it stays the
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
/// <b>The body's position is synced from <see cref="Player.Position"/> at the start of every
/// <c>Update</c>, not accumulated in the body itself.</b> Collision needs the ship and whatever it
/// might hit to actually occupy the same coordinates, which a body that never held more than one
/// frame's travel could not give an obstacle placed at its true position. The narrowing to
/// <see cref="float"/> that costs is the same one <see cref="Player.Position"/> already takes at
/// the camera and at the edge of what a save can resume — applied one layer earlier rather than a
/// new kind of risk. <see cref="Player.Position"/> itself stays exact: what physics hands back is
/// the *delta* the step produced, read as the body's position before stepping subtracted from its
/// position after, and it is that delta which is added to the player's own double-precision
/// record. A long flight can lose a little of the delta's own precision to the distance already
/// flown, never to the running total.
/// </para>
/// <para>
/// <b>An idle ship is never synced or stepped at all.</b> <see cref="Player.Position"/> is not
/// always moved by flying — a save being resumed, a test walking the player by hand — and syncing
/// an idle body to wherever that lands would let Aether discover an overlap with an obstacle on a
/// frame nobody asked the ship to fly, and correct it as though it had. A ship still coasting from
/// real thrust is not idle and is not skipped; only one with neutral controls and no velocity is.
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
    /// any step rate a real frame budget can afford. 1000 Hz is cheap enough for one moving body
    /// against a modest field of static obstacles to not be a frame-budget question yet, and close
    /// enough to the exact answer that the gap is a tuning-invisible fraction of a percent rather
    /// than something a pilot could feel.
    /// </remarks>
    static readonly TimeSpan FixedTimeStep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 1000);

    readonly ShipHandling _handling;
    readonly World _world = new(AetherVector2.Zero);
    readonly Body _body;

    long _pendingTicks;

    /// <summary>
    /// Creates the physics for a ship that handles the given way and takes up the given amount of
    /// room.
    /// </summary>
    /// <param name="handling">The ship's acceleration, drag and turn rate.</param>
    /// <param name="hullRadius">
    /// How much of a circle the hull fills, for the purpose of colliding with the world — not the
    /// ship's drawn size, which is a rendering concern this never sees.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The handling could not be flown: a ship with no acceleration never moves, and one with no
    /// drag never stops accelerating and has no top speed. A value that is not a finite number is
    /// refused for the same reason and named the same way — infinite acceleration makes
    /// <see cref="ShipHandling.MaximumSpeed"/> infinite and the ship leaves the world in a frame or
    /// two, infinite drag makes it zero so full thrust moves the ship nowhere, and an infinite turn
    /// rate leaves the heading at whatever wrapping infinity happens to produce. All are content
    /// mistakes that would otherwise only show up as a ship that feels wrong, at a distance from
    /// the numbers that caused it. <paramref name="hullRadius"/> that is not a positive, finite
    /// number is refused for the same reason: a hull with no size collides with nothing, and one
    /// with an infinite size collides with everything, everywhere, at once.
    /// </exception>
    public ShipMovement(ShipHandling handling, double hullRadius)
    {
        ArgumentNullException.ThrowIfNull(handling);

        // Ordered before the sign guards so that negative infinity and NaN are named by the rule
        // they actually break. Both already fail the guards below, but NaN only because the sign
        // bit of double.NaN happens to be set — incidental behaviour to lean on for the one value
        // that would otherwise spread from the heading into every position the ship reports.
        ThrowIfNotFinite(handling.Acceleration);
        ThrowIfNotFinite(handling.Drag);
        ThrowIfNotFinite(handling.TurnRate);
        ThrowIfNotFinite(hullRadius);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(handling.Acceleration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(handling.Drag);
        ArgumentOutOfRangeException.ThrowIfNegative(handling.TurnRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hullRadius);

        _handling = handling;

        _body = _world.CreateBody(AetherVector2.Zero, rotation: 0f, BodyType.Dynamic);
        _body.CreateCircle((float)hullRadius, density: 1f);

        // Heading is this type's own kinematic update, never Aether's — see the remarks on why —
        // so nothing here wants the body free to spin from a collision's own momentum either.
        // Set before Mass below and not after: FixedRotation recomputes mass data from the
        // fixture's own area and density, which would silently throw away the override that
        // follows it rather than the other way round.
        _body.FixedRotation = true;

        // Unit mass, so a force of Handling.Acceleration produces exactly that acceleration
        // (F = ma) — the number content authors under "Acceleration" keeps the meaning its XML
        // doc already promises, rather than being scaled by a hull mass nothing has decided yet.
        // Asserted after both of the above, which would otherwise have left it at whatever the
        // circle's area and a density picked only for the fixture to exist worked out to.
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
        _pendingTicks = 0;
    }

    /// <summary>
    /// Puts a fixed, circular obstacle into the ship's physics — a rock, a hull, anything the ship
    /// should not be able to fly through. It never moves once placed.
    /// </summary>
    /// <param name="position">Where the obstacle stands in the world.</param>
    /// <param name="radius">How big a circle it fills. Must be a positive, finite number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="radius"/> is not a positive, finite number — an obstacle with no size blocks
    /// nothing, and one of infinite size blocks everywhere.
    /// </exception>
    /// <remarks>
    /// This knows nothing about what the obstacle represents — a rock, a wreck, a wall — because
    /// that is content, and content is the game's to supply. It only ever adds; nothing here removes
    /// one, because nothing yet asks to.
    /// </remarks>
    public void AddObstacle(Position position, double radius)
    {
        ThrowIfNotFinite(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        Body obstacle = _world.CreateBody(ToAether(position), rotation: 0f, BodyType.Static);
        obstacle.CreateCircle((float)radius, density: 1f);
    }

    /// <summary>
    /// Flies the ship for one frame: applies the helm, burns the engine, and moves the player by
    /// the distance actually covered — stopping short of it wherever the hull meets an obstacle.
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

        if (controls.IsNeutral && _body.LinearVelocity == AetherVector2.Zero)
        {
            // an idle ship stays idle without being stepped — which matters for more than saving
            // the work. Player.Position can move without this ever being asked to fly, a save
            // being resumed into a position or a test walking one by hand, and syncing the body
            // to it below would otherwise plant a ship with real momentum's-worth of nothing into
            // whatever happens to be there — including the middle of an obstacle, which Aether
            // would then push the player out of on this "idle" frame's behalf. A ship already
            // moving does not get this early exit, because drag still has to bring it to rest.
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

        // read fresh from the authoritative double every frame rather than left to accumulate in
        // the body itself — see the remarks on why this still keeps Player.Position exact
        AetherVector2 opening = ToAether(player.Position);
        _body.Position = opening;

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

        player.MoveBy(_body.Position.X - opening.X, _body.Position.Y - opening.Y);
    }

    /// <summary>
    /// Narrows a world position to what Aether's own maths can hold.
    /// </summary>
    static AetherVector2 ToAether(Position position) => new((float)position.X, (float)position.Y);

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
