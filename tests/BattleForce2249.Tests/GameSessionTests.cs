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

    /// <summary>
    /// A yard with two ships, so the tests can tell "the ship the save named" apart from "the ship
    /// a new game awards" — with only one ship both answers look the same.
    /// </summary>
    sealed class TestShipYard : IShipYard
    {
        public static readonly Ship Starter =
            new("starter", "starter-art", new ShipHandling(Acceleration: 100, Drag: 1, TurnRate: 2));

        public static readonly Ship Earned =
            new("earned", "earned-art", new ShipHandling(Acceleration: 300, Drag: 1, TurnRate: 3));

        public Ship StartingShip => Starter;

        public Ship? Find(string shipId) =>
            new[] { Starter, Earned }.FirstOrDefault(ship => ship.Id == shipId);
    }

    static QuestDefinition Quest(string id, bool autoStarts = true) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            autoStarts);

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(params string[] questIds)
    {
        string[] ids = questIds.Length == 0 ? ["quest-1"] : questIds;

        return new GameSession(
            _saves,
            new TestCampaign([.. ids.Select(id => Quest(id))]),
            new TestWorld([.. ids.Select(id => new QuestMarkers(id, Start, End))]),
            new TestShipYard());
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

    // ---- the ship a new game awards ----

    [Fact]
    public async Task StartNewGame_AwardsThePlayerTheStartingShip()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Same(TestShipYard.Starter, session.Player.Ship);
    }

    [Fact]
    public async Task StartNewGame_AwardsTheShipBeforeTheSessionIsReady()
    {
        // nothing flies or draws the player until the session says it is ready, so by that point
        // there has to be a ship — a ready session with no ship is a player flying nothing
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.True(session.IsReady);
        Assert.NotNull(session.Player.Ship);
    }

    [Fact]
    public async Task StartNewGame_WritesTheAwardedShipIntoTheSave()
    {
        GameSession session = CreateSession();

        await session.StartNewGame();

        Assert.Equal(TestShipYard.Starter.Id, _saves.Saved!.ShipId);
    }

    [Fact]
    public async Task SavesTheShipThePlayerIsFlying_NotTheOneTheyStartedWith()
    {
        GameSession session = CreateSession();
        await session.StartNewGame();

        session.Player.Award(TestShipYard.Earned);
        await session.Save();

        Assert.Equal(TestShipYard.Earned.Id, _saves.Saved!.ShipId);
    }

    [Fact]
    public async Task SavesTheShipByIdentifier_NotItsContent()
    {
        // the ship is content: a later build is free to rebalance it or redraw it, and every saved
        // game gets the change. Writing the ship whole would freeze it at the day it was written.
        GameSession session = CreateSession();

        await session.StartNewGame();

        string content = _saves.Content!;
        Assert.Contains(TestShipYard.Starter.Id, content);
        Assert.DoesNotContain(TestShipYard.Starter.AssetKey, content);
        Assert.DoesNotContain(nameof(Ship.Handling), content);
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
    public async Task Continue_RestoresTheShipTheSaveWasWrittenWith()
    {
        // pillar 4: what the player owns is persistent record, and a reload gives it back
        GameSession first = CreateSession();
        await first.StartNewGame();
        first.Player.Award(TestShipYard.Earned);
        await first.Save();

        GameSession resumed = CreateSession();
        await resumed.Continue();

        Assert.Same(TestShipYard.Earned, resumed.Player.Ship);
    }

    [Fact]
    public async Task Continue_AwardsTheStartingShip_WhenThereIsNoSave()
    {
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(TestShipYard.Starter, session.Player.Ship);
    }

    [Fact]
    public async Task Continue_AwardsTheStartingShip_WhenTheSaveNamesAShipThisBuildDoesNotHave()
    {
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame { ShipId = "a-hull-we-dropped" });
        GameSession session = CreateSession();

        await session.Continue();

        // a downgraded player is a better outcome than a grounded one
        Assert.Same(TestShipYard.Starter, session.Player.Ship);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task Continue_AwardsTheStartingShip_ForASaveWrittenBeforeShipsWereRecorded()
    {
        // an older save has no ship in it at all; it must still load and still be flyable
        _saves.Content = """
            {
              "PlayerX": 0,
              "PlayerY": 500,
              "Quests": [ { "QuestId": "quest-1", "State": "Active" } ]
            }
            """;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Same(TestShipYard.Starter, session.Player.Ship);
        Assert.Equal(new Position(0, 500), session.Player.Position);
        Assert.Single(session.Quests.Active);
    }

    [Fact]
    public async Task Continue_RestoresThePositionAndTheShipIndependently()
    {
        // being awarded a ship must not move the player back to the world start
        GameSession first = CreateSession();
        await first.StartNewGame();
        first.Player.Award(TestShipYard.Earned);
        first.Player.MoveTo(new Position(12, 340));
        await first.Save();

        GameSession resumed = CreateSession();
        await resumed.Continue();

        Assert.Same(TestShipYard.Earned, resumed.Player.Ship);
        Assert.Equal(new Position(12, 340), resumed.Player.Position);
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
