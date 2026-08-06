using OliveGameStudio;

namespace BattleForce2249.MonoGame.Tests;

/// <summary>
/// Pins the dead zone the game actually ships.
/// </summary>
/// <remarks>
/// Everything else about the dead zone is covered where the arithmetic lives, in
/// <c>OliveGameStudio.World</c> — but those tests name their own number, because the constant is
/// declared here in the host, downstream of them. That leaves the shipped value covered by
/// nothing: set <see cref="DesktopGamePad.DeadZone"/> to 0 and every test in every other project
/// stays green while a worn controller flies the ship on its own. This file is the only place that
/// would notice.
/// </remarks>
public sealed class ShippedDeadZoneTests
{
    static ShipControls Stick(double thrust, double turn) =>
        ShipControls.FromStick(thrust, turn, DesktopGamePad.DeadZone);

    [Fact]
    public void TheShippedDeadZone_LeavesMostOfTheTravel()
    {
        // a dead zone large enough to matter, small enough to still fly with. Both ends are worth
        // pinning: too small and the ship flies itself, too large and the stick feels dead
        Assert.InRange(DesktopGamePad.DeadZone, 0.05, 0.35);
    }

    [Theory]
    [InlineData(0.05, 0.05)]
    [InlineData(-0.1, 0.15)]
    [InlineData(0.2, -0.2)]
    public void AWornStick_DoesNotFlyTheShip_AtTheShippedDeadZone(double thrust, double turn)
    {
        Assert.True(Stick(thrust, turn).IsNeutral);
    }

    [Fact]
    public void AWornStick_CannotTakeTheGameFromTheKeyboard()
    {
        // this used to be a statement about arbitration — the pad was asked first, so a drifting
        // stick that counted as "in use" shut the keyboard out. The device is now locked at the
        // start press instead, so what has to hold is narrower and stronger: a stick reports no
        // confirm however far it has drifted, so a worn pad cannot become the device the game is
        // played on at all.
        UIController ui = new();
        RoutedShipInput ship = new();
        InputRouter router = new(ui, ship, new RoutedInteraction(), DesktopGamePad.DeadZone);

        Button start = new("START");
        ui.Add(start);
        ui.FocusOn(start);

        router.Route(new InputFrame(
            KeyboardFrame.None,
            new GamePadFrame(Connected: true, Thrust: 0.15, Turn: -0.15, Confirm: false)));

        Assert.Equal(ControlDevice.None, router.LockedTo);
    }

    [Fact]
    public void AStickAtItsStop_StillAsksForEverything_AtTheShippedDeadZone()
    {
        Assert.Equal(1, Stick(1, 0).Thrust);
        Assert.Equal(-1, Stick(-1, 0).Thrust);
    }
}
