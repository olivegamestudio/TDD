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
    // a name that is not a state has always been rejected; a number that is not one was not, and
    // loaded as a quest that could neither be started nor completed for the rest of the game
    [InlineData("\"Nonsense\"")]
    [InlineData("99")]
    [InlineData("0")]
    public void Deserialize_ReturnsNull_ForAQuestStateThisBuildDidNotWrite(string state)
    {
        string content = $$"""
        { "PlayerX": 0, "PlayerY": 0, "Quests": [ { "QuestId": "quest-1", "State": {{state}} } ] }
        """;

        Assert.Null(SaveGameSerializer.Deserialize(content));
    }

    // ---- the ship the save remembers ----

    [Fact]
    public void RoundTrips_TheShipThePlayerWasFlying()
    {
        SaveGame saved = new() { ShipId = "disgraced" };

        SaveGame? loaded = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(saved));

        Assert.Equal("disgraced", loaded!.ShipId);
    }

    [Fact]
    public void WritesTheShipsIdentifier_AndNeverItsContent()
    {
        // a ship's numbers and its artwork are content a later build is free to rebalance or
        // redraw. Writing them into the save would freeze the player's ship at whatever it was
        // worth the day they saved, so a balance change would reach new games and not existing
        // ones.
        string json = SaveGameSerializer.Serialize(new SaveGame { ShipId = BattleForceShip.Id });

        Assert.Contains(BattleForceShip.Id, json);
        Assert.DoesNotContain(BattleForceShip.AssetKey, json);
        Assert.DoesNotContain("Acceleration", json);
        Assert.DoesNotContain("TurnRate", json);
        Assert.DoesNotContain("Handling", json);
    }

    [Fact]
    public void Deserialize_ReadsASaveWrittenBeforeShipsWereRecorded()
    {
        // the shape the previous build wrote. It is not damaged, it is old, and it has to load.
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "PlayerX": 1, "PlayerY": 2, "Quests": [] }""");

        Assert.NotNull(loaded);
        Assert.Equal("", loaded.ShipId);
        Assert.Equal(1, loaded.PlayerX);
    }

    [Fact]
    public void Deserialize_ReadsAShipWrittenAsJsonNull()
    {
        // a genuinely separate path from the one above: a property initialiser runs when the
        // property is absent, not when it is present and null
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "PlayerX": 0, "PlayerY": 0, "ShipId": null, "Quests": null }""");

        Assert.NotNull(loaded);
        Assert.Equal("", loaded.ShipId);
        Assert.Empty(loaded.Quests);
    }

    [Fact]
    public void TwoSavesDifferingOnlyByShip_AreNotEqual()
    {
        // without this, every whole-save assertion in the suite would pass against a serialiser
        // that dropped the ship on the floor
        SaveGame flying = new() { ShipId = "earned" };
        SaveGame grounded = new() { ShipId = "disgraced" };

        Assert.NotEqual(flying, grounded);
        Assert.NotEqual(flying.GetHashCode(), grounded.GetHashCode());
    }

    [Fact]
    public void TheShipsIdentifierIsNeverTranslated()
    {
        // a save written in one language has to load in another
        foreach (string culture in new[] { "de", "ja", "tr", "ar" })
        {
            using CultureScope _ = new(culture);

            SaveGame? loaded = SaveGameSerializer.Deserialize(
                SaveGameSerializer.Serialize(new SaveGame { ShipId = BattleForceShip.Id }));

            Assert.Equal(BattleForceShip.Id, loaded!.ShipId);
        }
    }
}
