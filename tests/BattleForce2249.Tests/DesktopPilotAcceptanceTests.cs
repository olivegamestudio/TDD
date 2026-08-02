using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's acceptance pass on #9. The existing game screen tests fly the ship on
/// <see cref="ShipControls"/> built by hand, which proves the physics but not the binding: the
/// ticket's definition of done is that a <em>person</em> can fly Quest 1 with a keyboard, and on a
/// gamepad, and that a resting stick does not fly it for them. These fly it through the real
/// mapping — <see cref="ShipControls.FromKeys"/> and <see cref="ShipControls.FromStick"/> — and
/// through the real <see cref="FirstActiveShipInput"/> arbitration, in the same order the shipping
/// composition binds them.
/// </summary>
/// <remarks>
/// What still cannot be covered is <c>Keyboard.GetState</c> and <c>GamePad.GetState</c>, which are
/// static calls into MonoGame with no seam in front of them. So these stand in for the device
/// reads and exercise everything downstream of them, which is where every decision this ticket
/// made actually lives.
/// </remarks>
public sealed class DesktopPilotAcceptanceTests : HostTestBase
{
    /// <summary>The dead zone the shipping gamepad binding uses.</summary>
    const double DeadZone = 0.2;

    IGameSession Session => Resolve<IGameSession>();

    /// <summary>
    /// A stand-in for one real device, answering with whatever the engine's own mapping makes of
    /// the keys held or the stick position it is given.
    /// </summary>
    sealed class MappedDevice : IShipInput
    {
        public ShipControls Controls { get; set; } = ShipControls.Neutral;

        public ShipControls Read() => Controls;

        /// <summary>A stick pushed to the given position, read through the shipping dead zone.</summary>
        public static MappedDevice Stick(double thrust, double turn) =>
            new() { Controls = ShipControls.FromStick(thrust, turn, DeadZone) };

        /// <summary>The four keys held or not, read the way the desktop keyboard binding reads them.</summary>
        public static MappedDevice Keys(bool ahead = false, bool astern = false, bool port = false, bool starboard = false) =>
            new() { Controls = ShipControls.FromKeys(ahead, astern, port, starboard) };
    }

    /// <summary>
    /// Binds a gamepad and a keyboard in the order the shipping host binds them — the pad first —
    /// so these tests fail if that precedence ever changes silently.
    /// </summary>
    static FirstActiveShipInput Desktop(IShipInput pad, IShipInput keyboard) => new(pad, keyboard);

    /// <summary>
    /// Drives the host through the company screen and a real press of the menu's start button,
    /// leaving the game screen active — the same path a player takes on a new game launch.
    /// </summary>
    IHost StartTheGame(IShipInput pilot)
    {
        Configure(services: services =>
        {
            services
                .AddSingleton<IUIController, UIController>()
                .AddSingleton<IScreenDirector, LifecycleScreenDirector>()
                .AddSingleton(pilot);
        });

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

    static void Play(IHost host, int frames, Func<bool>? until = null)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            if (until?.Invoke() is true)
            {
                return;
            }

            host.Update(TimeSpan.FromSeconds(1 / 60.0));
        }
    }

    void AssertQuest1Completed()
    {
        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    // ---- the definition of done, one test per line of it

    [Fact]
    public void HoldingTheAheadKey_FliesQuest1_WithAGamePadAlsoBoundAndResting()
    {
        // "a player can launch the game, fly the ship with the keyboard, and complete Quest 1
        // without a test moving the player" — flown through FromKeys, not a hand-built ask
        IHost host = StartTheGame(Desktop(MappedDevice.Stick(0, 0), MappedDevice.Keys(ahead: true)));

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        AssertQuest1Completed();
    }

    [Fact]
    public void PushingTheStickForward_FliesQuest1()
    {
        // "the same is true on a gamepad" — flown through FromStick and its dead zone rescaling
        IHost host = StartTheGame(Desktop(MappedDevice.Stick(1, 0), MappedDevice.Keys()));

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        AssertQuest1Completed();
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.19, 0.19)]
    [InlineData(-0.19, 0.1)]
    [InlineData(0.2, -0.2)]
    public void AWornStickRestingInsideItsDeadZone_DoesNotFlyTheShipOnItsOwn(double thrust, double turn)
    {
        // "a dead zone stops a resting stick from producing thrust or turn" — the boundary is
        // included, because a stick parked exactly on the edge is still a stick nobody is touching
        IHost host = StartTheGame(Desktop(MappedDevice.Stick(thrust, turn), MappedDevice.Keys()));

        Play(host, frames: 600);

        Assert.Equal(new Position(0, 0), Session.Player.Position);
        Assert.Empty(Session.Quests.Completed);
    }

    // ---- the arbitration, at the level the player feels it

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.19, -0.19)]
    [InlineData(0.05, 0.15)]
    public void AWornStickBoundAheadOfTheKeyboard_DoesNotShutTheKeyboardOut(double drift, double turnDrift)
    {
        // the pad is asked first, so a pad that drifts inside its dead zone must still report
        // itself as not in use — otherwise a player with a controller plugged in cannot use the
        // keyboard, and nothing says why
        IHost host = StartTheGame(
            Desktop(MappedDevice.Stick(drift, turnDrift), MappedDevice.Keys(ahead: true)));

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        AssertQuest1Completed();
    }

    [Fact]
    public void AGamePadWhoseDriverReportsNothingReadable_DoesNotLeaveThePlayerUnableToFly()
    {
        // the regression QA raised as 4b, pinned where it is felt rather than at the unit: an
        // unreadable pad claiming the arbitration is a player whose hands are on the controls and
        // a ship that never moves, with nothing logged
        IHost host = StartTheGame(
            Desktop(MappedDevice.Stick(double.NaN, double.NaN), MappedDevice.Keys(ahead: true)));

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        AssertQuest1Completed();
        Assert.False(double.IsNaN(Session.Player.Position.Y), "an unreadable axis reached the position");
    }

    [Fact]
    public void AGamePadInUse_WinsOverTheKeyboardOutright_RatherThanTheTwoSumming()
    {
        // both devices asking at once must not fly the ship harder than either asked
        IHost host = StartTheGame(
            Desktop(MappedDevice.Stick(1, 0), MappedDevice.Keys(ahead: true)));
        IHost keyboardOnly;

        Play(host, frames: 120);
        Position both = Session.Player.Position;

        using (DesktopPilotAcceptanceTests solo = new())
        {
            keyboardOnly = solo.StartTheGame(
                Desktop(MappedDevice.Stick(0, 0), MappedDevice.Keys(ahead: true)));
            Play(keyboardOnly, frames: 120);

            Assert.Equal(solo.Session.Player.Position.Y, both.Y, precision: 6);
        }
    }

    [Fact]
    public void HoldingTheAheadAndAsternKeysTogether_LeavesTheShipWhereItIs()
    {
        // opposite keys cancel, and a cancelled keyboard reports itself as not in use, so the
        // ship is left at rest rather than flown in whichever direction was checked first
        IHost host = StartTheGame(
            Desktop(MappedDevice.Stick(0, 0), MappedDevice.Keys(ahead: true, astern: true)));

        Play(host, frames: 600);

        Assert.Equal(new Position(0, 0), Session.Player.Position);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void LettingGoOfTheStickMidFlight_HandsTheShipToTheKeyboardOnTheNextFrame()
    {
        // stateless arbitration, felt over frames: no timeout the player cannot see the reason for
        MappedDevice pad = MappedDevice.Stick(1, 1);            // ahead and hard to starboard
        MappedDevice keys = MappedDevice.Keys(ahead: true);     // ahead, helm centred
        IHost host = StartTheGame(Desktop(pad, keys));
        ShipMovement ship = Resolve<ShipMovement>();

        Play(host, frames: 60);
        Assert.True(Math.Abs(ship.Heading) > 0.1, "the stick never steered the ship");

        pad.Controls = ShipControls.FromStick(0, 0, DeadZone);  // hands off the pad
        double heldHeading = ship.Heading;
        Position before = Session.Player.Position;

        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        // the keyboard answers from this frame on, with no delay: its helm is centred, so the ship
        // stops turning, and its thrust is held, so the ship keeps flying rather than going dead
        Assert.Equal(heldHeading, ship.Heading, precision: 9);
        Assert.NotEqual(before, Session.Player.Position);
    }
}
