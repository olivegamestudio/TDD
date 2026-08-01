using Pilgrimage;

namespace BattleForce2249.Tests;

public sealed class SaveGameSerializerTests
{
    [Fact]
    public void RoundTrips_ThePlayerPositionAndEveryQuestState()
    {
        SaveGame saved = new()
        {
            PlayerX = 12.5,
            PlayerY = -940.25,
            Quests =
            [
                new QuestProgress("quest-1", QuestState.Completed),
                new QuestProgress("quest-2", QuestState.Active),
                new QuestProgress("quest-3", QuestState.NotStarted),
            ],
        };

        SaveGame? loaded = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(saved));

        Assert.Equal(saved, loaded);
    }

    [Fact]
    public void WritesQuestStatesAsNames_SoASaveSurvivesReorderingTheEnum()
    {
        SaveGame saved = new()
        {
            Quests = [new QuestProgress("quest-1", QuestState.Completed)],
        };

        string json = SaveGameSerializer.Serialize(saved);

        Assert.Contains("\"Completed\"", json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"PlayerX\": ")]
    public void Deserialize_ReturnsNull_ForMissingOrCorruptContent(string? content)
    {
        // a corrupt save must not crash the game; the caller falls back to a new game
        Assert.Null(SaveGameSerializer.Deserialize(content));
    }

    [Theory]
    [InlineData(1e300, 0)]
    [InlineData(-1e300, 0)]
    [InlineData(0, 1e300)]
    [InlineData(0, -1e300)]
    [InlineData(1e300, 1e300)]
    public void Deserialize_ReturnsNull_ForAPositionTheGameCannotBeResumedAt(double x, double y)
    {
        // These are finite doubles and valid JSON numbers, so nothing upstream rejects them —
        // but the game hands a position to the drawing side as a float, and 1e300 narrows to
        // infinity. The camera refuses a non-finite target, so a save carrying one of these used
        // to be a blank screen and is now an exception thrown once a frame. Neither is a game.
        string json = SaveGameSerializer.Serialize(new SaveGame { PlayerX = x, PlayerY = y });

        Assert.Null(SaveGameSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1000.5, 940.25)]
    [InlineData(3e38, -3e38)]
    public void Deserialize_ReadsAPositionThatIsStillWithinReach(double x, double y)
    {
        // The bound is the float range and not a world size: the world is unbounded and a player
        // who flew a long way must still be able to load, so 3e38 is a save and 1e300 is not.
        string json = SaveGameSerializer.Serialize(new SaveGame { PlayerX = x, PlayerY = y });

        SaveGame? loaded = SaveGameSerializer.Deserialize(json);

        Assert.NotNull(loaded);
        Assert.Equal(x, loaded.PlayerX);
        Assert.Equal(y, loaded.PlayerY);
    }
}
