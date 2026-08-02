using OliveGameStudio;

namespace BattleForce2249.MonoGame.Tests;

/// <summary>
/// Pins the dead zone the game actually ships.
/// </summary>
/// <remarks>
/// Everything else about the dead zone is covered where the arithmetic lives, in
/// <c>OliveGameStudio.World</c> — but those tests name their own number, because the constant is
/// declared here in the host, downstream of them. That leaves the shipped value covered by
/// nothing: set <see cref="GamePadShipInput.DeadZone"/> to 0 and every test in every other project
/// stays green while a worn controller flies the ship on its own and locks the keyboard out. This
/// file is the only place that would notice.
/// </remarks>
public sealed class ShippedDeadZoneTests
{
    static ShipControls Stick(double thrust, double turn) =>
        ShipControls.FromStick(thrust, turn, GamePadShipInput.DeadZone);

    [Fact]
    public void TheShippedDeadZone_LeavesMostOfTheTravel()
    {
        // a dead zone large enough to matter, small enough to still fly with. Both ends are worth
        // pinning: too small and the ship flies itself, too large and the stick feels dead
        Assert.InRange(GamePadShipInput.DeadZone, 0.05, 0.35);
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
    public void AWornStick_DoesNotShutTheKeyboardOut_AtTheShippedDeadZone()
    {
        // through the real arbitration in the real order, because the pad being asked first is
        // what makes a drifting stick able to take the whole game away from the player
        FirstActiveShipInput pilot = new(
            new FixedInput(Stick(0.15, -0.15)),
            new FixedInput(ShipControls.FromKeys(ahead: true, astern: false, port: false, starboard: false)));

        Assert.Equal(1, pilot.Read().Thrust);
    }

    [Fact]
    public void AStickAtItsStop_StillAsksForEverything_AtTheShippedDeadZone()
    {
        Assert.Equal(1, Stick(1, 0).Thrust);
        Assert.Equal(-1, Stick(-1, 0).Thrust);
    }

    sealed class FixedInput(ShipControls controls) : IShipInput
    {
        public ShipControls Read() => controls;
    }
}
