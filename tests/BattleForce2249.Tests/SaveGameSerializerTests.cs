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
    public void DistinguishesTwoSaves_ThatDifferOnlyByTheShip()
    {
        // the sessions tests compare whole saves, so a ship left out of equality would make them
        // pass against a serialiser that dropped it
        SaveGame flyingOne = new() { ShipId = "disgraced" };
        SaveGame flyingAnother = new() { ShipId = "something-earned" };

        Assert.NotEqual(flyingOne, flyingAnother);
    }

    [Fact]
    public void Deserialize_ReadsASaveWrittenBeforeShipsWereRecorded()
    {
        // an older save has no ship in it; it reads back as "no ship named" rather than failing
        SaveGame? loaded = SaveGameSerializer.Deserialize("""{ "PlayerX": 3, "PlayerY": 400 }""");

        Assert.NotNull(loaded);
        Assert.Equal("", loaded.ShipId);
        Assert.Equal(400, loaded.PlayerY);
    }

    [Fact]
    public void Deserialize_ReadsASaveWhoseShipIsNull()
    {
        // a property initialiser only runs when the property is absent, not when it is written null
        SaveGame? loaded = SaveGameSerializer.Deserialize("""{ "ShipId": null }""");

        Assert.NotNull(loaded);
        Assert.Equal("", loaded.ShipId);
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
