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

    [Theory]
    // a null element, rather than a null list — the list was guarded, the elements were not
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ null ] }""")]
    // an entry with no identifier at all: absent, or written as null
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ { "State": "Active" } ] }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ { "QuestId": null, "State": "Active" } ] }""")]
    public void Deserialize_ReturnsNull_ForAQuestEntryThisBuildDidNotWrite(string content)
    {
        // these reached QuestLog.Restore as a NullReferenceException and an ArgumentNullException,
        // neither of which is a storage failure — so they escaped the session and froze the game
        Assert.Null(SaveGameSerializer.Deserialize(content));
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void Deserialize_KeepsTheProgressBesideAnEntryNamingNoQuest(string blankId)
    {
        // A blank identifier is not an unreadable entry, it is an entry naming a quest this build
        // does not have — the drift QuestLog.Restore skips. Refusing the whole file for it threw
        // away the completed campaign saved next to it, and a refused save is then written over.
        string content = $$"""
        { "PlayerX": 0, "PlayerY": 700,
          "Quests": [ { "QuestId": "quest-1", "State": "Completed" },
                      { "QuestId": {{blankId}},  "State": "Active"    } ] }
        """;

        SaveGame? loaded = SaveGameSerializer.Deserialize(content);

        Assert.NotNull(loaded);
        Assert.Equal(700, loaded.PlayerY);
        Assert.Equal(
            QuestState.Completed,
            loaded.Quests.Single(quest => quest.QuestId == "quest-1").State);
    }

    [Theory]
    // 1e400 is well-formed JSON that overflows to Infinity rather than failing to parse
    [InlineData("1e400")]
    [InlineData("-1e400")]
    public void Deserialize_ReturnsNull_ForAPositionNoPlayerCanBeAt(string coordinate)
    {
        // a non-finite position is not a crash, it is worse: every distance from it is Infinity, so
        // no proximity trigger can ever fire and the quest can never be started or completed
        string x = $$"""{ "PlayerX": {{coordinate}}, "PlayerY": 0, "Quests": [] }""";
        string y = $$"""{ "PlayerX": 0, "PlayerY": {{coordinate}}, "Quests": [] }""";

        Assert.Null(SaveGameSerializer.Deserialize(x));
        Assert.Null(SaveGameSerializer.Deserialize(y));
    }

    [Fact]
    public void Deserialize_StillReadsASaveAtTheFarEdgeOfTheWorld()
    {
        // the guard is on values that are not positions at all, not on distant ones
        string content = $$"""
        { "PlayerX": {{double.MaxValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}}, "PlayerY": 0, "Quests": [] }
        """;

        SaveGame? loaded = SaveGameSerializer.Deserialize(content);

        Assert.NotNull(loaded);
        Assert.Equal(double.MaxValue, loaded.PlayerX);
    }
}
