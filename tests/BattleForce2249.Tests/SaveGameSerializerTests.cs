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
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ null, { "QuestId": "quest-1", "State": "Active" } ] }""")]
    public void Deserialize_ReturnsNull_ForAQuestEntryThatIsNotThere(string content)
    {
        // This reached QuestLog.Restore as a NullReferenceException, which is not a storage failure
        // — so it escaped the session and froze the game. An entry that is not an entry is not drift
        // between a save and a campaign; it is a file this build did not write, and it is refused
        // even when there is real progress beside it.
        Assert.Null(SaveGameSerializer.Deserialize(content));
    }

    [Theory]
    // an entry that names no quest: absent, null, blank, whitespace
    [InlineData("""{ "State": "Active" }""")]
    [InlineData("""{ "QuestId": null, "State": "Active" }""")]
    [InlineData("""{ "QuestId": "", "State": "Active" }""")]
    [InlineData("""{ "QuestId": "   ", "State": "Active" }""")]
    public void Deserialize_DropsAQuestEntryNamingNoQuest_AndKeepsTheProgressBesideIt(string entry)
    {
        // Refusing the whole file over one of these threw away every real quest beside it, and did
        // so at an edge that gets no second chance. QuestLog.Restore calls an entry naming no quest
        // drift and skips it; this is the same answer, given one file earlier so nothing downstream
        // has to hold an entry that matches nothing.
        string content = $$"""
        { "PlayerX": 0, "PlayerY": 700, "Quests": [ { "QuestId": "quest-1", "State": "Completed" }, {{entry}} ] }
        """;

        SaveGame? loaded = SaveGameSerializer.Deserialize(content);

        Assert.NotNull(loaded);
        Assert.Equal(700, loaded.PlayerY);
        QuestProgress kept = Assert.Single(loaded.Quests);
        Assert.Equal(new QuestProgress("quest-1", QuestState.Completed), kept);
    }

    [Fact]
    public void Deserialize_ReadsASaveThatNamesNoQuestAtAll_AsASaveWithNoQuests()
    {
        // The drop is not a back door to refusal: a file whose every entry names nothing is still a
        // position the player was at, and a campaign that starts from the beginning beside it.
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "PlayerX": 5, "PlayerY": 700, "Quests": [ { "QuestId": " ", "State": "Active" } ] }""");

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Quests);
        Assert.Equal(700, loaded.PlayerY);
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
