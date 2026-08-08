namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers what a stick position means to the ship. Unlike a key, a stick is never quite at rest,
/// so the dead zone and what happens at its edge is the whole of this: absorb the drift, but do
/// not lose the top of the range or step over the boundary.
/// </summary>
public sealed class ShipControlsFromStickTests
{
    const double DeadZone = 0.2;

    [Fact]
    public void AStickAtRest_IsHandsOff()
    {
        ShipControls controls = ShipControls.FromStick(thrust: 0, turn: 0, DeadZone);

        Assert.True(controls.IsNeutral);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(-0.05)]
    [InlineData(0.19)]
    [InlineData(0.2)]
    [InlineData(-0.2)]
    public void AStickInsideItsDeadZone_DoesNotFlyTheShip(double drift)
    {
        // a worn stick rests slightly off centre; if that flew the ship the player would find
        // the game flying itself, and — worse — the keyboard shut out behind it
        ShipControls controls = ShipControls.FromStick(thrust: drift, turn: drift, DeadZone);

        Assert.True(controls.IsNeutral);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    public void AStickAtItsStop_AsksForTheFullRange(double pushed, double expected)
    {
        // the dead zone costs travel at the bottom, not at the top: a player who can never quite
        // ask for full thrust would be a subtle and infuriating bug
        ShipControls controls = ShipControls.FromStick(thrust: pushed, turn: pushed, DeadZone);

        Assert.Equal(expected, controls.Thrust);
        Assert.Equal(expected, controls.Turn);
    }

    [Fact]
    public void AStickAtItsStop_AsksForWhatTheKeyDoes()
    {
        // the physics never learns which device the pilot used, so the two had better agree
        Assert.Equal(
            ShipControls.FromKeys(ahead: true, astern: false, port: false, starboard: true),
            ShipControls.FromStick(thrust: 1, turn: 1, DeadZone));
    }

    [Fact]
    public void CrossingTheDeadZone_AsksForALittle_RatherThanJumping()
    {
        // the remaining travel is stretched back over the full range, so the first thing the
        // player feels past the edge is a nudge and not half thrust
        ShipControls justPast = ShipControls.FromStick(thrust: 0.2001, turn: 0, DeadZone);

        Assert.InRange(justPast.Thrust, 0, 0.01);
        Assert.False(justPast.IsNeutral);
    }

    [Fact]
    public void TheResponseIsMonotonic_AcrossTheWholeRange()
    {
        double previous = -1;

        for (int step = 0; step <= 1000; step++)
        {
            double thrust = ShipControls.FromStick(thrust: step / 500.0 - 1, turn: 0, DeadZone).Thrust;

            Assert.True(thrust >= previous, $"pushing further asked for less at {step}");
            previous = thrust;
        }
    }

    [Fact]
    public void TheResponseIsSymmetric_AboutTheCentre()
    {
        // a stick that answered differently to port and to starboard would pull the ship to one
        // side the whole time the player held it straight
        for (int step = 1; step <= 100; step++)
        {
            double pushed = step / 100.0;

            Assert.Equal(
                ShipControls.FromStick(thrust: pushed, turn: 0, DeadZone).Thrust,
                -ShipControls.FromStick(thrust: -pushed, turn: 0, DeadZone).Thrust,
                precision: 12);
        }
    }

    [Fact]
    public void ANaNAxis_IsHandsOff_RatherThanThrowing()
    {
        // Math.Sign throws on NaN rather than returning 0, and an ordered comparison against NaN
        // is false either way round, so the dead zone test above does not catch one. A driver
        // reporting an axis it could not read must read as nobody flying, not as a dead game.
        ShipControls controls = ShipControls.FromStick(thrust: double.NaN, turn: double.NaN, DeadZone);

        Assert.True(controls.IsNeutral);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1)]
    [InlineData(2)]
    public void ADeadZoneThatLeavesNoTravel_IsHandsOff(double deadZone)
    {
        // a stick that can never be pushed far enough to count is one nobody can fly with, and
        // the arithmetic that would otherwise run here divides by nothing
        ShipControls controls = ShipControls.FromStick(thrust: 1, turn: 1, deadZone);

        Assert.True(controls.IsNeutral);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(-1)]
    [InlineData(double.NegativeInfinity)]
    public void ANegativeDeadZone_DoesNotAmplifyTheStick(double deadZone)
    {
        // left unguarded a negative dead zone inverts the feature into a sensitivity boost: the
        // drift it exists to absorb is exactly what would fly the ship
        ShipControls controls = ShipControls.FromStick(thrust: 0.01, turn: 0.01, deadZone);

        Assert.InRange(controls.Thrust, 0, 0.01);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(double.NegativeInfinity, -1)]
    [InlineData(double.MaxValue, 1)]
    public void AnAxisBeyondItsRange_ReadsAsFullDeflection(double axis, double expected)
    {
        Assert.Equal(expected, ShipControls.FromStick(thrust: axis, turn: 0, DeadZone).Thrust);
    }

    [Fact]
    public void NoStrafeGiven_DefaultsToHandsOff()
    {
        // every call site written before a second stick existed still reads the same two axes it
        // always did
        ShipControls controls = ShipControls.FromStick(thrust: 0, turn: 0, DeadZone);

        Assert.Equal(0, controls.Strafe);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    public void AStrafeStickAtItsStop_AsksForTheFullRange(double pushed, double expected)
    {
        ShipControls controls = ShipControls.FromStick(thrust: 0, turn: 0, DeadZone, strafe: pushed);

        Assert.Equal(expected, controls.Strafe);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(-0.19)]
    public void AStrafeStickInsideItsDeadZone_DoesNotStrafeTheShip(double drift)
    {
        // the same dead zone a worn thrust or turn stick gets — a second stick is still a stick
        ShipControls controls = ShipControls.FromStick(thrust: 0, turn: 0, DeadZone, strafe: drift);

        Assert.Equal(0, controls.Strafe);
        Assert.True(controls.IsNeutral);
    }

    [Fact]
    public void StrafeIsIndependent_OfThrustAndTurn()
    {
        ShipControls controls = ShipControls.FromStick(thrust: 1, turn: -1, DeadZone, strafe: 1);

        Assert.Equal(1, controls.Thrust);
        Assert.Equal(-1, controls.Turn);
        Assert.Equal(1, controls.Strafe);
    }
}
