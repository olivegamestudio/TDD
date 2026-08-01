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
public sealed class GameScreenTests : GameplayTestBase
{
    IGameScreen GameScreen => Resolve<IGameScreen>();

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
    public async Task ADamagedSave_DoesNotSilentlyFreezeTheGameScreen()
    {
        // The same freeze as above, reached through a damaged file rather than a locked one: a null
        // entry in the quest array threw out of Continue, so the session never became ready and the
        // logged error was the only sign anything had happened.
        GameSession session = new(
            new InMemorySaveProgressService { Content = """{ "PlayerX": 0, "PlayerY": 0, "Quests": [ null ] }""" },
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
