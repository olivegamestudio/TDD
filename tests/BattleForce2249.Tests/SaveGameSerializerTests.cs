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
    public void ReadsASaveWrittenBeforeShipsWereRecorded()
    {
        // the shape of the save is the compatibility boundary: an older file has no ship id, and
        // must come back as "no ship recorded" rather than as an unreadable save
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "PlayerX": 0, "PlayerY": 500, "Quests": [] }""");

        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded.ShipId);
    }

    [Fact]
    public void ReadsASaveWhoseShipIdIsExplicitlyNull()
    {
        // JSON can supply a null for a member the type declares as never null
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "ShipId": null, "Quests": null }""");

        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded.ShipId);
        Assert.Empty(loaded.Quests);
    }

    [Fact]
    public void TwoSavesDifferingOnlyByShip_AreNotTheSameSave()
    {
        // SaveGame compares by hand, so a member added without touching Equals would be invisible
        Assert.NotEqual(
            new SaveGame { ShipId = "disgraced" },
            new SaveGame { ShipId = "earned" });
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
