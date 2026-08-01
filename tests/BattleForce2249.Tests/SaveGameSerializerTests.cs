using System.Globalization;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

public sealed class SaveGameSerializerTests
{
    /// <summary>
    /// A coordinate written the way JSON needs it, whatever the machine's culture. A culture with a
    /// comma decimal separator would otherwise put one in the middle of the number and the test
    /// would be measuring the parser's opinion of malformed JSON instead of the guard.
    /// </summary>
    static string Json(double coordinate) =>
        coordinate.ToString("R", CultureInfo.InvariantCulture);

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

    [Theory]
    // a null element, rather than a null list — the list was guarded, the elements were not
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ null ] }""")]
    // an entry that names no quest: absent, null, blank
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ { "State": "Active" } ] }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ { "QuestId": null, "State": "Active" } ] }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ { "QuestId": "", "State": "Active" } ] }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ { "QuestId": "   ", "State": "Active" } ] }""")]
    public void Deserialize_ReturnsNull_ForAQuestEntryThisBuildDidNotWrite(string content)
    {
        // these reached QuestLog.Restore as a NullReferenceException and an ArgumentNullException,
        // neither of which is a storage failure — so they escaped the session and froze the game
        Assert.Null(SaveGameSerializer.Deserialize(content));
    }

    [Theory]
    // 1e400 is well-formed JSON that overflows to Infinity rather than failing to parse
    [InlineData("1e400")]
    [InlineData("-1e400")]
    // and these are finite, which is the point: the arithmetic outcome is identical, so guarding on
    // finiteness alone let the same brick through one character of the file later
    [InlineData("1e300")]
    [InlineData("-1e300")]
    [InlineData("1.7976931348623157e308")]
    public void Deserialize_ReturnsNull_ForAPositionNoPlayerCanBeAt(string coordinate)
    {
        // a position out here is not a crash, it is worse: the player cannot move away from it, so
        // no proximity trigger can ever fire and the quest can never be started or completed
        string x = $$"""{ "PlayerX": {{coordinate}}, "PlayerY": 0, "Quests": [] }""";
        string y = $$"""{ "PlayerX": 0, "PlayerY": {{coordinate}}, "Quests": [] }""";

        Assert.Null(SaveGameSerializer.Deserialize(x));
        Assert.Null(SaveGameSerializer.Deserialize(y));
    }

    [Fact]
    public void Deserialize_StillReadsASaveAtTheFarEdgeOfTheWorld()
    {
        // The guard is on positions the player cannot play on from, not on distant ones — and the
        // far edge is a real place, not a rounding of one. This used to assert double.MaxValue
        // round-tripped, which proved the serialiser faithful and the save unplayable at the same
        // time; the campaign's markers sit at ~1e3, so Position.MaxCoordinate is already twelve
        // orders of magnitude past anything the game produces.
        string content = $$"""
        { "PlayerX": {{Json(Position.MaxCoordinate)}}, "PlayerY": {{Json(-Position.MaxCoordinate)}}, "Quests": [] }
        """;

        SaveGame? loaded = SaveGameSerializer.Deserialize(content);

        Assert.NotNull(loaded);
        Assert.Equal(Position.MaxCoordinate, loaded.PlayerX);
        Assert.Equal(-Position.MaxCoordinate, loaded.PlayerY);
    }

    [Fact]
    public void Deserialize_StillReadsASaveFromOrdinaryDistantPlay()
    {
        // the guard rejecting real play would be a worse defect than the one it was written for
        string content = """{ "PlayerX": -123456.75, "PlayerY": 987654.5, "Quests": [] }""";

        SaveGame? loaded = SaveGameSerializer.Deserialize(content);

        Assert.NotNull(loaded);
        Assert.Equal(-123456.75, loaded.PlayerX);
        Assert.Equal(987654.5, loaded.PlayerY);
    }
}
