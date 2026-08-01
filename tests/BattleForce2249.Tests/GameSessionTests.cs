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

    static readonly Ship Starter = new("starter", new ShipHandling(180, 0.9, 2.5), "ship1");

    static readonly Ship Earned = new("earned", new ShipHandling(240, 0.8, 3), "ship2");

    /// <summary>
    /// Two hulls, so restoring a saved ship is a real choice rather than the only ship there is.
    /// </summary>
    sealed class TestShipyard : IShipyard
    {
        public IReadOnlyList<Ship> Ships { get; } = [Starter, Earned];

        public Ship Starting => Starter;

        public Ship? Find(string id) => Ships.FirstOrDefault(ship => ship.Id == id);
    }

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(params string[] questIds)
    {
        string[] ids = questIds.Length == 0 ? ["quest-1"] : questIds;

        return new GameSession(
            _saves,
            new TestCampaign([.. ids.Select(id => Quest(id))]),
            new TestWorld([.. ids.Select(id => new QuestMarkers(id, Start, End))]),
            new TestShipyard());
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
    public async Task StartNewGame_GivesThePlayerTheStartingShip()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Same(Starter, session.Player.Ship);
    }

    [Fact]
    public async Task StartNewGame_TakesBackAShipEarnedInAPreviousGame()
    {
        // starting over starts over: a new game is not a way to keep the last one's hull
        GameSession session = CreateSession();
        await session.StartNewGame();
        session.Player.Award(Earned);

        await session.StartNewGame();

        Assert.Same(Starter, session.Player.Ship);
    }

    [Fact]
    public async Task StartNewGame_SavesTheShipItAwarded()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(Starter.Id, _saves.Saved!.ShipId);
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
    public async Task Continue_PutsThePlayerBackInTheShipTheySaved()
    {
        // pillar 4: the persistent record survives, so a hull earned in the last session is not
        // quietly taken away by a reload
        GameSession first = CreateSession();
        await first.StartNewGame();
        first.Player.Award(Earned);
        await first.Save();

        GameSession resumed = CreateSession();
        await resumed.Continue();

        Assert.Same(Earned, resumed.Player.Ship);
    }

    [Fact]
    public async Task Continue_GivesThePlayerTheStartingShip_WhenThereIsNoSave()
    {
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(Starter, session.Player.Ship);
    }

    [Fact]
    public async Task Continue_FallsBackToTheStartingShip_WhenTheSaveNamesAShipThisBuildNoLongerHas()
    {
        // a save the build has drifted from must still load, and the player must still have
        // something to fly — the same tolerance the quest list gets
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            ShipId = "a-hull-from-a-later-episode",
            Quests = [new QuestProgress("quest-1", QuestState.NotStarted)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(Starter, session.Player.Ship);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task Continue_GivesThePlayerTheStartingShip_WhenTheSaveRecordsNoShipAtAll()
    {
        // a save written before ships were recorded still loads, and the player flies the hull
        // that build would have given them
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            PlayerY = 700,
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(Starter, session.Player.Ship);
        Assert.Equal(new Position(0, 700), session.Player.Position);
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
}
