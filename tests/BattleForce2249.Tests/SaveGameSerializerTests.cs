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

    // ---- a position that reads back perfectly and still cannot be played ----

    [Theory]
    [InlineData(1e300, 0)]
    [InlineData(-1e300, 0)]
    [InlineData(0, 1e300)]
    [InlineData(0, -1e300)]
    [InlineData(1e300, 1e300)]
    [InlineData(3.5e38, 0)]
    public void Deserialize_ReturnsNull_ForAPositionThePlayerCanNeverBeDrawnAt(double x, double y)
    {
        // The world is measured in double and the screen in float, and the game narrows from one
        // to the other when it hands a position to the drawing side. A coordinate past float's
        // range is a finite, well formed double that narrows to infinity — so this JSON is not
        // corrupt in any way a parser can see, and the game still cannot resume from it.
        string json = $$"""{ "PlayerX": {{x:R}}, "PlayerY": {{y:R}}, "Quests": [] }""";

        Assert.Null(SaveGameSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData(3.4028234663852886e38)]   // float.MaxValue exactly, as a double
    [InlineData(-3.4028234663852886e38)]
    [InlineData(1e30)]
    public void Deserialize_KeepsAPositionAtTheFarEdgeOfWhatCanBeDrawn(double coordinate)
    {
        // the bound is where the drawing side runs out, not somewhere short of it: a save from
        // the furthest the game can draw is still a save
        SaveGame saved = new() { PlayerX = coordinate, PlayerY = coordinate };

        SaveGame? loaded = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(saved));

        Assert.Equal(saved, loaded);
    }
}
