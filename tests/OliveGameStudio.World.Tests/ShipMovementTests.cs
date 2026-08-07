namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers flying the ship: what the controls do to it, and what it does to the player's position.
/// The numbers below are a test ship, not the game's — the shipping ones are game content and are
/// covered on the game side.
/// </summary>
public sealed class ShipMovementTests
{
    /// <summary>
    /// A ship that settles at 100 units a second and takes a second to turn half a circle. Round
    /// numbers, so an expectation can be worked out by hand rather than copied from a run.
    /// </summary>
    static readonly ShipHandling Handling = new(Acceleration: 100, Drag: 1, TurnRate: Math.PI);

    /// <summary>
    /// A hull small enough that none of the flights below graze an obstacle by accident. Collision
    /// itself is covered separately, below.
    /// </summary>
    const double HullRadius = 1;

    readonly Player _player = new();

    readonly ShipMovement _ship = new(Handling, HullRadius);

    /// <summary>
    /// Flies the ship on the given controls for a stretch of time, delivered as whole frames.
    /// </summary>
    /// <remarks>
    /// The frames are cut in ticks and the split has to come out exact, because a frame time that
    /// is a rounded fraction of a second would make these tests measure <see cref="TimeSpan"/>'s
    /// rounding rather than the ship's physics.
    /// </remarks>
    void Fly(double thrust, double turn, double seconds, int frames = 1)
    {
        long ticks = (long)Math.Round(seconds * TimeSpan.TicksPerSecond);
        Assert.True(ticks % frames == 0, $"{seconds}s does not divide into {frames} whole frames");

        TimeSpan frameTime = TimeSpan.FromTicks(ticks / frames);

        for (int frame = 0; frame < frames; frame++)
        {
            _ship.Update(_player, new ShipControls(thrust, turn), frameTime);
        }
    }

    // ---- the ship at rest ----

    [Fact]
    public void StartsAtRestFacingForward()
    {
        Assert.Equal(0, _ship.Heading);
        Assert.Equal(Velocity.Stationary, _ship.Velocity);
    }

    [Fact]
    public void HandsOff_TheShipStaysWhereItIs()
    {
        Fly(thrust: 0, turn: 0, seconds: 10);

        Assert.Equal(Position.Origin, _player.Position);
    }

    // ---- thrust ----

    [Fact]
    public void FullThrust_MovesTheShipForward()
    {
        Fly(thrust: 1, turn: 0, seconds: 1);

        Assert.True(_player.Position.Y > 0, "the ship did not move forward");
        Assert.Equal(0, _player.Position.X, 10);
    }

    [Fact]
    public void FullThrust_AcceleratesRatherThanMovingAtAFixedRate()
    {
        // the second of flight covers more ground than the first, or this is not physics
        Fly(thrust: 1, turn: 0, seconds: 1);
        double first = _player.Position.Y;

        Fly(thrust: 1, turn: 0, seconds: 1);
        double second = _player.Position.Y - first;

        Assert.True(second > first, $"the ship did not accelerate: {first} then {second}");
    }

    [Fact]
    public void HalfThrust_MovesLessThanFullThrust()
    {
        Fly(thrust: 0.5, turn: 0, seconds: 1);
        double half = _player.Position.Y;

        Player other = new();
        new ShipMovement(Handling, HullRadius).Update(other, new ShipControls(1, 0), TimeSpan.FromSeconds(1));

        Assert.True(half < other.Position.Y, "half thrust did not fly slower than full thrust");
    }

    [Fact]
    public void SustainedThrust_SettlesAtTheShipsMaximumSpeed()
    {
        // Aether's damping is a per-step approximation of the exact decay the ship used to
        // integrate in closed form, not the exact answer — it converges towards
        // Handling.MaximumSpeed as the physics step shrinks, but a real step rate leaves a small,
        // permanent gap rather than the exact figure a formula would land on. Close is the
        // guarantee now; exact was only ever a property of the model this replaces.
        Fly(thrust: 1, turn: 0, seconds: 30, frames: 1500);

        Assert.True(_ship.Velocity.Speed <= Handling.MaximumSpeed + 1e-6,
            $"the ship exceeded its maximum of {Handling.MaximumSpeed}, reaching {_ship.Velocity.Speed}");
        Assert.True(_ship.Velocity.Speed >= Handling.MaximumSpeed * 0.99,
            $"the ship settled at {_ship.Velocity.Speed}, too far under its maximum of {Handling.MaximumSpeed}");
    }

    [Fact]
    public void NeverExceedsTheShipsMaximumSpeed()
    {
        // drag is what imposes the limit, so there is no separate clamp to get wrong
        for (int frame = 0; frame < 1800; frame++)
        {
            _ship.Update(_player, new ShipControls(1, 0), TimeSpan.FromMilliseconds(10));

            Assert.True(
                _ship.Velocity.Speed <= Handling.MaximumSpeed + 1e-9,
                $"the ship reached {_ship.Velocity.Speed}, over its maximum of {Handling.MaximumSpeed}");
        }
    }

    [Fact]
    public void ReverseThrust_MovesTheShipBackwards()
    {
        Fly(thrust: -1, turn: 0, seconds: 1);

        Assert.True(_player.Position.Y < 0, "the ship did not back up");
    }

    // ---- coasting ----

    [Fact]
    public void ReleasingThrust_KeepsTheShipMovingForAWhile()
    {
        Fly(thrust: 1, turn: 0, seconds: 2, frames: 100);
        double underPower = _player.Position.Y;

        Fly(thrust: 0, turn: 0, seconds: 0.5, frames: 25);

        Assert.True(_player.Position.Y > underPower, "the ship stopped dead when thrust was released");
    }

    [Fact]
    public void CoastingLongEnough_BringsTheShipToAStop()
    {
        Fly(thrust: 1, turn: 0, seconds: 2, frames: 100);

        Fly(thrust: 0, turn: 0, seconds: 20, frames: 1000);

        Assert.Equal(0, _ship.Velocity.Speed, 6);
    }

    // ---- the helm ----

    [Fact]
    public void FullStarboardHelm_TurnsAtTheShipsTurnRate()
    {
        Fly(thrust: 0, turn: 1, seconds: 0.5, frames: 25);

        Assert.Equal(Math.PI / 2, _ship.Heading, 10);
    }

    [Fact]
    public void FullPortHelm_TurnsTheOtherWay_AndReportsAHeadingInRange()
    {
        Fly(thrust: 0, turn: -1, seconds: 0.5, frames: 25);

        Assert.Equal(3 * Math.PI / 2, _ship.Heading, 10);
    }

    [Fact]
    public void TurningPastAFullCircle_WrapsTheHeading()
    {
        // three seconds at π radians a second is one and a half turns
        Fly(thrust: 0, turn: 1, seconds: 3, frames: 150);

        Assert.Equal(Math.PI, _ship.Heading, 10);
    }

    [Fact]
    public void TurningDoesNotMoveTheShip()
    {
        Fly(thrust: 0, turn: 1, seconds: 2, frames: 100);

        Assert.Equal(Position.Origin, _player.Position);
    }

    [Fact]
    public void ThrustIsAppliedAlongTheHeading()
    {
        Fly(thrust: 0, turn: 1, seconds: 0.5, frames: 25);      // a quarter turn to starboard

        Fly(thrust: 1, turn: 0, seconds: 1, frames: 50);

        Assert.True(_player.Position.X > 0, "the ship did not fly the way it was pointing");
        Assert.Equal(0, _player.Position.Y, 6);
    }

    [Fact]
    public void TheShipKeepsItsMomentumThroughATurn()
    {
        // most of what makes flying feel like flying: a turn points the ship somewhere new, it
        // does not teleport the velocity there
        Fly(thrust: 1, turn: 0, seconds: 1, frames: 50);

        Fly(thrust: 0, turn: 1, seconds: 1, frames: 50);        // turn to face the way it came

        Assert.Equal(Math.PI, _ship.Heading, 6);
        Assert.True(_ship.Velocity.Y > 0, "the ship lost its momentum in the turn");
    }

    // ---- frame time ----

    [Fact]
    public void CoversTheSameGroundWhateverTheFrameRate()
    {
        // the definition of done: a second of flight is a second of flight, however it is sliced.
        // The frame rates below all divide a second into whole ticks, so what is being compared is
        // the physics and not TimeSpan's rounding.
        //
        // The fixed-step accumulator gives every slicing the same number of Aether steps over one
        // second of game time, so the result is not merely close, it is the same computation run —
        // but the ship's body is re-synced from Player's position every Update call (see
        // ShipMovement's remarks on why), so a frame rate that calls Update more often also rounds
        // that many more times through Aether's float32 position before Player's double accumulates
        // the delta. 3 decimal places is comfortably past what a pilot could ever feel and still
        // catches a model that has stopped being frame-rate independent at all.
        Position OneSecondOfThrust(int frames)
        {
            Player player = new();
            ShipMovement ship = new(Handling, HullRadius);
            TimeSpan frameTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / frames);

            for (int frame = 0; frame < frames; frame++)
            {
                ship.Update(player, new ShipControls(1, 0), frameTime);
            }

            return player.Position;
        }

        Position whole = OneSecondOfThrust(1);

        Assert.Equal(whole.Y, OneSecondOfThrust(40).Y, 3);
        Assert.Equal(whole.Y, OneSecondOfThrust(80).Y, 3);
        Assert.Equal(whole.Y, OneSecondOfThrust(160).Y, 3);
    }

    [Fact]
    public void AZeroLengthFrame_ChangesNothing()
    {
        // a paused game keeps handing frames to the screen; the ship must hold still through them
        Fly(thrust: 1, turn: 0, seconds: 1, frames: 50);
        Position held = _player.Position;
        double heading = _ship.Heading;

        _ship.Update(_player, new ShipControls(1, 1), TimeSpan.Zero);

        Assert.Equal(held, _player.Position);
        Assert.Equal(heading, _ship.Heading);
    }

    [Fact]
    public void ANegativeFrame_ChangesNothing()
    {
        // time only ever moves forwards; rewinding the ship is not a thing the game asks for
        _ship.Update(_player, new ShipControls(1, 1), TimeSpan.FromSeconds(-1));

        Assert.Equal(Position.Origin, _player.Position);
        Assert.Equal(0, _ship.Heading);
    }

    // ---- resetting ----

    [Fact]
    public void Reset_BringsTheShipToRestFacingForward()
    {
        Fly(thrust: 1, turn: 1, seconds: 2, frames: 100);

        _ship.Reset();

        Assert.Equal(0, _ship.Heading);
        Assert.Equal(Velocity.Stationary, _ship.Velocity);
    }

    [Fact]
    public void Reset_LeavesThePlayerWhereTheyAre()
    {
        // resuming a save restores a position; the ship is only responsible for the momentum
        Fly(thrust: 1, turn: 0, seconds: 2, frames: 100);
        Position reached = _player.Position;

        _ship.Reset();

        Assert.Equal(reached, _player.Position);
    }

    // ---- obstacles ----

    [Fact]
    public void AnObstacleInTheWay_BlocksTheShip()
    {
        // the ship starts at the origin facing forward (+Y), so this sits squarely in its path
        _ship.AddObstacle(new Position(0, 50), width: 10, height: 10, rotation: 0);

        // unobstructed, five seconds at up to the ship's own top speed of 100 covers several
        // hundred units — comfortably enough to have flown straight through an obstacle at 50
        // if the hull did not actually collide with it
        Fly(thrust: 1, turn: 0, seconds: 5, frames: 5000);

        Assert.True(_player.Position.Y < 45,
            $"the ship passed the obstacle, reaching {_player.Position.Y}");
        Assert.True(_player.Position.Y > 30,
            $"the ship did not reach the obstacle at all, stopping at {_player.Position.Y}");
    }

    [Fact]
    public void AnObstacleNotInTheWay_DoesNotAffectTheShip()
    {
        // off to the side rather than ahead: the ship flying straight forward should never come
        // near it, so nothing here should read any differently to a ship with no obstacles at all
        _ship.AddObstacle(new Position(200, 25), width: 10, height: 10, rotation: 0);

        Fly(thrust: 1, turn: 0, seconds: 1);

        Assert.True(_player.Position.Y > 0, "the ship did not move forward");
        Assert.Equal(0, _player.Position.X, 6);
    }

    [Fact]
    public void ARotatedObstacle_BlocksAlongItsTurnedAxis_NotItsOriginalOne()
    {
        // Short across, long up and down, off to the side of the ship's path at x=20 — unrotated,
        // neither dimension reaches x=0, so the ship flying straight up it passes untouched.
        // Turned a quarter circle, the long axis swings across the path instead of along it, and
        // now it does. Same obstacle, same position; only the rotation says whether it is hit.
        //
        // Turned, the rectangle's near edge — its own width, 4, halved — sits 2 units below its
        // centre, so at y=48; a hull radius of 1 stops the ship with its centre at y=47.
        _ship.AddObstacle(new Position(20, 50), width: 4, height: 60, rotation: Math.PI / 2);

        Fly(thrust: 1, turn: 0, seconds: 5, frames: 5000);

        Assert.True(_player.Position.Y < 47.5,
            $"a rectangle turned across the flight path did not block it, reaching {_player.Position.Y}");
        Assert.True(_player.Position.Y > 30,
            $"the ship did not reach the obstacle at all, stopping at {_player.Position.Y}");
    }

    [Fact]
    public void AnUnrotatedObstacle_OffToOneSide_DoesNotBlockTheShip()
    {
        // the same obstacle as the turned one above, at rotation zero: its long axis runs
        // alongside the flight path rather than across it, so the ship never reaches it
        _ship.AddObstacle(new Position(20, 50), width: 4, height: 60, rotation: 0);

        Fly(thrust: 1, turn: 0, seconds: 1);

        Assert.True(_player.Position.Y > 0, "the ship did not move forward");
        Assert.Equal(0, _player.Position.X, 6);
    }

    [Fact]
    public void AnIdlePlayerInsideAnObstacle_IsNotPushedOut()
    {
        // Player.Position can be set by something other than flying — a save being resumed, a
        // test moving it by hand — and this is what that looks like when it happens to land
        // inside an obstacle. An idle frame must not go looking for the nearest way to correct
        // that on the player's behalf; the ship was never asked to fly, so it does not move them.
        _ship.AddObstacle(Position.Origin, width: 20, height: 20, rotation: 0);

        _ship.Update(_player, ShipControls.Neutral, TimeSpan.FromSeconds(1));

        Assert.Equal(Position.Origin, _player.Position);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddObstacle_RejectsANonPositiveWidthOrHeight(double size)
    {
        // an obstacle with no size blocks nothing, which is a silent no-op content almost
        // certainly did not intend when it asked for one
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ship.AddObstacle(Position.Origin, width: size, height: 10, rotation: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ship.AddObstacle(Position.Origin, width: 10, height: size, rotation: 0));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void AddObstacle_RejectsAWidthHeightOrRotationThatIsNotAFiniteNumber(double value)
    {
        // an infinite size blocks everywhere rather than just where it was placed, and a rotation
        // that is not a number places every corner nowhere, in full, the same trap a heading falls
        // into for the same reason
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ship.AddObstacle(Position.Origin, width: value, height: 10, rotation: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ship.AddObstacle(Position.Origin, width: 10, height: value, rotation: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ship.AddObstacle(Position.Origin, width: 10, height: 10, rotation: value));
    }

    // ---- handling that cannot be flown ----

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void RejectsAShipThatCannotAccelerate(double acceleration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipMovement(Handling with { Acceleration = acceleration }, HullRadius));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsAShipWithNoDrag(double drag)
    {
        // without drag there is no top speed, and the ship accelerates until the numbers give out
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipMovement(Handling with { Drag = drag }, HullRadius));
    }

    [Fact]
    public void RejectsANegativeTurnRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipMovement(Handling with { TurnRate = -1 }, HullRadius));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void RejectsAnAccelerationThatIsNotAFiniteNumber(double acceleration)
    {
        // infinite acceleration gives an infinite top speed: the ship leaves the world within a
        // frame or two, and the position it leaves behind names neither the ship nor the frame
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipMovement(Handling with { Acceleration = acceleration }, HullRadius));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void RejectsADragThatIsNotAFiniteNumber(double drag)
    {
        // infinite drag is the opposite failure to no drag at all: a top speed of zero, so full
        // thrust moves the ship nowhere and the controls read as broken rather than as content
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipMovement(Handling with { Drag = drag }, HullRadius));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void RejectsATurnRateThatIsNotAFiniteNumber(double turnRate)
    {
        // an infinite turn rate wraps the heading to whatever the modulus of infinity leaves, and
        // NaN spreads from the heading into every position the ship reports afterwards
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipMovement(Handling with { TurnRate = turnRate }, HullRadius));
    }

    [Fact]
    public void NamesTheHandlingValueItRefused()
    {
        // the ship is built from content: an exception that says only "handling" leaves whoever
        // authored the profile reading three numbers to find out which one it meant
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipMovement(Handling with { Drag = double.PositiveInfinity }, HullRadius));

        Assert.Equal("handling.Drag", refused.ParamName);
    }

    [Fact]
    public void RejectsAFrameWithNoPlayerToMove()
    {
        Assert.Throws<ArgumentNullException>(
            () => _ship.Update(null!, ShipControls.Neutral, TimeSpan.FromSeconds(1)));
    }
}
