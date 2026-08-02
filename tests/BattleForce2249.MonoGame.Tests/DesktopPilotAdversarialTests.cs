using OliveGameStudio;

namespace BattleForce2249.MonoGame.Tests;

/// <summary>
/// The corner where the two halves of the binding meet: the gamepad's <em>shipped</em> dead zone
/// and the gamepad-first arbitration.
/// </summary>
/// <remarks>
/// <para>
/// Each half is covered on its own — <c>ShipControls.FromStick</c> against a dead zone the test
/// chooses, and <see cref="FirstActiveShipInput"/> against devices whose answers the test writes
/// out. Neither pins the claim the composition root actually rests on, which
/// <c>AddDesktopPilot</c> states as "a resting pad is zeroed by its dead zone, so asking it first
/// costs the keyboard nothing".
/// </para>
/// <para>
/// That claim is the load-bearing one, because if it stops holding the failure is not a wrong
/// number: a player with a worn controller plugged in finds the keyboard dead, with the pad
/// winning every frame on drift nobody's hand is causing. So these drive the real
/// <see cref="GamePadShipInput.DeadZone"/> through the real mapping and the real arbitration, and
/// only the static <c>GamePad.GetState</c> call is stood in for.
/// </para>
/// </remarks>
public sealed class DesktopPilotAdversarialTests
{
    /// <summary>A device whose answer the test writes out, standing in for a real one.</summary>
    sealed class Device(ShipControls controls) : IShipInput
    {
        public ShipControls Read() => controls;
    }

    /// <summary>What <see cref="GamePadShipInput"/> would report for a stick pushed here.</summary>
    static Device Pad(double thrust, double turn) =>
        new(ShipControls.FromStick(thrust, turn, GamePadShipInput.DeadZone));

    /// <summary>A keyboard with full ahead held, and nothing else.</summary>
    static Device KeyboardFullAhead => new(ShipControls.FromKeys(
        ahead: true, astern: false, port: false, starboard: false));

    /// <summary>The pilot composed the way <c>AddDesktopPilot</c> composes it: pad, then keys.</summary>
    static FirstActiveShipInput Pilot(IShipInput pad, IShipInput keyboard) => new(pad, keyboard);

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(-0.19)]
    [InlineData(GamePadShipInput.DeadZone)]           // exactly on the boundary, which is inside it
    [InlineData(-GamePadShipInput.DeadZone)]
    public void AWornPadRestingInsideTheShippedDeadZone_DoesNotShutOutTheKeyboard(double drift)
    {
        // the whole reason the pad may be asked first. If the dead zone ever stops covering the
        // rest position, a plugged-in worn controller silently kills the keyboard and the player
        // has no way to tell why the ship will not answer them
        FirstActiveShipInput pilot = Pilot(Pad(drift, drift), KeyboardFullAhead);

        Assert.Equal(1, pilot.Read().Thrust);
    }

    [Fact]
    public void AWornPadRestingDiagonally_DoesNotShutOutTheKeyboard()
    {
        // the dead zone is applied per axis, not to the stick's distance from centre. A stick
        // resting on the boundary in both axes is 0.28 from centre, so a radial dead zone of 0.2
        // would let it through — this pins the axial reading as the deliberate one, because it is
        // the reading that keeps a resting stick silent
        FirstActiveShipInput pilot = Pilot(
            Pad(GamePadShipInput.DeadZone, GamePadShipInput.DeadZone),
            KeyboardFullAhead);

        Assert.Equal(1, pilot.Read().Thrust);
    }

    [Fact]
    public void APadPushedPastTheDeadZone_TakesTheControlsFromTheKeyboard()
    {
        // the other direction, so the tests above cannot pass by the pad being ignored outright.
        // 0.6 is half the travel past a 0.2 dead zone, so the pad answers for both axes
        FirstActiveShipInput pilot = Pilot(Pad(0.6, 0), KeyboardFullAhead);

        ShipControls controls = pilot.Read();

        Assert.Equal(0.5, controls.Thrust, 6);
        Assert.Equal(0, controls.Turn);
    }

    [Fact]
    public void APadThatCannotBeRead_DoesNotShutOutTheKeyboard()
    {
        // through the real FromStick rather than by handing FirstActiveShipInput a NaN directly,
        // because the pad is read with GamePadDeadZone.None and a raw driver float is the way a
        // NaN actually arrives. It must neither throw on the frame path nor win the arbitration
        FirstActiveShipInput pilot = Pilot(Pad(double.NaN, double.NaN), KeyboardFullAhead);

        Assert.Equal(1, pilot.Read().Thrust);
    }

    [Fact]
    public void APinnedPadReportingAnInfiniteAxis_StillAnswersForBothAxes()
    {
        // unlike NaN an infinity has a direction, so it stays a full ask rather than becoming
        // hands off. A pad that is genuinely hard over should beat a held key, not defer to it
        FirstActiveShipInput pilot = Pilot(Pad(double.NegativeInfinity, 0), KeyboardFullAhead);

        Assert.Equal(-1, pilot.Read().Thrust);
    }

    [Fact]
    public void TheShippedDeadZoneLeavesTheStickSomewhereToGo()
    {
        // a dead zone of 1 or more is read as unusable and zeroes the pad outright, so the game
        // would ship unflyable on a controller while every arbitration test above still passed
        Assert.InRange(GamePadShipInput.DeadZone, 0, 0.5);
        Assert.Equal(1, ShipControls.FromStick(1, 0, GamePadShipInput.DeadZone).Thrust, 6);
    }
}
