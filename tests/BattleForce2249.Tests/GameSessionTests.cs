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

    static readonly ShipModel Starter = new("starter", "ship1", new ShipHandling(180, 0.9, 2.5));

    static readonly ShipModel Earned = new("earned", "ship2", new ShipHandling(240, 0.8, 2.2));

    /// <summary>
    /// A shipyard stocking exactly what a test wants. The first ship is the one a new game awards.
    /// </summary>
    sealed class TestShipyard(params ShipModel[] ships) : IShipyard
    {
        public ShipModel StartingShip => Ships[0];

        public IReadOnlyList<ShipModel> Ships { get; } = ships;

        public ShipModel? Find(string? id) =>
            id is null ? null : Ships.FirstOrDefault(ship => ship.Id == id);
    }

    static QuestDefinition Quest(string id, bool autoStarts = true) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            autoStarts);

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(params string[] questIds) =>
        CreateSession(new TestShipyard(Starter), questIds);

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
    public async Task StartNewGame_AwardsThePlayerTheStartingShip()
    {
        GameSession session = CreateSession();
        Assert.Null(session.Player.Ship);

        await session.StartNewGame();

        Assert.Same(Starter, session.Player.Ship);
    }

    [Fact]
    public async Task StartNewGame_TakesBackAShipEarnedInAPreviousGame()
    {
        // a new game is a new game: starting one over the top of a finished one must not leave
        // the player flying whatever the last run ended in
        GameSession session = CreateSession(new TestShipyard(Starter, Earned));
        await session.StartNewGame();
        session.Player.GiveShip(Earned);

        await session.StartNewGame();

        Assert.Same(Starter, session.Player.Ship);
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
    public async Task SavesTheShipThePlayerIsFlying()
    {
        // pillar 4: the persistent record survives. A ship the player has earned is part of that
        // record, not something re-derived from the campaign every time the game loads.
        GameSession session = CreateSession(new TestShipyard(Starter, Earned));
        await session.StartNewGame();

        session.Player.GiveShip(Earned);
        await session.Save();

        Assert.Equal(Earned.Id, _saves.Saved!.ShipId);
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
        GameSession first = CreateSession(new TestShipyard(Starter, Earned));
        await first.StartNewGame();
        first.Player.GiveShip(Earned);
        await first.Save();

        GameSession resumed = CreateSession(new TestShipyard(Starter, Earned));
        await resumed.Continue();

        Assert.Same(Earned, resumed.Player.Ship);
    }

    [Fact]
    public async Task Continue_AwardsTheStartingShip_WhenTheSavePreDatesShipsBeingRecorded()
    {
        // an older save has no ship id at all, and must still load
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            PlayerY = 500,
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(Starter, session.Player.Ship);
    }

    [Fact]
    public async Task Continue_AwardsTheStartingShip_WhenTheSaveNamesAShipThisBuildNoLongerHas()
    {
        // drift is tolerated in both directions, the same as the quest list: being demoted a hull
        // is a better outcome than a save that will not load
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            ShipId = "a-ship-from-a-later-build",
            Quests = [new QuestProgress("quest-1", QuestState.NotStarted)],
        });
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(Starter, session.Player.Ship);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task Continue_AwardsTheStartingShip_WhenThereIsNoSave()
    {
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(Starter, session.Player.Ship);
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
