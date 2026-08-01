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

    readonly Player _player = new();

    readonly ShipMovement _ship = new(Handling);

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
        new ShipMovement(Handling).Update(other, new ShipControls(1, 0), TimeSpan.FromSeconds(1));

        Assert.True(half < other.Position.Y, "half thrust did not fly slower than full thrust");
    }

    [Fact]
    public void SustainedThrust_SettlesAtTheShipsMaximumSpeed()
    {
        Fly(thrust: 1, turn: 0, seconds: 30, frames: 1500);

        Assert.Equal(Handling.MaximumSpeed, _ship.Velocity.Speed, 6);
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
        Position OneSecondOfThrust(int frames)
        {
            Player player = new();
            ShipMovement ship = new(Handling);
            TimeSpan frameTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / frames);

            for (int frame = 0; frame < frames; frame++)
            {
                ship.Update(player, new ShipControls(1, 0), frameTime);
            }

            return player.Position;
        }

        Position whole = OneSecondOfThrust(1);

        Assert.Equal(whole.Y, OneSecondOfThrust(40).Y, 6);
        Assert.Equal(whole.Y, OneSecondOfThrust(80).Y, 6);
        Assert.Equal(whole.Y, OneSecondOfThrust(160).Y, 6);
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

    // ---- handling that cannot be flown ----

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void RejectsAShipThatCannotAccelerate(double acceleration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipMovement(Handling with { Acceleration = acceleration }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsAShipWithNoDrag(double drag)
    {
        // without drag there is no top speed, and the ship accelerates until the numbers give out
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipMovement(Handling with { Drag = drag }));
    }

    [Fact]
    public void RejectsANegativeTurnRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipMovement(Handling with { TurnRate = -1 }));
    }

    [Fact]
    public void RejectsAFrameWithNoPlayerToMove()
    {
        Assert.Throws<ArgumentNullException>(
            () => _ship.Update(null!, ShipControls.Neutral, TimeSpan.FromSeconds(1)));
    }

    // ---- a device that reports nonsense ----

    [Fact]
    public void ANaNFrame_DoesNotPoisonTheShip()
    {
        // NaN propagates through every arithmetic path here, so one bad frame from a device would
        // otherwise put NaN in the heading, then the velocity, then the position — and nothing
        // recovers it. The ship is then silently unflyable: no trigger ever fires again, because
        // every distance measured against a NaN position is NaN, and the save carries it.
        _ship.Update(_player, new ShipControls(double.NaN, double.NaN), TimeSpan.FromMilliseconds(16));

        _ship.Update(_player, new ShipControls(thrust: 1, turn: 0), TimeSpan.FromMilliseconds(16));

        Assert.False(double.IsNaN(_player.Position.X), "position X is NaN");
        Assert.False(double.IsNaN(_player.Position.Y), "position Y is NaN");
        Assert.False(double.IsNaN(_ship.Heading), "heading is NaN");
        Assert.True(_player.Position.Y > 0, "the good frame after the bad one did not move the ship");
    }

    [Fact]
    public void AnInfiniteFrame_FliesAtFullDeflectionRatherThanBreakingTheShip()
    {
        _ship.Update(
            _player,
            new ShipControls(double.PositiveInfinity, double.NegativeInfinity),
            TimeSpan.FromSeconds(1));

        Assert.True(double.IsFinite(_player.Position.X));
        Assert.True(double.IsFinite(_player.Position.Y));
        Assert.InRange(_ship.Velocity.Speed, 0, Handling.MaximumSpeed);
    }
}
