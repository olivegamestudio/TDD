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

        // a test world that names no places: whatever enters it enters where it starts
        public Position Introduce(Ship ship, string location) => PlayerStart;

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
            new TestRoster(),
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

    // ---- a junk quest entry beside real progress ----

    [Fact]
    public async Task Continue_KeepsTheProgressSavedBesideAnEntryNamingNoQuest()
    {
        // #44's report, pinned verbatim. The blank id names nothing the campaign registered, so
        // there is nothing to apply it to; the completed quest and the 700 units beside it are
        // what the file is actually worth. Refusing the file over the blank line took both, and
        // took them for good — a refused save is set aside and played over.
        _saves.Content = """
        { "PlayerX": 0, "PlayerY": 700,
          "Quests": [ { "QuestId": "quest-1", "State": "Completed" },
                      { "QuestId": "  ",      "State": "Active"    } ] }
        """;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Equal(700, session.Player.Position.Y);
        Assert.True(session.Quests.Find("quest-1")!.IsCompleted);
        Assert.Null(_saves.SetAsideContent);            // the file was read, not judged damaged
        Assert.Equal(0, _saves.SaveCount);
    }

    [Fact]
    public async Task Continue_StartsANewGame_WhenTheSaveHoldsAQuestEntryThatIsNotThere()
    {
        // The other half of the same decision, and the one that used to soft-lock: a null entry
        // reached QuestLog.Restore and threw onto a task nothing awaits, so the session never
        // became ready and the game screen waited on it forever. A null entry is not drift — no
        // build wrote it — so the file is refused and a new game starts over it.
        _saves.Content = """
        { "PlayerX": 0, "PlayerY": 700,
          "Quests": [ { "QuestId": "quest-1", "State": "Completed" }, null ] }
        """;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Equal(Start, session.Player.Position);
        Assert.Equal(QuestState.NotStarted, session.Quests.Find("quest-1")!.State);
    }

    [Fact]
    public async Task Continue_StartsANewGame_WhenTheSaveHoldsAQuestEntryWithNoIdAtAll()
    {
        // `{ "State": "Active" }` deserialises to a QuestProgress whose QuestId is null, which came
        // out of the dictionary lookup as ArgumentNullException('key') — the same soft-lock by a
        // different route. Dropped at the serializer now, so the file loads.
        _saves.Content = """
        { "PlayerX": 0, "PlayerY": 700,
          "Quests": [ { "QuestId": "quest-1", "State": "Completed" }, { "State": "Active" } ] }
        """;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Equal(700, session.Player.Position.Y);
        Assert.True(session.Quests.Find("quest-1")!.IsCompleted);
    }

    // ---- a save the game refuses ----

    /// <summary>
    /// A save holding 700 units of real progress that this build refuses, because a quest state of
    /// 99 is not a state. QA's reproduction on #46, and the case that matters: the file is not
    /// gibberish, it is progress written in a shape this build will not take — exactly what a later
    /// build, or a less strict boundary, might read perfectly well.
    /// </summary>
    const string RefusedSave = """
    { "PlayerX": 0, "PlayerY": 700, "Quests": [ { "QuestId": "quest-1", "State": 99 } ] }
    """;

    [Fact]
    public async Task Continue_SetsARefusedSaveAside_RatherThanWritingOverIt()
    {
        // the whole issue: refusing a save is a judgement, and every judgement was final while the
        // new game landed on top of the file. 700 units of progress used to become a fresh save.
        _saves.Content = RefusedSave;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(RefusedSave, _saves.SetAsideContent);
    }

    [Fact]
    public async Task Continue_StillStartsAndSavesANewGame_AfterSettingARefusedSaveAside()
    {
        // recovering the old file must not cost the player the new game; the save that was in the
        // way is gone, so writing is safe again
        _saves.Content = RefusedSave;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.True(session.IsSavingProgress);
        Assert.Equal(Start, session.Player.Position);

        // and what landed on disk is the new game, not the refused one
        SaveGame written = Assert.IsType<SaveGame>(_saves.Saved);
        Assert.Equal(0, written.PlayerY);
    }

    [Fact]
    public async Task Continue_SetsNothingAside_WhenThereIsNoSaveAtAll()
    {
        // a first-time player has nothing to preserve, and must not be left an empty file that
        // looks like a recoverable one
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Null(_saves.SetAsideContent);
        Assert.Equal(1, _saves.SaveCount);
    }

    [Fact]
    public async Task Continue_SetsNothingAside_WhenTheSaveIsBlank()
    {
        // there is nothing in a blank file for a later build to read, so keeping it would only be
        // a file the player has to wonder about
        _saves.Content = "   ";
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Null(_saves.SetAsideContent);
        Assert.True(session.IsSavingProgress);
    }

    [Fact]
    public async Task Continue_LeavesTheRefusedSaveWhereItIs_WhenItCannotBeSetAside()
    {
        // the fallback that decides what this is worth. If the file cannot be moved out of the
        // way, writing the new game over it would destroy the very thing being protected — so the
        // player gets a playable game and nothing is written, the same answer a locked save gets.
        FailingSaveProgressService saves = new(setAsideError: new IOException("The process cannot access the file."))
        {
            Content = RefusedSave,
        };
        GameSession session = CreateSession(saves);

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Equal(RefusedSave, saves.Content);
        Assert.Equal(0, saves.SaveCount);
        Assert.False(session.IsSavingProgress);
        Assert.IsType<IOException>(session.SaveError);
    }

    [Fact]
    public async Task Continue_KeepsThatGamePlayable_WhenTheRefusedSaveCannotBeSetAside()
    {
        // "playable" has to mean playable, not merely constructed: the quests are there and they
        // still run, they simply are not written down
        FailingSaveProgressService saves = new(setAsideError: new IOException("locked"))
        {
            Content = RefusedSave,
        };
        GameSession session = CreateSession(saves);
        await session.Continue();

        Quest quest = Assert.Single(session.Quests.Quests);
        quest.Start();
        await session.PendingSave;

        Assert.True(quest.IsActive);
        Assert.Equal(0, saves.SaveCount);
        Assert.Equal(RefusedSave, saves.Content);
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
