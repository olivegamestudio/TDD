namespace OliveGameStudio.World.Tests;

public sealed class ShipControlsTests
{
    [Fact]
    public void Neutral_AsksForNothing()
    {
        Assert.Equal(0, ShipControls.Neutral.Thrust);
        Assert.Equal(0, ShipControls.Neutral.Turn);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    [InlineData(0.4, 0.4)]
    [InlineData(2, 1)]
    [InlineData(-7.5, -1)]
    public void Thrust_IsClampedToWhatTheShipIsRatedFor(double asked, double expected)
    {
        // a device that reports its axes in some other range must not out-fly the ship
        Assert.Equal(expected, new ShipControls(thrust: asked, turn: 0).Thrust);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    [InlineData(0.25, 0.25)]
    [InlineData(3, 1)]
    [InlineData(-3, -1)]
    public void Turn_IsClampedToWhatTheShipIsRatedFor(double asked, double expected)
    {
        Assert.Equal(expected, new ShipControls(thrust: 0, turn: asked).Turn);
    }

    [Fact]
    public void TwoSetsOfControlsAskingForTheSameThingAreEqual()
    {
        Assert.Equal(new ShipControls(1, -1), new ShipControls(4, -9));
    }

    // ---- asking for nothing ----

    [Fact]
    public void Neutral_IsNeutral()
    {
        Assert.True(ShipControls.Neutral.IsNeutral);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-0.01, 0)]
    [InlineData(0, -0.01)]
    public void AnyAskAtAll_IsNotNeutral(double thrust, double turn)
    {
        // this is how a device says "I am the one being used", so the smallest ask has to count
        Assert.False(new ShipControls(thrust, turn).IsNeutral);
    }

    // ---- keys ----

    [Fact]
    public void FromKeys_NothingHeld_AsksForNothing()
    {
        Assert.True(ShipControls.FromKeys(false, false, false, false).IsNeutral);
    }

    [Fact]
    public void FromKeys_AHeldKeyIsAFullAsk()
    {
        // a key is all or nothing; there is no half-pressing one
        ShipControls controls = ShipControls.FromKeys(ahead: true, astern: false, port: false, starboard: true);

        Assert.Equal(1, controls.Thrust);
        Assert.Equal(1, controls.Turn);
    }

    [Fact]
    public void FromKeys_AsternAndPortAreTheNegativeDirections()
    {
        ShipControls controls = ShipControls.FromKeys(ahead: false, astern: true, port: true, starboard: false);

        Assert.Equal(-1, controls.Thrust);
        Assert.Equal(-1, controls.Turn);
    }

    [Fact]
    public void FromKeys_OppositeKeysHeldTogetherCancel()
    {
        // what the keys literally say, rather than a direction decided by which was checked first
        ShipControls controls = ShipControls.FromKeys(ahead: true, astern: true, port: true, starboard: true);

        Assert.True(controls.IsNeutral);
    }

    // ---- sticks ----

    [Fact]
    public void FromStick_ARestingStickAsksForNothing()
    {
        Assert.True(ShipControls.FromStick(thrust: 0, turn: 0, deadZone: 0.2).IsNeutral);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.2)]
    [InlineData(-0.19)]
    public void FromStick_AWornStickInsideTheDeadZoneDoesNotFlyTheShip(double drift)
    {
        // a stick that rests slightly off centre must not fly the ship on its own
        Assert.True(ShipControls.FromStick(drift, drift, deadZone: 0.2).IsNeutral);
    }

    [Fact]
    public void FromStick_AtItsStop_AsksForEverything()
    {
        ShipControls controls = ShipControls.FromStick(thrust: 1, turn: -1, deadZone: 0.2);

        Assert.Equal(1, controls.Thrust, 6);
        Assert.Equal(-1, controls.Turn, 6);
    }

    [Fact]
    public void FromStick_JustPastTheDeadZone_AsksForALittle()
    {
        // the travel left past the dead zone is stretched back over the full range, so crossing it
        // does not jump the ship straight to the dead zone's worth of thrust
        ShipControls controls = ShipControls.FromStick(thrust: 0.21, turn: 0, deadZone: 0.2);

        Assert.InRange(controls.Thrust, 0, 0.05);
        Assert.True(controls.Thrust > 0, "crossing the dead zone should ask for something");
    }

    [Fact]
    public void FromStick_HalfWayPastTheDeadZone_AsksForHalf()
    {
        Assert.Equal(0.5, ShipControls.FromStick(thrust: 0.6, turn: 0, deadZone: 0.2).Thrust, 6);
    }

    [Fact]
    public void FromStick_KeepsTheDirectionItWasPushed()
    {
        ShipControls controls = ShipControls.FromStick(thrust: -0.6, turn: 0.6, deadZone: 0.2);

        Assert.Equal(-0.5, controls.Thrust, 6);
        Assert.Equal(0.5, controls.Turn, 6);
    }

    [Fact]
    public void FromStick_WithNoDeadZone_PassesTheStickStraightThrough()
    {
        Assert.Equal(0.35, ShipControls.FromStick(thrust: 0.35, turn: 0, deadZone: 0).Thrust, 6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    public void FromStick_ADeadZoneWithNoTravelLeftIsUnusableRatherThanInfinite(double deadZone)
    {
        // dividing by the remaining travel would fly the ship on an infinity
        Assert.True(ShipControls.FromStick(thrust: 1, turn: 1, deadZone).IsNeutral);
    }
}
