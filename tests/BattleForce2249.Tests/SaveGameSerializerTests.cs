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
    public void ReadsASaveWrittenBeforeShipsWereRecorded_AsNoShip()
    {
        // an older build's save has no ShipId field at all; the session reads a blank one as
        // "give them the starting ship" rather than refusing to load
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "PlayerX": 0, "PlayerY": 700, "Quests": [] }""");

        Assert.NotNull(loaded);
        Assert.Equal("", loaded.ShipId);
    }

    [Fact]
    public void ReadsANullShipId_AsNoShip()
    {
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "PlayerX": 0, "PlayerY": 0, "ShipId": null, "Quests": [] }""");

        Assert.NotNull(loaded);
        Assert.Equal("", loaded.ShipId);
    }

    [Fact]
    public void WritesTheShipIdVerbatim_BecauseItIsAnIdentifierAndNeverTranslated()
    {
        string json = SaveGameSerializer.Serialize(new SaveGame { ShipId = "disgraced" });

        Assert.Contains("\"disgraced\"", json);
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
}
