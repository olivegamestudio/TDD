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

    [Theory]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(double.NegativeInfinity, -1)]
    public void AnInfiniteAxis_ReadsAsFullDeflection(double asked, double expected)
    {
        Assert.Equal(expected, new ShipControls(thrust: asked, turn: asked).Thrust);
        Assert.Equal(expected, new ShipControls(thrust: asked, turn: asked).Turn);
    }

    [Fact]
    public void ANaNAxis_ReadsAsHandsOff()
    {
        // Math.Clamp(NaN, -1, 1) is NaN — the clamp does not hold for it, so a device reporting a
        // NaN axis would put NaN into the heading and never get it back out again. A pilot who
        // cannot be read is a pilot asking for nothing.
        ShipControls controls = new(thrust: double.NaN, turn: double.NaN);

        Assert.Equal(0, controls.Thrust);
        Assert.Equal(0, controls.Turn);
    }

    [Fact]
    public void TheDefaultControls_AreHandsOff()
    {
        // a struct's zero value skips the constructor entirely, so the clamp never runs on it. That
        // makes the default the one reading no validation can protect — it has to be safe by
        // construction. It is what an uninitialised field, an unfilled array element or a
        // default(ShipControls) in a host that has not read its device yet all deliver.
        ShipControls unset = default;

        Assert.Equal(0, unset.Thrust);
        Assert.Equal(0, unset.Turn);
        Assert.Equal(ShipControls.Neutral, unset);
    }

    // ---- asking for nothing ----

    [Fact]
    public void Neutral_IsNeutral()
    {
        Assert.True(ShipControls.Neutral.IsNeutral);
    }

    [Fact]
    public void TheDefaultControls_AreNeutral()
    {
        // the zero value skips the constructor, so IsNeutral has to be right about it too — it is
        // what a device that has not been read yet reports
        Assert.True(default(ShipControls).IsNeutral);
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

    [Fact]
    public void ADeviceThatCannotBeRead_IsNeutral()
    {
        // IsNeutral compares exactly, and NaN == 0 is false, so a device reporting NaN would
        // otherwise claim to be the one being used and shut out the device that works. The
        // constructor is what makes that unreachable: it reads an unreadable axis as 0 before
        // IsNeutral ever sees it. Pinned here because that is the only thing holding the exact
        // comparison up.
        Assert.True(new ShipControls(double.NaN, double.NaN).IsNeutral);
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

    [Fact]
    public void FromStick_ANaNAxis_ReadsAsHandsOff_RatherThanThrowing()
    {
        // Math.Sign(double) throws on NaN rather than answering 0, and the dead zone test above it
        // does not catch one: NaN <= deadZone is false, so an unreadable axis falls straight into
        // it. The pad is read with GamePadDeadZone.None, so raw driver floats arrive here with
        // nothing in between — an ArithmeticException on the frame path takes the game down
        // mid-flight. An axis that cannot be read is an axis asking for nothing.
        ShipControls controls = ShipControls.FromStick(thrust: double.NaN, turn: 0, deadZone: 0.2);

        Assert.Equal(0, controls.Thrust);
        Assert.Equal(0, controls.Turn);
    }

    [Fact]
    public void FromStick_ANaNDeadZone_ReadsBothAxesAsHandsOff()
    {
        // a dead zone that cannot be read says nothing about how far the stick has to move, so
        // there is no answer to give about either axis except that nobody is asking for anything
        Assert.True(ShipControls.FromStick(thrust: 1, turn: -1, deadZone: double.NaN).IsNeutral);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(double.NegativeInfinity, -1)]
    public void FromStick_AnInfiniteAxis_ReadsAsFullDeflection(double asked, double expected)
    {
        // unlike NaN this has a direction, and the constructor's clamp already has an answer for
        // it; it stays a full ask rather than becoming hands off
        Assert.Equal(expected, ShipControls.FromStick(asked, asked, deadZone: 0.2).Thrust);
    }
}
