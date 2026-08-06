using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers how a session comes by the character it plays and the ship it flies: that a game always
/// has both, that a new game inherits neither, and that the world is what decides where the ship
/// is put down.
/// </summary>
public sealed class GameSessionProvisioningTests
{
    static readonly Position Start = new(0, 0);
    static readonly Position End = new(0, 1000);

    const string QuestId = "quest-1";

    sealed class TestCampaign : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests =>
        [
            new QuestDefinition(
                QuestId,
                "Title of quest-1",
                new QuestTrigger(QuestTriggerKind.Proximity, 25),
                new QuestTrigger(QuestTriggerKind.Proximity, 50)),
        ];
    }

    /// <summary>
    /// A world that records what it was asked to place and where, so a test can see the seam being
    /// used rather than only its result.
    /// </summary>
    sealed class RecordingWorld(Position placeAt) : IWorld
    {
        public Position PlayerStart => new(-1, -1);

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } =
            [new QuestMarkers(QuestId, Start, End)];

        // nobody stands in these worlds; what they are for is quests and saves
        public IReadOnlyList<Npc> Npcs { get; } = [];

        public Ship? Introduced { get; private set; }

        public string? IntroducedAt { get; private set; }

        public int Introductions { get; private set; }

        public Position Introduce(Ship ship, string location)
        {
            ArgumentNullException.ThrowIfNull(ship);
            ArgumentException.ThrowIfNullOrWhiteSpace(location);

            Introduced = ship;
            IntroducedAt = location;
            Introductions++;

            return placeAt;
        }
    }

    readonly InMemorySaveProgressService _saves = new();
    readonly TestRoster _roster = new();

    GameSession CreateSession(IWorld world) => new(_saves, new TestCampaign(), _roster, world);

    [Fact]
    public void ASessionAlwaysHasACharacterAndAShip_EvenBeforeAGameHasStarted()
    {
        // frames arrive while a save is still being read. Nothing should be driving the session
        // then — but a screen that has to null check the ship for the moment before a game begins
        // carries that check for the whole of the run.
        GameSession session = CreateSession(new RecordingWorld(Start));

        Assert.NotNull(session.Character);
        Assert.NotNull(session.Ship);
    }

    [Fact]
    public async Task StartNewGame_PutsTheCharacterInAShipBuiltFromTheirOwnHull()
    {
        GameSession session = CreateSession(new RecordingWorld(Start));

        await session.StartNewGame();

        Assert.Same(session.Ship, session.Character.Ship);
        Assert.Same(session.Character.Template.Ship, session.Ship.Profile);
        Assert.Same(TestRoster.Profile.Handling, session.Ship.Handling);
    }

    [Fact]
    public async Task StartNewGame_PlaysAsTheCharacterTheRosterStartsWith()
    {
        GameSession session = CreateSession(new RecordingWorld(Start));

        await session.StartNewGame();

        Assert.Equal(TestRoster.TemplateId, session.Character.Template.Id);
    }

    [Fact]
    public async Task StartNewGame_AsksTheWorldToPlaceTheShipWhereTheCharacterStarts()
    {
        // content names the place; the world says where that is. A coordinate in content would be a
        // number that has to be kept in step with the world by hand.
        RecordingWorld world = new(new Position(40, 90));
        GameSession session = CreateSession(world);

        await session.StartNewGame();

        Assert.Same(session.Ship, world.Introduced);
        Assert.Equal(TestRoster.StartLocation, world.IntroducedAt);
        Assert.Equal(new Position(40, 90), session.Player.Position);
    }

    [Fact]
    public async Task ANewGame_FliesANewShipRatherThanTheOneTheLastGameLeftBehind()
    {
        // what makes a resumed or restarted game begin at rest facing forward. Nothing resets the
        // ship to get this: a new ship has never been anywhere.
        GameSession session = CreateSession(new RecordingWorld(Start));
        await session.StartNewGame();

        Ship flown = session.Ship;
        flown.Movement.Update(session.Player, new ShipControls(thrust: 1, turn: 1), TimeSpan.FromSeconds(2));

        await session.StartNewGame();

        Assert.NotSame(flown, session.Ship);
        Assert.Equal(Velocity.Stationary, session.Ship.Movement.Velocity);
        Assert.Equal(0, session.Ship.Movement.Heading);
    }

    [Fact]
    public async Task ANewGame_InheritsNothingTheLastCharacterEarned()
    {
        GameSession session = CreateSession(new RecordingWorld(Start));
        await session.StartNewGame();
        session.Character.Earn(500);
        session.Character.Progression.Gain(120);

        await session.StartNewGame();

        Assert.Equal(0, session.Character.Credits);
        Assert.Equal(0, session.Character.Progression.Experience);
    }

    [Fact]
    public async Task ANewGame_DoesNotDamageTheShipItInherited()
    {
        GameSession session = CreateSession(new RecordingWorld(Start));
        await session.StartNewGame();
        session.Ship.Health.Reduce(session.Ship.Health.Maximum);

        await session.StartNewGame();

        Assert.False(session.Ship.Health.IsEmpty);
    }

    [Fact]
    public async Task TheCharacterRecordsTheirStoryInTheSessionsQuestLog()
    {
        GameSession session = CreateSession(new RecordingWorld(Start));

        await session.StartNewGame();

        Assert.Same(session.Quests, session.Character.Quests);
        Assert.Equal(QuestId, Assert.Single(session.Character.Quests.Quests).Id);
    }

    [Fact]
    public async Task ANewGame_KeepsTheQuestLogItSubscribedTo()
    {
        // the character is replaced and the log is not, because the session subscribes to the log
        // once and saves on what it hears. Replaced, every subscriber would be listening to a log
        // nothing writes to any more — and the game would stop saving with nothing said.
        GameSession session = CreateSession(new RecordingWorld(Start));
        QuestLog quests = session.Quests;

        await session.StartNewGame();
        await session.StartNewGame();

        Assert.Same(quests, session.Quests);

        _saves.Content = null;
        session.Quests.Quests.Single().Start();
        await session.PendingSave;

        Assert.NotNull(_saves.Content);
    }

    [Fact]
    public async Task Continue_ProvisionsTheSameWayANewGameDoes()
    {
        // every path out of the session leaves the player in a ship, including the one where there
        // was no save to resume at all
        RecordingWorld world = new(new Position(40, 90));
        GameSession session = CreateSession(world);

        await session.Continue();

        Assert.NotNull(session.Character.Ship);
        Assert.Same(session.Ship, world.Introduced);
        Assert.Equal(new Position(40, 90), session.Player.Position);
    }
}
