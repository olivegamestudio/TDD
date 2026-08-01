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

    static readonly ShipHandling TestHandling = new(Acceleration: 100, Drag: 1, TurnRate: 1);

    static readonly Ship Starter = new("starter", "starter-graphic", TestHandling);

    static readonly Ship Earned = new("earned", "earned-graphic", TestHandling);

    sealed class TestShipyard(params Ship[] ships) : IShipyard
    {
        public IReadOnlyList<Ship> Ships { get; } = ships.Length == 0 ? [Starter, Earned] : ships;

        public Ship StartingShip => Ships[0];

        public Ship? Find(string? id) =>
            string.IsNullOrWhiteSpace(id) ? null : Ships.FirstOrDefault(ship => ship.Id == id);
    }

    static QuestDefinition Quest(string id, bool autoStarts = true) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            autoStarts);

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(params string[] questIds) => CreateSession(new TestShipyard(), questIds);

    GameSession CreateSession(IShipyard shipyard, params string[] questIds)
    {
        string[] ids = questIds.Length == 0 ? ["quest-1"] : questIds;

        return new GameSession(
            _saves,
            new TestCampaign([.. ids.Select(id => Quest(id))]),
            new TestWorld([.. ids.Select(id => new QuestMarkers(id, Start, End))]),
            shipyard);
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

    // ---- the ship the player is given ----

    [Fact]
    public async Task StartNewGame_AwardsThePlayerTheStartingShip()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(Starter, session.Ship);
    }

    [Fact]
    public void ASessionThatHasNotStarted_ReportsTheShipANewGameWouldAward()
    {
        // nothing should have to handle a player with no ship at all, so there is no such state
        GameSession session = CreateSession();

        Assert.Equal(Starter, session.Ship);
    }

    [Fact]
    public async Task StartNewGame_WritesTheAwardedShipIntoTheSave()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(Starter.Id, _saves.Saved!.ShipId);
    }

    [Fact]
    public async Task StartNewGame_AwardsTheStartingShipAgain_AfterAnEarnedOneWasResumed()
    {
        // a new game is a new game: it must not inherit the ship the last save was flying
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame { ShipId = Earned.Id });
        GameSession session = CreateSession();
        await session.Continue();
        Assert.Equal(Earned, session.Ship);

        await session.StartNewGame();

        Assert.Equal(Starter, session.Ship);
    }

    [Fact]
    public async Task Continue_RestoresTheShipTheSaveWasFlying()
    {
        // pillar 4: the ship is something the player has, not something re-derived on every load
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            ShipId = Earned.Id,
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(Earned, session.Ship);
    }

    [Fact]
    public async Task Continue_FallsBackToTheStartingShip_WhenTheSaveNamesAShipTheGameNoLongerHas()
    {
        // losing a hull must not cost the player the rest of the save
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            PlayerY = 700,
            ShipId = "a-ship-this-build-does-not-have",
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(Starter, session.Ship);
        Assert.Equal(new Position(0, 700), session.Player.Position);
        Assert.Single(session.Quests.Active);
    }

    [Fact]
    public async Task Continue_FallsBackToTheStartingShip_ForASaveWrittenBeforeShipsWereRecorded()
    {
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            PlayerY = 500,
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(Starter, session.Ship);
    }

    [Fact]
    public async Task TheAwardedShipSurvivesASaveAndAReload()
    {
        GameSession first = CreateSession(new TestShipyard(Earned, Starter));
        await first.StartNewGame();

        GameSession resumed = CreateSession(new TestShipyard(Starter, Earned));
        await resumed.Continue();

        Assert.Equal(Earned, resumed.Ship);
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
}
