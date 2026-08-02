using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Two frames the acceptance pass next door does not reach.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DesktopPilotAcceptanceTests"/> already flies Quest 1 through the real mapping and the
/// real arbitration, on the keyboard with a pad resting beside it and on the stick. These are not
/// more of those. They are the two cases where the shipping composition can be wrong while every
/// test that flies Quest 1 straight ahead still passes, because flying straight ahead is the one
/// shape in which the wrong answer and the right answer are the same number.
/// </para>
/// <para>
/// Both were written to fail: the first fails if <c>FirstActiveShipInput</c> is changed to
/// arbitrate per axis, which the end-to-end summing test cannot see, and the second fails if
/// crossing the dead zone stops asking for anything short of the stick's stop.
/// </para>
/// </remarks>
public sealed class DesktopPilotFliesTheGameAdversarialTests : HostTestBase
{
    /// <summary>The dead zone the shipping gamepad binding uses.</summary>
    /// <remarks>
    /// Repeated rather than referenced, because <c>GamePadShipInput.DeadZone</c> lives in the
    /// MonoGame host, which the game's own tests deliberately do not depend on. The real constant
    /// is pinned in <c>BattleForce2249.MonoGame.Tests.DesktopPilotAdversarialTests</c>.
    /// </remarks>
    const double PadDeadZone = 0.2;

    IGameSession Session => Resolve<IGameSession>();

    /// <summary>
    /// Drives the host through the company screen and a real press of the menu's start button,
    /// leaving the game screen active — the same path a player takes on a new game launch.
    /// </summary>
    IHost StartTheGame(IShipInput pilot)
    {
        Configure(services: services => services
            .AddSingleton<IUIController, UIController>()
            .AddSingleton<IScreenDirector, LifecycleScreenDirector>()
            .AddSingleton(pilot));

        IHost host = CreateHost();
        host.Start();
        host.Update(TimeSpan.FromDays(1));                      // company screen elapses

        MenuScreen menu = (MenuScreen)Resolve<IMenuScreen>();
        for (int frame = 0; !menu.IsReadyForInput; frame++)
        {
            Assert.True(frame < 1000, "the menu never became ready for input");
            host.Update(TimeSpan.Zero);
        }

        menu.Press();
        menu.Release();

        return host;
    }

    void Play(IHost host, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            host.Update(TimeSpan.FromSeconds(1 / 60.0));
        }
    }

    /// <summary>A device holding whatever the test put in it.</summary>
    sealed class Device(ShipControls controls) : IShipInput
    {
        public ShipControls Read() => controls;
    }

    static Device Stick(double thrust, double turn) =>
        new(ShipControls.FromStick(thrust, turn, PadDeadZone));

    static Device Keys(bool ahead) =>
        new(ShipControls.FromKeys(ahead, astern: false, port: false, starboard: false));

    [Fact]
    public void AStickOnTheHelmAndAKeyOnTheThrottle_DoesNotFlyTheShipOnBoth()
    {
        // the arbitration is per device, not per axis, and this is the frame that tells them
        // apart. The pad asks for helm and no thrust; the keyboard asks for thrust and no helm.
        // Per device, the pad wins outright and the ship turns on the spot. Per axis, the two
        // answers are taken together and the ship flies off under a thrust nobody's hand asked
        // for. The end-to-end summing test cannot see this, because it has both devices asking
        // for the same full-ahead: summed or arbitrated, that comes to the same number, and the
        // ship covers the same ground either way.
        IHost host = StartTheGame(new FirstActiveShipInput(Stick(0, 0.6), Keys(ahead: true)));
        ShipMovement ship = Resolve<ShipMovement>();

        Play(host, frames: 60);

        Assert.Equal(new Position(0, 0), Session.Player.Position);
        Assert.True(ship.Heading != 0, "the pad's helm never reached the ship");
    }

    [Fact]
    public void APlayerOnTheGamePad_JustPastTheDeadZone_StillGetsUnderway()
    {
        // the rescaling past the dead zone, felt as movement rather than measured as a number. A
        // pad that only moved the ship at its stop would read as a controller the game ignores,
        // and every quest would still be completable at full deflection, so nothing that flies
        // Quest 1 straight ahead would notice
        IHost host = StartTheGame(new FirstActiveShipInput(Stick(0.3, 0), Keys(ahead: false)));

        Play(host, frames: 120);

        Assert.True(
            Session.Player.Position.Y > 1,
            $"the pad past its dead zone did not get the ship underway (Y = {Session.Player.Position.Y})");
    }
}
