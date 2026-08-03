using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.MonoGame.Tests;

/// <summary>
/// Covers what can be checked about the desktop pilot without a device: that the shipping
/// composition wires the pushed input path end to end, and that the dead zone the composed game
/// actually flies on is this host's number.
/// </summary>
/// <remarks>
/// The old ordering guard — register the pilot above <c>AddBattleForce</c> and watch the game go
/// unflyable — has no equivalent any more, and that is worth saying rather than leaving to be
/// noticed. Both registrations now produce an <see cref="InputRouter"/>, differing only in the
/// dead zone they were handed, so a mis-ordered composition is not a game with nobody at the
/// controls: it is a game flying on <see cref="InputRouter.DefaultDeadZone"/> instead. The tests
/// below pin the number in effect through the real container, which is the form that guard can
/// still take, and it has teeth the moment the two numbers stop agreeing.
/// </remarks>
public sealed class DesktopPilotRegistrationTests
{
    /// <summary>
    /// Builds the shipping composition and starts the game on the pad, which is what makes the pad
    /// the device the game is played on.
    /// </summary>
    sealed class ComposedGame : IDisposable
    {
        readonly ServiceProvider _provider;

        public ComposedGame()
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddBattleForce();
            services.AddDesktopPilot();

            _provider = services.BuildServiceProvider();

            IUIController ui = _provider.GetRequiredService<IUIController>();
            Button start = new("START");
            ui.Add(start);
            ui.FocusOn(start);

            Router.Route(Pad(confirm: true));
            Router.Route(Pad(confirm: false));
            ui.UnFocus();
        }

        public IInputRouter Router => _provider.GetRequiredService<IInputRouter>();

        public IShipInput Pilot => _provider.GetRequiredService<IShipInput>();

        public void Dispose() => _provider.Dispose();
    }

    static InputFrame Pad(double thrust = 0, bool confirm = false) =>
        new(KeyboardFrame.None, new GamePadFrame(Connected: true, thrust, Turn: 0, confirm));

    [Fact]
    public void TheComposedGame_PutsRoutedInputWhereThePhysicsReadsIt()
    {
        // the whole pushed path in one assertion: a frame handed to the router the container built
        // reaches the IShipInput the container hands the game screen. Registering the two
        // separately would leave the ship reading a different object for ever, and nothing else in
        // this project would notice.
        using ComposedGame game = new();

        game.Router.Route(Pad(thrust: 1));

        Assert.Equal(1, game.Pilot.Read().Thrust);
    }

    [Fact]
    public void TheComposedGame_FliesOnTheHostsDeadZone()
    {
        using ComposedGame game = new();

        game.Router.Route(Pad(thrust: DesktopGamePad.DeadZone - 0.01));
        Assert.True(game.Pilot.Read().IsNeutral, "a stick inside the dead zone flew the ship");

        game.Router.Route(Pad(thrust: DesktopGamePad.DeadZone + 0.01));
        Assert.False(game.Pilot.Read().IsNeutral, "a stick past the dead zone did not fly the ship");
    }

    [Fact]
    public void TheComposedGame_IsLockedToTheDeviceThatStartedIt()
    {
        using ComposedGame game = new();

        Assert.Equal(ControlDevice.GamePad, game.Router.LockedTo);
    }
}
