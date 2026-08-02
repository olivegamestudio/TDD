namespace OliveGameStudio.World.Tests;

/// <summary>
/// QA's adversarial pass over the two device mappings. These do not cover what a stick or a key
/// is <em>meant</em> to do — <see cref="ShipControlsTests"/> already does — they cover what the
/// mappings do when handed a number nobody intended them to be handed. A gamepad is read with the
/// platform's own dead zone off, so raw driver floats reach <see cref="ShipControls.FromStick"/>
/// with nothing in between, and this runs on the frame path where an exception or a
/// <c>NaN</c> is a game that stops rather than a value that is wrong.
/// </summary>
public sealed class ShipControlsAdversarialTests
{
    /// <summary>
    /// Every hostile axis value QA could think of, each of which must produce a flyable answer
    /// rather than a throw or a number the physics cannot recover from.
    /// </summary>
    public static TheoryData<double> HostileAxes =>
    [
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
        double.MaxValue,
        double.MinValue,
        double.Epsilon,
        -double.Epsilon,
        0,
        -0.0,
        1,
        -1,
        1.0000000000000002,
        -1.0000000000000002,
        1e308,
        -1e308,
    ];

    /// <summary>
    /// Every hostile dead zone. The shipping one is a const 0.2, but <see cref="ShipControls"/> is
    /// engine surface and the dead zone is a parameter, so a second host is free to pass any of
    /// these.
    /// </summary>
    public static TheoryData<double> HostileDeadZones =>
    [
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
        0,
        -0.0,
        1,
        -1,
        0.5,
        0.9999999999999999,
        double.Epsilon,
        2,
        -0.5,
    ];

    public static TheoryData<double, double> EveryHostilePair()
    {
        TheoryData<double, double> pairs = [];
        foreach (double axis in new[]
        {
            double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue,
            double.MinValue, 0, -0.0, 1, -1, 0.5, -0.5, double.Epsilon,
        })
        {
            foreach (double deadZone in new[]
            {
                double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -0.0, 1, -1,
                0.2, 0.9999999999999999, 2, -0.5,
            })
            {
                pairs.Add(axis, deadZone);
            }
        }

        return pairs;
    }

    // ---- the invariant that matters most: nothing reaches the physics that it cannot recover from

    [Theory]
    [MemberData(nameof(EveryHostilePair))]
    public void FromStick_WhateverItIsHanded_ProducesAFlyableAsk(double axis, double deadZone)
    {
        // the frame path: a throw here is the game dying mid-flight, and a NaN here is a heading,
        // then a velocity, then a position that never recovers
        ShipControls controls = ShipControls.FromStick(axis, axis, deadZone);

        Assert.False(double.IsNaN(controls.Thrust), $"NaN thrust from ({axis}, {deadZone})");
        Assert.False(double.IsNaN(controls.Turn), $"NaN turn from ({axis}, {deadZone})");
        Assert.InRange(controls.Thrust, -1, 1);
        Assert.InRange(controls.Turn, -1, 1);
    }

    [Theory]
    [MemberData(nameof(HostileAxes))]
    public void Constructed_WhateverItIsHanded_ProducesAFlyableAsk(double axis)
    {
        ShipControls controls = new(axis, axis);

        Assert.False(double.IsNaN(controls.Thrust));
        Assert.False(double.IsNaN(controls.Turn));
        Assert.InRange(controls.Thrust, -1, 1);
        Assert.InRange(controls.Turn, -1, 1);
    }

    [Theory]
    [MemberData(nameof(HostileAxes))]
    public void FromStick_TheAxesAreIndependent_SoOneUnreadableAxisDoesNotTakeTheOther(double hostile)
    {
        // a driver reporting one bad axis must not cost the player the axis that still works,
        // unless the dead zone itself is what is unreadable
        ShipControls controls = ShipControls.FromStick(thrust: hostile, turn: 1, deadZone: 0.2);

        Assert.Equal(1, controls.Turn);
    }

    // ---- the dead zone's own boundary

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(0.001)]
    public void FromStick_AtExactlyTheDeadZone_TheStickIsStillResting(double deadZone)
    {
        // the boundary is inclusive on the resting side, in both directions, or a stick parked
        // exactly on the edge flies the ship
        Assert.Equal(0, ShipControls.FromStick(deadZone, deadZone, deadZone).Thrust);
        Assert.Equal(0, ShipControls.FromStick(-deadZone, -deadZone, deadZone).Thrust);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void FromStick_CrossingTheDeadZone_DoesNotJump(double deadZone)
    {
        // the stated reason the remaining travel is rescaled: crossing the edge must ask for a
        // little, not snap to the dead zone's width
        double justInside = ShipControls.FromStick(deadZone, 0, deadZone).Thrust;
        double justOutside = ShipControls
            .FromStick(Math.BitIncrement(Math.BitIncrement(deadZone)), 0, deadZone).Thrust;

        Assert.Equal(0, justInside);
        Assert.True(justOutside < 0.0001, $"crossing the dead zone jumped straight to {justOutside}");
        Assert.True(justOutside >= 0, $"crossing the dead zone asked for {justOutside}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void FromStick_AtItsStop_ReachesExactlyFullDeflection(double deadZone)
    {
        // whatever the dead zone costs in the middle, the stick must still reach its rating at the
        // stop, or the player can never ask for full thrust
        Assert.Equal(1, ShipControls.FromStick(1, 1, deadZone).Thrust);
        Assert.Equal(-1, ShipControls.FromStick(-1, -1, deadZone).Thrust);
    }

    // ---- shape of the response curve

    [Theory]
    [InlineData(0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void FromStick_PushingTheStickFurther_NeverAsksForLess(double deadZone)
    {
        // a curve that dipped anywhere would read as the ship fighting the player
        double previous = -1;
        for (int step = 0; step <= 1000; step++)
        {
            double asked = ShipControls.FromStick(step / 1000.0, 0, deadZone).Thrust;

            Assert.True(asked >= previous, $"asking for {asked} at {step / 1000.0} after {previous}");
            previous = asked;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void FromStick_AnswersTheSameEitherSideOfCentre(double deadZone)
    {
        // port and starboard must cost the same, or the ship pulls to one side on a straight stick
        for (int step = 0; step <= 100; step++)
        {
            double pushed = step / 100.0;

            Assert.Equal(
                ShipControls.FromStick(pushed, 0, deadZone).Thrust,
                -ShipControls.FromStick(-pushed, 0, deadZone).Thrust,
                precision: 12);
        }
    }

    // ---- the keys

    [Fact]
    public void FromKeys_EveryCombinationOfTheFourKeys_IsFlyable()
    {
        for (int combination = 0; combination < 16; combination++)
        {
            ShipControls controls = ShipControls.FromKeys(
                ahead: (combination & 1) != 0,
                astern: (combination & 2) != 0,
                port: (combination & 4) != 0,
                starboard: (combination & 8) != 0);

            Assert.False(double.IsNaN(controls.Thrust), $"combination {combination}");
            Assert.InRange(controls.Thrust, -1, 1);
            Assert.InRange(controls.Turn, -1, 1);
        }
    }

    [Fact]
    public void FromKeys_AllFourKeysHeld_AsksForNothing()
    {
        // a hand flat on the keyboard cancels on both axes rather than picking a direction, and
        // therefore reports itself as not the device in use
        ShipControls controls = ShipControls.FromKeys(true, true, true, true);

        Assert.True(controls.IsNeutral);
    }

    [Fact]
    public void FromKeys_HoldingBothOnOneAxis_LeavesTheOtherAxisAlone()
    {
        // rolling a hand across W and S must not cost the player the helm
        ShipControls controls = ShipControls.FromKeys(
            ahead: true, astern: true, port: false, starboard: true);

        Assert.Equal(0, controls.Thrust);
        Assert.Equal(1, controls.Turn);
    }

    [Fact]
    public void FromKeys_AndFromStickAtItsStop_AskForTheSameThing()
    {
        // the physics never learns which device the pilot used, so full ahead has to mean the same
        // number whichever hand asked for it
        Assert.Equal(
            ShipControls.FromKeys(ahead: true, astern: false, port: false, starboard: false),
            ShipControls.FromStick(thrust: 1, turn: 0, deadZone: 0.2));
    }

    // ---- neutrality, which is what decides arbitration

    [Theory]
    [MemberData(nameof(HostileAxes))]
    public void AnUnreadableOrOverRangeAxis_NeverClaimsToBeTheDeviceInUse_WhenItAsksForNothing(double axis)
    {
        // IsNeutral is an exact comparison; -0.0, NaN and the epsilons are where an exact
        // comparison usually goes wrong
        ShipControls controls = new(axis, axis);

        Assert.Equal(controls.Thrust == 0 && controls.Turn == 0, controls.IsNeutral);
    }

    [Fact]
    public void NegativeZero_IsHandsOff_NotAnAsk()
    {
        ShipControls controls = new(-0.0, -0.0);

        Assert.True(controls.IsNeutral);
    }

    [Fact]
    public void ARestingStick_ReadThroughTheShippingDeadZone_IsHandsOff()
    {
        // the whole reason the dead zone exists: a worn pad must not fly the ship, and must not
        // claim the arbitration away from the keyboard either
        Assert.True(ShipControls.FromStick(0.19, -0.19, 0.2).IsNeutral);
    }
}
