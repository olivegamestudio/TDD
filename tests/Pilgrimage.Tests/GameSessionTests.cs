namespace Pilgrimage.Tests;

public sealed class GameSessionTests
{
    static readonly Position PlayerStart = new(0, 0);
    static readonly QuestMarker Start = new(PlayerStart, 25);
    static readonly QuestMarker End = new(new Position(0, 1000), 50);

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public Position PlayerStart => GameSessionTests.PlayerStart;

        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    static ICampaign OneAutoStartingQuest() =>
        new TestCampaign(new QuestDefinition("quest-1", "Get out.", Start, End));

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(ICampaign? campaign = null) =>
        new(_saves, campaign ?? OneAutoStartingQuest());

    // ---- acceptance criterion 1: the quest auto starts on a new game launch ----

    [Fact]
    public async Task StartNewGame_PutsThePlayerAtTheCampaignStart()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(PlayerStart, session.Player.Position);
    }

    [Fact]
    public async Task StartNewGame_AutoStartsTheQuest_WithNoPlayerAction()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Quest quest = Assert.Single(session.Quests.Active);
        Assert.Equal("quest-1", quest.Id);
    }

    [Fact]
    public async Task StartNewGame_LeavesTheSessionReadyToUpdate()
    {
        GameSession session = CreateSession();
        Assert.False(session.IsReady);

        await session.StartNewGame();

        Assert.True(session.IsReady);
    }

    // ---- acceptance criterion 2: the quest is registered with the player state / save game ----

    [Fact]
    public async Task StartNewGame_RegistersEveryCampaignQuestInTheLog()
    {
        GameSession session = CreateSession(new TestCampaign(
            new QuestDefinition("quest-1", "Get out.", Start, End),
            new QuestDefinition("quest-2", "Later.", Start, End, AutoStarts: false)));

        await session.StartNewGame();

        Assert.Equal(["quest-1", "quest-2"], session.Quests.Quests.Select(quest => quest.Id));
    }

    [Fact]
    public async Task StartNewGame_WritesEveryQuestIntoTheSaveGame()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        SaveGame? saved = _saves.Saved;
        Assert.NotNull(saved);
        Assert.Equal([new QuestProgress("quest-1", QuestState.Active)], saved.Quests);
    }

    // ---- acceptance criterion 3: progress is saved ----

    [Fact]
    public async Task StartNewGame_SavesTheNewGame()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(1, _saves.SaveCount);
        Assert.NotNull(_saves.Saved);
    }

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
    public async Task SavesAutomatically_WhenAQuestCompletes()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();
        int savesAfterStart = _saves.SaveCount;

        session.Player.MoveTo(End.Position);
        session.Update(TimeSpan.FromSeconds(1));
        await session.PendingSave;

        Assert.True(_saves.SaveCount > savesAfterStart);
        Assert.Equal([new QuestProgress("quest-1", QuestState.Completed)], _saves.Saved!.Quests);
    }

    [Fact]
    public async Task DoesNotSaveOnEveryFrame_OnlyWhenSomethingChanges()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();
        int savesAfterStart = _saves.SaveCount;

        for (int frame = 0; frame < 100; frame++)
        {
            session.Player.MoveBy(0, 1);
            session.Update(TimeSpan.FromSeconds(1 / 60.0));
        }

        Assert.Equal(savesAfterStart, _saves.SaveCount);
    }

    // ---- acceptance criterion 4: the player moves forward and completes the quest ----

    [Fact]
    public async Task TheQuestStaysInProgress_WhileThePlayerIsShortOfTheEndMarker()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();

        for (int frame = 0; frame < 90; frame++)
        {
            session.Player.MoveBy(0, 10);          // 900 units forward, short of 1000
            session.Update(TimeSpan.FromSeconds(1 / 60.0));
        }

        Assert.Single(session.Quests.Active);
        Assert.Empty(session.Quests.Completed);
    }

    [Fact]
    public async Task TheQuestCompletes_WhenThePlayerMovesForwardToTheEndMarker()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();

        for (int frame = 0; frame < 100; frame++)
        {
            session.Player.MoveBy(0, 10);          // 1000 units forward, onto the end marker
            session.Update(TimeSpan.FromSeconds(1 / 60.0));
        }

        Quest quest = Assert.Single(session.Quests.Completed);
        Assert.Equal("quest-1", quest.Id);
        Assert.Empty(session.Quests.Active);
    }

    [Fact]
    public async Task RaisesQuestCompletedOnce_HoweverManyFramesFollow()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();
        int completed = 0;
        session.Quests.QuestCompleted += (_, _) => completed++;

        session.Player.MoveTo(End.Position);
        for (int frame = 0; frame < 10; frame++)
        {
            session.Update(TimeSpan.FromSeconds(1 / 60.0));
        }

        Assert.Equal(1, completed);
    }

    [Fact]
    public void Update_IsIgnored_BeforeTheGameHasStarted()
    {
        // Enter() kicks off an async load; frames can arrive before it finishes
        GameSession session = CreateSession();

        session.Update(TimeSpan.FromSeconds(1));

        Assert.Empty(session.Quests.Quests);
        Assert.Equal(0, _saves.SaveCount);
    }

    // ---- continuing from a save ----

    [Fact]
    public async Task Continue_StartsANewGame_WhenThereIsNoSave()
    {
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(PlayerStart, session.Player.Position);
        Assert.Single(session.Quests.Active);
    }

    [Fact]
    public async Task Continue_StartsANewGame_WhenTheSaveIsCorrupt()
    {
        _saves.Content = "not json at all";
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(PlayerStart, session.Player.Position);
        Assert.Single(session.Quests.Active);
    }

    [Fact]
    public async Task Continue_RestoresThePlayerPositionAndQuestProgress()
    {
        GameSession first = CreateSession();
        await first.StartNewGame();
        first.Player.MoveTo(new Position(0, 700));
        await first.Save();

        GameSession resumed = CreateSession();
        await resumed.Continue();

        Assert.Equal(new Position(0, 700), resumed.Player.Position);
        Assert.Single(resumed.Quests.Active);
        Assert.Empty(resumed.Quests.Completed);
    }

    [Fact]
    public async Task Continue_ThenMovingForward_CompletesTheResumedQuest()
    {
        GameSession first = CreateSession();
        await first.StartNewGame();
        first.Player.MoveTo(new Position(0, 700));
        await first.Save();

        GameSession resumed = CreateSession();
        await resumed.Continue();
        resumed.Player.MoveTo(End.Position);
        resumed.Update(TimeSpan.FromSeconds(1));
        await resumed.PendingSave;

        Assert.Single(resumed.Quests.Completed);
        Assert.Equal([new QuestProgress("quest-1", QuestState.Completed)], _saves.Saved!.Quests);
    }

    [Fact]
    public async Task Continue_DoesNotReplayACompletedQuest()
    {
        GameSession first = CreateSession();
        await first.StartNewGame();
        first.Player.MoveTo(End.Position);
        first.Update(TimeSpan.FromSeconds(1));
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
            PlayerX = 0,
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
}
