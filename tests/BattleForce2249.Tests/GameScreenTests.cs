using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the game screen as the seam between the screen lifecycle and gameplay: entering the
/// screen begins the game, and each frame drives the quests from where the player is.
/// </summary>
public sealed class GameScreenTests : HostTestBase
{
    IGameSession Session => Resolve<IGameSession>();

    IGameScreen GameScreen => Resolve<IGameScreen>();

    /// <summary>
    /// Drives the host through the company screen and a real press of the menu's start button,
    /// leaving the game screen active — the same path a player takes on a new game launch.
    /// </summary>
    /// <param name="configure">Anything else the test needs registered, such as a pilot.</param>
    IHost StartTheGame(Action<IServiceCollection>? configure = null)
    {
        Configure(services: services =>
        {
            services
                // the real UI controller, so pressing the start button raises its action
                .AddSingleton<IUIController, UIController>()
                // the shipping director, so navigating a screen actually enters it
                .AddSingleton<IScreenDirector, LifecycleScreenDirector>();

            configure?.Invoke(services);
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

    [Fact]
    public void EnteringTheScreen_StartsTheGame()
    {
        ((IActivatable)GameScreen).Enter();

        Assert.True(Session.IsReady);
    }

    [Fact]
    public void TheGameScreenIsActive_OnceTheMenuRequestsAStart()
    {
        StartTheGame();

        Assert.Same(GameScreen, ScreenDirector.Current);
    }

    [Fact]
    public void Quest1_IsActive_AfterTheFirstFrameOfANewGame()
    {
        // the screen, not the quest model, notices the player is on the start marker
        IHost host = StartTheGame();

        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Quest quest = Assert.Single(Session.Quests.Active);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    [Fact]
    public void Quest1_StaysInProgress_UntilTheEndMarkerIsReached()
    {
        IHost host = StartTheGame();

        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.Single(Session.Quests.Active);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void MovingForwardAcrossFrames_CompletesQuest1()
    {
        IHost host = StartTheGame();
        QuestMarkers markers = Resolve<IWorld>().QuestMarkers
            .Single(marker => marker.QuestId == BattleForceCampaign.Quest1Id);

        // the quest path on its own, with the player moved by hand rather than flown: the ship is
        // covered by the tests below, and this one is about the markers
        for (int frame = 0; !Session.Quests.Completed.Any(); frame++)
        {
            Assert.True(frame < 10_000, "the player never completed the quest");
            host.Update(TimeSpan.FromSeconds(1 / 60.0));
            Session.Player.MoveBy(0, 10);
        }

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
        Assert.Empty(Session.Quests.Active);
        Assert.True(Session.Player.Position.DistanceTo(markers.End) <= 50 + 10);
    }

    [Fact]
    public void DoesNotDriveQuests_BeforeTheGameHasStarted()
    {
        // frames can arrive while the save is still loading
        GameScreen screen = (GameScreen)GameScreen;

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Empty(Session.Quests.Quests);
    }

    [Fact]
    public async Task AnUnreadableSave_DoesNotSilentlyFreezeTheGameScreen()
    {
        // Nothing in the shipping game holds the task Enter() starts, so a load that threw would
        // leave the session never ready and every frame turning straight back — no crash, no
        // message, just a game that stopped.
        GameSession session = new(
            new FailingSaveProgressService(loadError: new IOException("The process cannot access the file.")),
            new BattleForceCampaign(),
            new BattleForceWorld());
        GameScreen screen = new(
            session,
            new ShipMovement(BattleForceShip.Handling),
            new NeutralShipInput(),
            new QuestProximityWatcher(new BattleForceWorld()),
            NullLogger<GameScreen>.Instance);

        screen.Enter();
        await screen.Started;
        screen.Update(TimeSpan.FromMilliseconds(16));
        screen.Update(TimeSpan.FromMilliseconds(16));

        Assert.True(session.IsReady, "the game screen never became ready, so no frame will ever do anything");
        Quest quest = Assert.Single(session.Quests.Active);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    [Fact]
    public async Task AGameThatFailsToStart_IsLoggedRatherThanLostSilently()
    {
        // the session recovers from a save it cannot read, so anything left is a defect — and a
        // defect nobody can see is what made this silent rather than merely broken
        FakeLogger<GameScreen> logger = new();
        GameScreen screen = new(
            new ThrowingGameSession(),
            new ShipMovement(BattleForceShip.Handling),
            new NeutralShipInput(),
            new QuestProximityWatcher(new BattleForceWorld()),
            logger);

        screen.Enter();
        await Assert.ThrowsAsync<InvalidOperationException>(() => screen.Started);

        Assert.Contains(logger.Errors, entry => entry is InvalidOperationException);
    }

    sealed class ThrowingGameSession : IGameSession
    {
        public Player Player { get; } = new();

        public QuestLog Quests { get; } = new();

        public bool IsReady => false;

        public Task PendingSave => Task.CompletedTask;

        public Exception? SaveError => null;

        public bool IsSavingProgress => false;

        public Task StartNewGame() => throw new InvalidOperationException("a defect");

        public Task Continue() => throw new InvalidOperationException("a defect");

        public Task Save() => throw new InvalidOperationException("a defect");
    }

    sealed class FakeLogger<T> : ILogger<T>
    {
        public List<Exception?> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel is LogLevel.Error)
            {
                Errors.Add(exception);
            }
        }
    }

    // ---- flying the ship ----

    static readonly ShipControls FullAhead = new(thrust: 1, turn: 0);

    /// <summary>
    /// Runs the game for a stretch of 60Hz frames, stopping early once <paramref name="until"/> is
    /// satisfied so a test that is waiting for something does not have to guess how long it takes.
    /// </summary>
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

    [Fact]
    public void FlyingForwardFromANewGame_GetsClearOfTheDebrisField_AndCompletesQuest1()
    {
        // the whole point of the ticket: quest 1 completed by playing rather than by a test
        // reaching in and moving the player
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
        Assert.Empty(Session.Quests.Active);
    }

    [Fact]
    public void HandsOff_TheShipStaysPut_AndQuest1StaysInProgress()
    {
        // no pilot registered, so the engine's neutral input is what the ship is flown on
        IHost host = StartTheGame();

        Play(host, frames: 600);

        Assert.Equal(new Position(0, 0), Session.Player.Position);
        Assert.Single(Session.Quests.Active);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void TurningTakesTheShipOffTheForwardAxis()
    {
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 1, turn: 1) };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 60);

        Assert.True(Math.Abs(Session.Player.Position.X) > 1, "the helm did not steer the ship");
    }

    [Fact]
    public void TheShipFliesBeforeTheQuestsAreMeasured()
    {
        // ordering, and it is load bearing: a quest measured against last frame's position fires a
        // frame late, which at speed is a marker the player has already gone past
        FixedShipInput pilot = new();
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 1);                                  // the start marker begins quest 1
        Assert.Single(Session.Quests.Active);

        // 60 units short of the exit marker, which fires within 50; one second of thrust from rest
        // covers about 68, so this frame is the frame that arrives
        Session.Player.MoveTo(new Position(0, 940));
        pilot.Controls = FullAhead;

        host.Update(TimeSpan.FromSeconds(1));

        Assert.Single(Session.Quests.Completed);
    }

    [Fact]
    public void AFrameThatFliesTheShipClearPastTheExitMarker_StillCompletesQuest1()
    {
        // the ticket, through the shipping composition. One stalled frame carries the ship from
        // short of the exit marker to hundreds of units beyond it; sampling where it finished
        // would leave the quest running and the player wondering why the debris field never ended.
        FixedShipInput pilot = new();
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 1);                                  // the start marker begins quest 1
        Assert.Single(Session.Quests.Active);

        Session.Player.MoveTo(new Position(0, 940));
        pilot.Controls = FullAhead;

        host.Update(TimeSpan.FromSeconds(5));

        // the frame really did overshoot: it is the sweep completing the quest, not the arrival
        Assert.True(
            Session.Player.Position.Y > 1000 + 50,
            $"the frame stopped at {Session.Player.Position.Y}, inside the trigger, so this proves nothing");
        Assert.Single(Session.Quests.Completed);
        Assert.Empty(Session.Quests.Active);
    }

    [Fact]
    public void AFrameThatFliesWideOfTheExitMarker_LeavesQuest1Running()
    {
        // the other half: sweeping must not turn a trigger into a corridor
        FixedShipInput pilot = new();
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 1);
        Assert.Single(Session.Quests.Active);

        // alongside the exit marker's lane and 300 units to the side of it, flying past
        Session.Player.MoveTo(new Position(300, 940));
        pilot.Controls = FullAhead;

        host.Update(TimeSpan.FromSeconds(5));

        Assert.Single(Session.Quests.Active);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void EnteringTheScreen_BringsTheShipToRest()
    {
        // the save carries where the player is, never how fast they were going
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 1, turn: 1) };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));
        Play(host, frames: 120);

        ((IActivatable)GameScreen).Enter();

        ShipMovement ship = Resolve<ShipMovement>();
        Assert.Equal(Velocity.Stationary, ship.Velocity);
        Assert.Equal(0, ship.Heading);
    }

    [Fact]
    public void ADeviceThatGlitchesForAFewFrames_DoesNotCostThePlayerTheirGame()
    {
        // ShipControls reads an unreadable axis as hands-off, and ShipControlsTests pins that in
        // isolation. This is the same thing through the whole game, because the consequence of
        // getting it wrong is not a wrong number: a NaN position makes every quest distance NaN, so
        // no trigger fires again and the player is left flying a ship that can never finish
        // anything. The failure is silent, so only an end-to-end test says the game survived it.
        FixedShipInput pilot = new();
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 1);
        Assert.Single(Session.Quests.Active);

        pilot.Controls = new ShipControls(thrust: double.NaN, turn: double.NaN);
        Play(host, frames: 10);

        pilot.Controls = FullAhead;
        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void Quest1_IsCompletableAtAnyFrameRateTheGameWillBePlayedAt(int framesPerSecond)
    {
        // frame-rate independence is pinned on the physics in isolation; this asks the question the
        // player actually cares about, which is whether the game can be finished on their machine.
        // A quest driven by sampling the player once a frame is where a frame rate stops being an
        // implementation detail, so the acceptance criterion is worth holding across the range
        // rather than at the 60Hz the other tests all happen to run at.
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));
        TimeSpan frameTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / framesPerSecond);

        for (int frame = 0; frame < framesPerSecond * 30 && !Session.Quests.Completed.Any(); frame++)
        {
            host.Update(frameTime);
        }

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
        Assert.Empty(Session.Quests.Active);
    }
}
