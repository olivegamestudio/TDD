using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

public sealed class GameSessionTests
{
    static readonly Position Start = new(0, 0);
    static readonly Position End = new(0, 1000);

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => Start;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    static QuestDefinition Quest(string id, bool autoStarts = true) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            autoStarts);

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(params string[] questIds) => CreateSession(_saves, questIds);

    static GameSession CreateSession(ISaveProgressService saves, params string[] questIds)
    {
        string[] ids = questIds.Length == 0 ? ["quest-1"] : questIds;

        return new GameSession(
            saves,
            new TestCampaign([.. ids.Select(id => Quest(id))]),
            new TestWorld([.. ids.Select(id => new QuestMarkers(id, Start, End))]));
    }

    // ---- starting a new game ----

    [Fact]
    public async Task StartNewGame_PutsThePlayerAtTheWorldStart()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(Start, session.Player.Position);
    }

    [Fact]
    public async Task StartNewGame_RegistersEveryCampaignQuest()
    {
        GameSession session = CreateSession("quest-1", "quest-2");

        await session.StartNewGame();

        Assert.Equal(["quest-1", "quest-2"], session.Quests.Quests.Select(quest => quest.Id));
    }

    [Fact]
    public async Task StartNewGame_DoesNotStartQuestsItself()
    {
        // proximity is the presentation's job; the session only holds and persists state
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Empty(session.Quests.Active);
        Assert.All(session.Quests.Quests, quest => Assert.Equal(QuestState.NotStarted, quest.State));
    }

    [Fact]
    public async Task StartNewGame_LeavesTheSessionReady()
    {
        GameSession session = CreateSession();
        Assert.False(session.IsReady);

        await session.StartNewGame();

        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task StartNewGame_SavesTheNewGame()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(1, _saves.SaveCount);
        Assert.Equal([new QuestProgress("quest-1", QuestState.NotStarted)], _saves.Saved!.Quests);
    }

    // ---- persisting progress ----

    [Fact]
    public async Task SavesThePlayerPosition()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();

        session.Player.MoveTo(new Position(3, 400));
        await session.Save();

        SaveGame? saved = _saves.Saved;
        Assert.NotNull(saved);
        Assert.Equal(3, saved.PlayerX);
        Assert.Equal(400, saved.PlayerY);
    }

    [Fact]
    public async Task SavesAutomatically_WhenAQuestStarts()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();
        int savesAfterStart = _saves.SaveCount;

        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.True(_saves.SaveCount > savesAfterStart);
        Assert.Equal([new QuestProgress("quest-1", QuestState.Active)], _saves.Saved!.Quests);
    }

    [Fact]
    public async Task SavesAutomatically_WhenAQuestCompletes()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();
        Quest quest = session.Quests.Find("quest-1")!;
        quest.Start();
        await session.PendingSave;
        int savesAfterQuestStarted = _saves.SaveCount;

        quest.Complete();
        await session.PendingSave;

        Assert.True(_saves.SaveCount > savesAfterQuestStarted);
        Assert.Equal([new QuestProgress("quest-1", QuestState.Completed)], _saves.Saved!.Quests);
    }

    [Fact]
    public async Task DoesNotSave_WhenThePlayerMerelyMoves()
    {
        // saving on every frame of travel would write constantly
        GameSession session = CreateSession();
        await session.StartNewGame();
        int savesAfterStart = _saves.SaveCount;

        for (int frame = 0; frame < 100; frame++)
        {
            session.Player.MoveBy(0, 1);
        }

        Assert.Equal(savesAfterStart, _saves.SaveCount);
    }

    // ---- continuing from a save ----

    [Fact]
    public async Task Continue_StartsANewGame_WhenThereIsNoSave()
    {
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(Start, session.Player.Position);
        Assert.Single(session.Quests.Quests);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task Continue_StartsANewGame_WhenTheSaveIsCorrupt()
    {
        _saves.Content = "not json at all";
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(Start, session.Player.Position);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task Continue_RestoresThePlayerPositionAndQuestProgress()
    {
        GameSession first = CreateSession();
        await first.StartNewGame();
        first.Quests.Find("quest-1")!.Start();
        first.Player.MoveTo(new Position(0, 700));
        await first.Save();

        GameSession resumed = CreateSession();
        await resumed.Continue();

        Assert.Equal(new Position(0, 700), resumed.Player.Position);
        Assert.Single(resumed.Quests.Active);
    }

    [Fact]
    public async Task Continue_DoesNotReplayACompletedQuest()
    {
        GameSession first = CreateSession();
        await first.StartNewGame();
        Quest quest = first.Quests.Find("quest-1")!;
        quest.Start();
        quest.Complete();
        await first.PendingSave;

        GameSession resumed = CreateSession();
        int started = 0;
        resumed.Quests.QuestStarted += (_, _) => started++;
        await resumed.Continue();

        Assert.Single(resumed.Quests.Completed);
        Assert.Equal(0, started);
    }

    [Fact]
    public async Task Continue_DoesNotWriteASaveWhenItOnlyReadsOne()
    {
        GameSession first = CreateSession();
        await first.StartNewGame();
        int savesAfterFirstGame = _saves.SaveCount;

        GameSession resumed = CreateSession();
        await resumed.Continue();

        Assert.Equal(savesAfterFirstGame, _saves.SaveCount);
    }

    [Fact]
    public async Task Continue_IgnoresAQuestInTheSaveThatIsNoLongerInTheCampaign()
    {
        // a save written by an older build must still load
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            PlayerY = 500,
            Quests =
            [
                new QuestProgress("quest-1", QuestState.Active),
                new QuestProgress("quest-removed", QuestState.Completed),
            ],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Single(session.Quests.Quests);
        Assert.Single(session.Quests.Active);
    }

    [Fact]
    public async Task Continue_StartsANewGame_WhenTheSaveHoldsAQuestStateThatIsNotAState()
    {
        // a hand-edited or corrupted save saying 99: the quest must be playable, not stuck
        _saves.Content = """
        { "PlayerX": 0, "PlayerY": 0, "Quests": [ { "QuestId": "quest-1", "State": 99 } ] }
        """;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Quest quest = Assert.Single(session.Quests.Quests);
        Assert.Equal(QuestState.NotStarted, quest.State);

        quest.Start();
        Assert.True(quest.IsActive);
        quest.Complete();
        Assert.True(quest.IsCompleted);
    }

    [Fact]
    public async Task Continue_StartsANewGame_WhenTheSaveHoldsANullQuestEntry()
    {
        // "Quests": null was already handled; a null *element* was not, and reached QuestLog.Restore
        // as a NullReferenceException that escaped Continue and left the game frozen
        _saves.Content = """{ "PlayerX": 0, "PlayerY": 0, "Quests": [ null ] }""";
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.NotNull(session.Quests.Find("quest-1"));
    }

    [Fact]
    public async Task Continue_StartsANewGame_WhenTheSaveHoldsAQuestEntryWithNoId()
    {
        // a quest entry naming no quest is not something this build ever wrote
        _saves.Content = """{ "PlayerX": 0, "PlayerY": 0, "Quests": [ { "State": "Active" } ] }""";
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.NotNull(session.Quests.Find("quest-1"));
    }

    [Fact]
    public async Task Continue_LeavesAQuestThatCanStart_WhenTheSaveHoldsAnUnreachablePosition()
    {
        // 1e400 does not throw; it overflows to Infinity and the save reads as valid. Every distance
        // from there is Infinity, so no proximity trigger can ever fire and no amount of flying
        // changes that — the same brick as a quest state outside the enum, through the position.
        _saves.Content = """
        { "PlayerX": 1e400, "PlayerY": 0, "Quests": [ { "QuestId": "quest-1", "State": "NotStarted" } ] }
        """;
        GameSession session = CreateSession();
        QuestProximityWatcher watcher = new(new TestWorld(new QuestMarkers("quest-1", Start, End)));

        await session.Continue();

        for (int frame = 0; frame < 500; frame++)
        {
            watcher.Update(session.Quests, session.Player.Position);
            session.Player.MoveBy(0, 10);
        }

        Quest quest = session.Quests.Find("quest-1")!;
        Assert.True(
            quest.IsActive || quest.IsCompleted,
            $"the quest never started: the player is at {session.Player.Position.X},{session.Player.Position.Y} and can never reach a marker");
    }

    // ---- a save the game refuses ----

    /// <summary>
    /// A save the serializer refuses, holding progress worth keeping: 700 units in, quest 1 done.
    /// The blank quest id is what gets it refused; everything beside it is real.
    /// </summary>
    const string RefusedSaveWithRealProgress = """
    { "PlayerX": 0, "PlayerY": 700,
      "Quests": [ { "QuestId": "quest-1", "State": "Completed" },
                  { "QuestId": "  ",      "State": "Active"    } ] }
    """;

    [Fact]
    public async Task Continue_KeepsARefusedSave_WhereItCanStillBeRecovered()
    {
        // #46: refusing a save is not the end of it, because the new game that replaces it is
        // written immediately. Without setting the old one aside first, every judgement the
        // serializer makes is final — and the bound added for defect 6 had to be drawn generously
        // precisely because a save refused in error could never be given back.
        _saves.Content = RefusedSaveWithRealProgress;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(RefusedSaveWithRealProgress, _saves.SetAsideContent);
    }

    [Fact]
    public async Task Continue_StillGivesAPlayableNewGame_WhenItSetsARefusedSaveAside()
    {
        // the recovery must not become a way for a file the game cannot read to block a start
        _saves.Content = RefusedSaveWithRealProgress;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.NotNull(session.Quests.Find("quest-1"));
        Assert.Equal(Start, session.Player.Position);
        Assert.True(session.IsSavingProgress);
    }

    [Fact]
    public async Task Continue_DoesNotSetAsideASaveItCouldRead()
    {
        // setting one aside is for the file that was refused, not for every launch — a save that
        // resumes fine must be left exactly where it is
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            PlayerY = 700,
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(0, _saves.SetAsideCount);
    }

    [Fact]
    public async Task Continue_SetsNothingAside_WhenThereWasNoSaveToBeginWith()
    {
        // a first launch is not a refusal, and should leave no trace suggesting it was
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(0, _saves.SetAsideCount);
        Assert.True(session.IsReady);
    }

    static FailingSaveProgressService UnmovableRefusedSave() =>
        new(setAsideError: new UnauthorizedAccessException("Access to the path is denied."))
        {
            Content = RefusedSaveWithRealProgress,
        };

    [Fact]
    public async Task Continue_StillGivesAPlayableGame_WhenARefusedSaveCannotBeSetAside()
    {
        // storage failures stay narrow: a file that will not move must not stop the game starting
        GameSession session = CreateSession(UnmovableRefusedSave());

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.NotNull(session.Quests.Find("quest-1"));
        Assert.Equal(Start, session.Player.Position);
    }

    [Fact]
    public async Task Continue_DoesNotWriteOverARefusedSave_ThatItCouldNotSetAside()
    {
        // The point of setting it aside is that the refusal may be wrong. If the copy cannot be
        // made, writing anyway destroys the file for exactly the reason the copy existed — so
        // writes are held back instead, the same answer this session already gives for a save it
        // could not read. The player still gets a game; it just does not persist over the old one.
        FailingSaveProgressService saves = UnmovableRefusedSave();
        GameSession session = CreateSession(saves);

        await session.Continue();
        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.False(session.IsSavingProgress);
        Assert.Equal(0, saves.SaveCount);
        Assert.Equal(RefusedSaveWithRealProgress, saves.Content);
    }

    [Fact]
    public async Task Continue_ReportsWhyARefusedSaveCouldNotBeSetAside()
    {
        // a session that has quietly stopped saving has to say why, or the next quest to complete
        // vanishes with no explanation anywhere
        GameSession session = CreateSession(UnmovableRefusedSave());

        await session.Continue();

        Assert.IsType<UnauthorizedAccessException>(session.SaveError);
    }

    // ---- a save that cannot be read ----

    static FailingSaveProgressService LockedSave() =>
        new(loadError: new IOException("The process cannot access the file."))
        {
            Content = SaveGameSerializer.Serialize(new SaveGame
            {
                PlayerY = 700,
                Quests = [new QuestProgress("quest-1", QuestState.Active)],
            }),
        };

    [Fact]
    public async Task Continue_LeavesAPlayableGame_WhenTheSaveCannotBeRead()
    {
        // the save is locked, not damaged — the player is still owed a game rather than a screen
        // where nothing ever happens again
        GameSession session = CreateSession(LockedSave());

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.NotNull(session.Quests.Find("quest-1"));
        Assert.Equal(Start, session.Player.Position);
    }

    [Fact]
    public async Task Continue_ReportsWhyTheSaveCouldNotBeRead()
    {
        GameSession session = CreateSession(LockedSave());

        await session.Continue();

        Assert.IsType<IOException>(session.SaveError);
    }

    [Fact]
    public async Task Continue_DoesNotWriteOverASaveItCouldNotRead()
    {
        // the save on disk may be perfectly intact; replacing it with the stand-in game would lose
        // a game that was never actually lost
        FailingSaveProgressService saves = LockedSave();
        string? saveOnDisk = saves.Content;
        GameSession session = CreateSession(saves);
        await session.Continue();

        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.False(session.IsSavingProgress);
        Assert.Equal(0, saves.SaveCount);
        Assert.Equal(saveOnDisk, saves.Content);
    }

    [Fact]
    public async Task Continue_SavesAgain_OnceTheSaveCanBeRead()
    {
        GameSession session = CreateSession(LockedSave());
        await session.Continue();
        Assert.NotNull(session.SaveError);

        GameSession recovered = CreateSession();
        await recovered.Continue();

        Assert.Null(recovered.SaveError);
        Assert.True(recovered.IsSavingProgress);
    }

    // ---- a save that cannot be written ----

    [Fact]
    public async Task StartNewGame_LeavesAPlayableGame_WhenTheSaveCannotBeWritten()
    {
        FailingSaveProgressService saves = new(saveError: new UnauthorizedAccessException("Access to the path is denied."));
        GameSession session = CreateSession(saves);

        await session.StartNewGame();

        Assert.True(session.IsReady);
        Assert.IsType<UnauthorizedAccessException>(session.SaveError);
    }

    [Fact]
    public async Task AFailedWrite_LeavesSavingOn_SoTheNextQuestTriesAgain()
    {
        // a write can fail for a moment; giving up on saving for the rest of the session would
        // cost the player far more than the one write that failed
        FailingSaveProgressService saves = new(saveError: new IOException("The disk is full."));
        GameSession session = CreateSession(saves);
        await session.StartNewGame();

        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.True(session.IsSavingProgress);
        Assert.IsType<IOException>(session.SaveError);
    }

    [Fact]
    public async Task ADefectDuringASave_IsNotSwallowedAsAStorageProblem()
    {
        // catching every exception here would bury real bugs behind "could not save"
        FailingSaveProgressService saves = new(saveError: new InvalidOperationException("a defect"));
        GameSession session = CreateSession(saves);

        await Assert.ThrowsAsync<InvalidOperationException>(session.StartNewGame);
    }
}
