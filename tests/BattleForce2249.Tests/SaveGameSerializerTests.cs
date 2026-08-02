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

    // ---- a junk quest entry beside real progress ----
    // Refusal is per file; drift is per entry. The two look alike, and reading them the same way
    // is what let one blank line discard a completed campaign.

    [Theory]
    // a null element, rather than a null list — the list was guarded, the elements were not
    [InlineData("""{ "PlayerX": 0, "PlayerY": 0, "Quests": [ null ] }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 700, "Quests": [ { "QuestId": "quest-1", "State": "Completed" }, null ] }""")]
    public void Deserialize_ReturnsNull_ForAQuestEntryThatIsNotThere(string content)
    {
        // These reached QuestLog.Restore as a NullReferenceException, which is not a storage
        // failure — so it escaped the session onto a task nothing awaits and left the game screen
        // waiting for a session that never became ready. An entry that is not an entry is not
        // drift between a save and a campaign; it is a file this build did not write, and it is
        // refused even when there is real progress beside it.
        Assert.Null(SaveGameSerializer.Deserialize(content));
    }

    [Theory]
    // an entry that names no quest: absent, null, empty, whitespace
    [InlineData("""{ "State": "Active" }""")]
    [InlineData("""{ "QuestId": null, "State": "Active" }""")]
    [InlineData("""{ "QuestId": "", "State": "Active" }""")]
    [InlineData("""{ "QuestId": "   ", "State": "Active" }""")]
    public void Deserialize_DropsAQuestEntryNamingNoQuest_AndKeepsTheProgressBesideIt(string entry)
    {
        // An entry naming no quest matches nothing the campaign registered, so there is nothing
        // downstream that could act on it. Dropping it here gives the answer QuestLog.Restore
        // already gives, one file earlier, so nothing further in has to carry an entry it must
        // ignore.
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
    public void Deserialize_ReadsASaveWhoseEveryEntryNamesNoQuest_AsASaveWithNoQuests()
    {
        // The drop is not a back door to refusal: a file whose every entry names nothing is still
        // a position the player was at, with a campaign that starts from the beginning beside it.
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "PlayerX": 5, "PlayerY": 700, "Quests": [ { "QuestId": " ", "State": "Active" } ] }""");

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Quests);
        Assert.Equal(700, loaded.PlayerY);
    }
}
