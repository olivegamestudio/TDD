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
    /// <param name="also">Anything else the test needs registered, after the two above so it wins.</param>
    IHost StartTheGame(Action<IServiceCollection>? also = null)
    {
        Configure(services: services =>
        {
            services
                // the real UI controller, so pressing the start button raises its action
                .AddSingleton<IUIController, UIController>()
                // the shipping director, so navigating a screen actually enters it
                .AddSingleton<IScreenDirector, LifecycleScreenDirector>();

            also?.Invoke(services);
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

        // travel to the end marker a frame at a time, as the movement system will
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
    public void OneLongFrame_CompletesQuest1_WithoutStandingOnTheExitMarker()
    {
        // The end of #8, driven the way the game will drive it. The player is flown from 500 short
        // of the exit marker to 500 past it inside a single frame, so no frame ever *lands* inside
        // the 50 unit end trigger — under point sampling the quest would still be in progress with
        // the debris field a thousand units behind.
        IHost host = StartTheGame();
        host.Update(TimeSpan.FromSeconds(1 / 60.0));
        Assert.Single(Session.Quests.Active);

        Session.Player.MoveTo(new Position(0, 500));
        host.Update(TimeSpan.FromSeconds(1 / 60.0));           // takes the placement, covers no ground
        Session.Player.MoveBy(0, 1000);
        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
        Assert.True(
            Session.Player.Position.DistanceTo(new Position(0, 1000)) > 50,
            "the player finished inside the end trigger, so this did not test the sweep");
    }

    [Fact]
    public void ResumingASaveDoesNotSweepTheGroundTheGameWasNeverFlownAcross()
    {
        // A resumed player was placed, not flown. If the first frame swept the line from wherever
        // the player happened to be to where the save puts them, it would fly them through every
        // marker in between on a journey nobody made — and quest 1 would complete itself on the
        // frame the save was read.
        //
        // The save is far enough out to put the exit marker between the origin and the player. No
        // shipping playthrough writes this one; it is the shape of a save, chosen so the phantom
        // journey would cross something.
        InMemorySaveProgressService saves = new()
        {
            Content = SaveGameSerializer.Serialize(new SaveGame
            {
                PlayerX = 0,
                PlayerY = 5000,
                Quests = [new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Active)],
            }),
        };

        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.Equal(new Position(0, 5000), Session.Player.Position);
        Assert.Empty(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, Assert.Single(Session.Quests.Active).Id);
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
        GameScreen screen = new(session, new QuestProximityWatcher(new BattleForceWorld()), NullLogger<GameScreen>.Instance);

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
        GameScreen screen = new(new ThrowingGameSession(), new QuestProximityWatcher(new BattleForceWorld()), logger);

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
}
