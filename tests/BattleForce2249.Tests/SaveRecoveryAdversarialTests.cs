using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// The awkward corners of recovering from a save the game cannot read. The happy paths and the two
/// reported defects are covered next door in <see cref="GameSessionTests"/>; these are the cases
/// that go looking for the recovery's edges — a lock that clears, a lock that arrives, a failure
/// that is not the storage's fault, and a quest finishing while the save is held back.
/// </summary>
public class SaveRecoveryAdversarialTests
{
    static readonly Position Start = new(0, 0);
    static readonly Position End = new(0, 1000);

    static QuestDefinition Quest(string id) =>
        new(id, $"Quest {id}", new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50));

    sealed class Campaign : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests => [Quest("quest-1")];
    }

    sealed class World : IWorld
    {
        public Position PlayerStart => Start;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } =
            [new QuestMarkers("quest-1", Start, End)];
    }

    static GameSession CreateSession(ISaveProgressService saves) =>
        new(saves, new Campaign(), new World());

    /// <summary>
    /// A save service whose read can be locked and unlocked while the game is running, which is
    /// what cloud sync or antivirus actually does. The fixed <see cref="FailingSaveProgressService"/>
    /// can only be one or the other for its whole life, so it cannot show a session recovering.
    /// </summary>
    sealed class LockableSaveProgressService : ISaveProgressService
    {
        public string? Content { get; set; }

        public Exception? LoadError { get; set; }

        public int SaveCount { get; private set; }

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load() =>
            LoadError is null ? Task.FromResult(Content) : throw LoadError;

        public Task Save(string content)
        {
            Content = content;
            SaveCount++;
            return Task.CompletedTask;
        }

        /// <summary>
        /// What a set-aside moved out of the way, so a test can show a refused save survived the
        /// new game that replaced it.
        /// </summary>
        public string? SetAsideContent { get; private set; }

        public Task SetAside()
        {
            if (Content is not null)
            {
                SetAsideContent = Content;
                Content = null;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>A real save, part-way through quest 1.</summary>
    static string SaveInProgress() => SaveGameSerializer.Serialize(new SaveGame
    {
        PlayerY = 700,
        Quests = [new QuestProgress("quest-1", QuestState.Active)],
    });

    [Fact]
    public async Task ASessionThatCouldNotReadTheSave_RecoversWhenTheLockClears()
    {
        // The shipped test for this used two separate sessions, which cannot show a session
        // recovering — only that a fresh one starts clean. The player who backs out to the menu and
        // presses continue again once cloud sync has let go gets the same session, not a new one.
        LockableSaveProgressService saves = new()
        {
            Content = SaveInProgress(),
            LoadError = new IOException("locked"),
        };
        GameSession session = CreateSession(saves);
        await session.Continue();
        Assert.NotNull(session.SaveError);
        Assert.False(session.IsSavingProgress);

        // the lock clears, and the player resumes again on the very same session
        saves.LoadError = null;
        await session.Continue();

        Assert.Null(session.SaveError);
        Assert.True(session.IsSavingProgress);
        Assert.Equal(700, session.Player.Position.Y);
        Assert.True(session.Quests.Find("quest-1")!.IsActive);
    }

    [Fact]
    public async Task ASessionThatWasSaving_StopsSaving_WhenALaterReadCannotBeDone()
    {
        // The dangerous direction: a session that has already been saving must not carry that
        // permission into a game standing in for a save it could not read, or the stand-in
        // overwrites the very save being protected.
        LockableSaveProgressService saves = new() { Content = SaveInProgress() };
        GameSession session = CreateSession(saves);
        await session.Continue();
        Assert.True(session.IsSavingProgress);

        // the same session resumes again, and this time the file is locked
        saves.LoadError = new IOException("locked");
        string? saveOnDisk = saves.Content;
        await session.Continue();

        Assert.False(session.IsSavingProgress);

        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.Equal(0, saves.SaveCount);
        Assert.Equal(saveOnDisk, saves.Content);
    }

    [Fact]
    public async Task AQuestCompleting_DoesNotWriteOverASaveThatCouldNotBeRead()
    {
        // The shipped test holds the line for a quest *starting*. Completing raises a different
        // event through a different subscription, so it is worth its own case.
        FailingSaveProgressService saves = new(loadError: new IOException("locked"))
        {
            Content = SaveInProgress(),
        };
        string? saveOnDisk = saves.Content;
        GameSession session = CreateSession(saves);
        await session.Continue();

        Quest quest = session.Quests.Find("quest-1")!;
        quest.Start();
        await session.PendingSave;
        quest.Complete();
        await session.PendingSave;

        Assert.True(quest.IsCompleted);
        Assert.Equal(0, saves.SaveCount);
        Assert.Equal(saveOnDisk, saves.Content);
    }

    [Theory]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task EveryWayStorageGetsInTheWayOfAReadIsRecoveredFrom(Type errorType)
    {
        // FileNotFoundException and DirectoryNotFoundException derive from IOException; a save
        // deleted between the existence check and the read reaches Continue as one of these.
        Exception error = (Exception)Activator.CreateInstance(errorType)!;
        GameSession session = CreateSession(new FailingSaveProgressService(loadError: error));

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.NotNull(session.Quests.Find("quest-1"));
    }

    [Fact]
    public async Task ADefectDuringAReadIsNotSwallowedAsAStorageProblem()
    {
        // The write side pins this; the read side did not. Catching wider than storage here would
        // turn every bug in the load path into a silent new game.
        GameSession session = CreateSession(
            new FailingSaveProgressService(loadError: new InvalidOperationException("a defect")));

        await Assert.ThrowsAsync<InvalidOperationException>(session.Continue);
    }

    [Fact]
    public async Task AReadFailure_DoesNotLeaveTheGameHoldingTheSavedPosition()
    {
        // Half-applying the save would be worse than not applying it: the player would resume at
        // the saved position with no quest progress, standing past a trigger that can never fire.
        FailingSaveProgressService saves = new(loadError: new IOException("locked"))
        {
            Content = SaveInProgress(),
        };
        GameSession session = CreateSession(saves);

        await session.Continue();

        Assert.Equal(Start, session.Player.Position);
        Assert.Equal(QuestState.NotStarted, session.Quests.Find("quest-1")!.State);
    }

    [Fact]
    public async Task ASaveHoldingTheSameQuestTwice_TakesTheLastStateRatherThanThrowing()
    {
        // A hand-edited or merged save can name a quest twice. It must not throw on the load path,
        // because throwing there is the freeze this whole change exists to remove.
        FailingSaveProgressService saves = new()
        {
            Content = """
            {
              "PlayerX": 0, "PlayerY": 900,
              "Quests": [
                { "QuestId": "quest-1", "State": "Active" },
                { "QuestId": "quest-1", "State": "Completed" }
              ]
            }
            """,
        };
        GameSession session = CreateSession(saves);

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.True(session.Quests.Find("quest-1")!.IsCompleted);
    }

    [Fact]
    public async Task AQuestAddedSinceTheSaveWasWritten_IsStillPlayable()
    {
        // QuestLog.Restore documents that a quest the save does not mention "simply starts from the
        // beginning". Check that is true in the session, not just in the log.
        FailingSaveProgressService saves = new()
        {
            Content = """{ "PlayerX": 0, "PlayerY": 0, "Quests": [] }""",
        };
        GameSession session = CreateSession(saves);

        await session.Continue();

        Quest quest = session.Quests.Find("quest-1")!;
        Assert.Equal(QuestState.NotStarted, quest.State);
        quest.Start();
        quest.Complete();
        Assert.True(quest.IsCompleted);
    }

    [Fact]
    public async Task AnUnreadableSave_LeavesQuest1CompletableByPlaying()
    {
        // End to end, through the watcher that drives quests in the real game: the stand-in game is
        // not a stub, it is a game — the player can fly it from the start marker to the exit.
        FailingSaveProgressService saves = new(loadError: new IOException("locked"))
        {
            Content = SaveInProgress(),
        };
        string? saveOnDisk = saves.Content;
        GameSession session = CreateSession(saves);
        QuestProximityWatcher watcher = new(new World());

        await session.Continue();
        watcher.Update(session.Quests, session.Player.Position);
        Assert.True(session.Quests.Find("quest-1")!.IsActive);

        for (int frame = 0; frame < 200 && !session.Quests.Find("quest-1")!.IsCompleted; frame++)
        {
            session.Player.MoveBy(0, 10);
            watcher.Update(session.Quests, session.Player.Position);
        }

        await session.PendingSave;
        Assert.True(session.Quests.Find("quest-1")!.IsCompleted);
        Assert.Equal(saveOnDisk, saves.Content);
    }
}
