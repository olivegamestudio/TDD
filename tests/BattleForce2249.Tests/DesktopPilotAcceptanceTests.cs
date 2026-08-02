using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Flies quest 1 the way a person does, through the real key and stick mappings and the real
/// arbitration rather than through controls a test built by hand.
/// </summary>
/// <remarks>
/// The existing gameplay tests prove the physics: they hand <see cref="ShipMovement"/> a
/// <see cref="ShipControls"/> and watch the ship go. What they cannot prove is the thing this
/// issue is about — that holding a key produces controls that fly the ship at all. Everything
/// below therefore goes through <see cref="ShipControls.FromKeys"/> or
/// <see cref="ShipControls.FromStick"/> and through <see cref="FirstActiveShipInput"/>, with the
/// gamepad bound ahead of the keyboard exactly as the shipping host binds them.
/// </remarks>
public sealed class DesktopPilotAcceptanceTests : HostTestBase
{
    /// <summary>
    /// The dead zone the game ships. Named here rather than referenced because the host that
    /// owns the real constant lives downstream of this project; the constant itself is pinned
    /// where it is declared, in the host's own tests.
    /// </summary>
    const double DeadZone = 0.2;

    /// <summary>
    /// A device whose reading a test can change between frames, standing in for a hand on the
    /// keys or on the stick.
    /// </summary>
    sealed class HandOn : IShipInput
    {
        public ShipControls Controls { get; set; } = ShipControls.Neutral;

        public ShipControls Read() => Controls;
    }

    IGameSession Session => Resolve<IGameSession>();

    /// <summary>
    /// Starts the game the way a player does — through the company screen and the menu's real
    /// start button — with the given devices bound in the given order behind one
    /// <see cref="FirstActiveShipInput"/>, as the shipping host binds them.
    /// </summary>
    IHost StartTheGame(params IShipInput[] devices)
    {
        Configure(services: services =>
        {
            services
                // the real UI controller, so pressing the start button raises its action
                .AddSingleton<IUIController, UIController>()
                // the shipping director, so navigating a screen actually enters it
                .AddSingleton<IScreenDirector, LifecycleScreenDirector>()
                .AddSingleton<IShipInput>(new FirstActiveShipInput(devices));
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

    static ShipControls AheadKey =>
        ShipControls.FromKeys(ahead: true, astern: false, port: false, starboard: false);

    static ShipControls StickForward => ShipControls.FromStick(thrust: 1, turn: 0, DeadZone);

    static ShipControls RestingStick =>
        ShipControls.FromStick(thrust: DeadZone, turn: -DeadZone, DeadZone);

    [Fact]
    public void HoldingTheAheadKey_CompletesQuest1()
    {
        // the ticket's first criterion: a person flies quest 1, with no test moving the player
        HandOn pad = new();
        HandOn keys = new() { Controls = AheadKey };
        IHost host = StartTheGame(pad, keys);

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    [Fact]
    public void PushingTheStickForward_CompletesQuest1()
    {
        HandOn pad = new() { Controls = StickForward };
        IHost host = StartTheGame(pad, new HandOn());

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Assert.Single(Session.Quests.Completed);
    }

    [Fact]
    public void AWornStickBoundAheadOfTheKeyboard_DoesNotShutTheKeyboardOut()
    {
        // the one that would have been worst to ship: the pad is asked first, so a stick resting
        // off centre that counted as "in use" would leave the player unable to fly at all
        HandOn pad = new() { Controls = RestingStick };
        HandOn keys = new() { Controls = AheadKey };
        IHost host = StartTheGame(pad, keys);

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Assert.Single(Session.Quests.Completed);
    }

    [Fact]
    public void AWornStickOnItsOwn_DoesNotFlyTheShip()
    {
        HandOn pad = new() { Controls = RestingStick };
        IHost host = StartTheGame(pad, new HandOn());

        Play(host, frames: 600);

        Assert.Equal(new Position(0, 0), Session.Player.Position);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void APadThatCannotBeRead_StillLeavesThePlayerAbleToFly()
    {
        HandOn pad = new() { Controls = new ShipControls(double.NaN, double.NaN) };
        HandOn keys = new() { Controls = AheadKey };
        IHost host = StartTheGame(pad, keys);

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Assert.Single(Session.Quests.Completed);
    }

    [Fact]
    public void LettingGoOfTheStick_HandsTheShipToTheKeyboard()
    {
        // the two devices are asking for opposite things, so which one the ship obeys is visible
        // in which way it goes. The arbitration holds no state, so the handover is immediate —
        // a player who puts the pad down should not have to wait for the keyboard to be believed
        HandOn pad = new() { Controls = ShipControls.FromStick(thrust: -1, turn: 0, DeadZone) };
        HandOn keys = new() { Controls = AheadKey };
        IHost host = StartTheGame(pad, keys);

        Play(host, frames: 60);
        Assert.True(Session.Player.Position.Y < 0, "the stick never flew the ship astern");

        pad.Controls = ShipControls.Neutral;

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Assert.Single(Session.Quests.Completed);
    }

    [Fact]
    public void AKeyOnTheThrottleAndAStickOnTheHelm_DoNotSum()
    {
        // arbitration is per device: the pad is asked first and it is asking for the helm, so the
        // key on the throttle is not consulted this frame and the ship does not burn
        HandOn pad = new() { Controls = ShipControls.FromStick(thrust: 0, turn: 1, DeadZone) };
        HandOn keys = new() { Controls = AheadKey };
        IHost host = StartTheGame(pad, keys);

        Play(host, frames: 1);

        Assert.Equal(Velocity.Stationary, Resolve<ShipMovement>().Velocity);
    }
}
