using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Flies the game the way a person does: frames of device state pushed into
/// <see cref="IHost.Input"/>, and nothing else. No test presses the start button by hand, chooses
/// a device, or hands the ship a <see cref="ShipControls"/> — every one of those has to come out
/// of the keys and the stick.
/// </summary>
/// <remarks>
/// This is the acceptance level for the input wiring. The router's own tests prove each rule
/// against a controller a test set up; what they cannot prove is that the rules add up to a game
/// somebody can start and fly, through the real screens in the real order — that the menu is
/// reachable with a key, that pressing it hands the same key to the ship, and that the ship then
/// goes somewhere.
/// </remarks>
public sealed class PlayerControlAcceptanceTests : HostTestBase
{
    IGameSession Session => Resolve<IGameSession>();

    IInputRouter Router => Resolve<IInputRouter>();

    IShipInput Pilot => Resolve<IShipInput>();

    static InputFrame Keys(
        bool ahead = false,
        bool astern = false,
        bool port = false,
        bool starboard = false,
        bool confirm = false) =>
        new(new KeyboardFrame(ahead, astern, port, starboard, confirm), GamePadFrame.None);

    static InputFrame Stick(double thrust = 0, double turn = 0, bool confirm = false) =>
        new(KeyboardFrame.None, new GamePadFrame(Connected: true, thrust, turn, confirm));

    /// <summary>
    /// Composes the game as it ships, apart from the two doubles the base class cannot do without:
    /// the real UI controller, so a press reaches a real button, and the real director, so
    /// navigating a screen actually enters it.
    /// </summary>
    void ComposeAsShipped() =>
        Configure(services: services =>
        {
            services
                .AddSingleton<IUIController, UIController>()
                .AddSingleton<IScreenDirector, LifecycleScreenDirector>();
        });

    /// <summary>
    /// Runs frames until the menu is ready for input — the save is read off the frame loop, and
    /// the start button is disabled until it comes back.
    /// </summary>
    static void WaitForTheMenu(IHost host, MenuScreen menu)
    {
        for (int frame = 0; !menu.IsReadyForInput; frame++)
        {
            Assert.True(frame < 1000, "the menu never became ready for input");
            host.Input(InputFrame.None);
            host.Update(TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Gets to the point a player is at a second after launching: past the company screen, at a
    /// menu waiting to be pressed, with no device chosen yet.
    /// </summary>
    IHost AtTheMenu()
    {
        ComposeAsShipped();

        IHost host = CreateHost();
        host.Start();
        host.Input(InputFrame.None);
        host.Update(TimeSpan.FromDays(1));            // the company screen elapses

        WaitForTheMenu(host, (MenuScreen)Resolve<IMenuScreen>());

        return host;
    }

    /// <summary>
    /// Presses and releases start on the given device, which is what begins the game and chooses
    /// the device it is played on.
    /// </summary>
    static void PressStart(IHost host, InputFrame down, InputFrame up)
    {
        host.Input(down);
        host.Update(TimeSpan.Zero);

        host.Input(up);
        host.Update(TimeSpan.Zero);
    }

    static void Fly(IHost host, InputFrame holding, int frames, Func<bool>? until = null)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            if (until?.Invoke() is true)
            {
                return;
            }

            host.Input(holding);
            host.Update(TimeSpan.FromSeconds(1 / 60.0));
        }
    }

    [Fact]
    public void AtTheMenu_NoDeviceHasBeenChosen()
    {
        AtTheMenu();

        Assert.Equal(ControlDevice.None, Router.LockedTo);
    }

    [Fact]
    public void AtTheMenu_HoldingTheAheadKey_DoesNotFlyTheShip()
    {
        // UI first, seen from the outside: the ahead key and the menu share the keyboard, and a
        // player reading the menu is not also burning the engine
        IHost host = AtTheMenu();

        host.Input(Keys(ahead: true));
        host.Update(TimeSpan.Zero);

        Assert.True(Pilot.Read().IsNeutral);
    }

    [Fact]
    public void PressingStartOnTheKeyboard_StartsTheGame()
    {
        // the gap this issue was about: before it, nothing carried a key press to the menu at all,
        // so the shipping game could not be started by a person
        IHost host = AtTheMenu();

        PressStart(host, Keys(confirm: true), Keys());

        Assert.IsType<GameScreen>(ScreenDirector.Current);
    }

    [Fact]
    public void PressingStartOnTheKeyboard_MakesTheKeyboardTheDeviceTheGameIsPlayedOn()
    {
        IHost host = AtTheMenu();

        PressStart(host, Keys(confirm: true), Keys());

        Assert.Equal(ControlDevice.Keyboard, Router.LockedTo);
    }

    [Fact]
    public void PressingStartOnThePad_MakesThePadTheDeviceTheGameIsPlayedOn()
    {
        IHost host = AtTheMenu();

        PressStart(host, Stick(confirm: true), Stick());

        Assert.Equal(ControlDevice.GamePad, Router.LockedTo);
    }

    /// <summary>
    /// How far forward counts as "actually flew there" for the tests below. Comfortably reached
    /// within the frame budget by either device's sustained ahead input, and comfortably short of
    /// the debris field's first real obstruction — these are about a key or a stick reaching the
    /// ship, not about the field being navigable, which is a separate, content-side question.
    /// </summary>
    const double FlewForward = 200;

    [Fact]
    public void HoldingTheAheadKey_FliesTheShipForward()
    {
        // the ticket's criterion, end to end: a person presses start, holds a key, and the ship
        // goes somewhere. Nothing below this line moves the player but the keyboard.
        IHost host = AtTheMenu();
        PressStart(host, Keys(confirm: true), Keys());

        Fly(host, Keys(ahead: true), frames: 60 * 30, until: () => Session.Player.Position.Y > FlewForward);

        Assert.True(Session.Player.Position.Y > FlewForward, "the ahead key never flew the ship");
    }

    [Fact]
    public void PushingTheStickForward_FliesTheShipForward()
    {
        IHost host = AtTheMenu();
        PressStart(host, Stick(confirm: true), Stick());

        Fly(host, Stick(thrust: 1), frames: 60 * 30, until: () => Session.Player.Position.Y > FlewForward);

        Assert.True(Session.Player.Position.Y > FlewForward, "the stick never flew the ship");
    }

    [Fact]
    public void HoldingTheAsternKey_FliesTheOtherWay()
    {
        // the six movements the ticket asks for come down to two axes, and both directions of each
        // have to be reachable from the keys rather than only the one quest 1 needs
        IHost host = AtTheMenu();
        PressStart(host, Keys(confirm: true), Keys());

        Fly(host, Keys(astern: true), frames: 60);

        Assert.True(Session.Player.Position.Y < 0, "the astern key did not fly the ship backwards");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void HoldingATurnKey_TurnsTheShip(bool port, bool starboard)
    {
        IHost host = AtTheMenu();
        PressStart(host, Keys(confirm: true), Keys());

        double before = Session.Ship.Movement.Heading;
        Fly(host, Keys(port: port, starboard: starboard), frames: 60);

        Assert.NotEqual(before, Session.Ship.Movement.Heading);
    }

    [Fact]
    public void APadLeftOnTheStick_CannotTakeTheShipFromTheKeyboard()
    {
        // the lock as the player feels it. Both devices are asking for opposite things, so which
        // one the ship obeys is visible in which way it goes.
        IHost host = AtTheMenu();
        PressStart(host, Keys(confirm: true), Keys());

        Fly(host, new InputFrame(
                new KeyboardFrame(Ahead: true, false, false, false, false),
                new GamePadFrame(Connected: true, Thrust: -1, Turn: 0, Confirm: false)),
            frames: 60);

        Assert.True(Session.Player.Position.Y > 0, "the pad flew the ship the player was flying");
    }

    [Fact]
    public void AKeyboardLeftHeldDown_CannotTakeTheShipFromThePad()
    {
        IHost host = AtTheMenu();
        PressStart(host, Stick(confirm: true), Stick());

        Fly(host, new InputFrame(
                new KeyboardFrame(Ahead: true, false, false, false, false),
                new GamePadFrame(Connected: true, Thrust: -1, Turn: 0, Confirm: false)),
            frames: 60);

        Assert.True(Session.Player.Position.Y < 0, "the keyboard flew the ship the player was flying");
    }

    [Fact]
    public void APadUnpluggedMidFlight_StopsTheShip_RatherThanHandingItToTheKeyboard()
    {
        // the cost of locking for the session, stated as a test rather than left to be discovered.
        // Handing the ship to a device the player did not choose is the failure the lock exists to
        // prevent; the ship coasting to a stop is at least something they can see.
        IHost host = AtTheMenu();
        PressStart(host, Stick(confirm: true), Stick());

        Fly(host, Stick(thrust: 1), frames: 60);
        double flown = Session.Player.Position.Y;
        Assert.True(flown > 0, "the stick never flew the ship");

        Fly(host, new InputFrame(
                new KeyboardFrame(Ahead: false, Astern: true, false, false, false),
                GamePadFrame.None),
            frames: 60);

        // still coasting forward on the momentum the stick gave it, and not being flown astern
        Assert.True(Session.Player.Position.Y > flown, "the keyboard took over an unplugged pad's ship");
    }
}
