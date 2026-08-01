using Pilgrimage;

namespace BattleForce2249.Tests;

public sealed class SaveGameSerializerTests
{
    [Fact]
    public void RoundTrips_ThePlayerPositionTheirShipAndEveryQuestState()
    {
        SaveGame saved = new()
        {
            PlayerX = 12.5,
            PlayerY = -940.25,
            ShipId = "disgraced",
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

    [Fact]
    public void ASaveWrittenBeforeShipsWereRecorded_ReadsBackWithNoShip()
    {
        // an older build's save has no ShipId at all; the session decides what to fly, and it must
        // get a blank rather than a null to decide from
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            "{ \"PlayerX\": 0, \"PlayerY\": 500, \"Quests\": [] }");

        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded.ShipId);
    }

    [Fact]
    public void ASaveWithANullShip_ReadsBackWithNoShip()
    {
        SaveGame? loaded = SaveGameSerializer.Deserialize("{ \"ShipId\": null, \"Quests\": [] }");

        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded.ShipId);
    }

    [Fact]
    public void TwoSavesFlyingDifferentShips_AreNotEqual()
    {
        // the tests that assert on what was saved compare whole saves; a ship left out of that
        // comparison would let a save silently change ship without a test noticing
        SaveGame disgraced = new() { ShipId = "disgraced" };
        SaveGame other = new() { ShipId = "other" };

        Assert.NotEqual(disgraced, other);
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
}
