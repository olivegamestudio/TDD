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
    public void Deserialize_ReturnsNull_ForAPositionNoPlayerCanBeAt(string coordinate)
    {
        // a non-finite position is not a crash, it is worse: every distance from it is Infinity, so
        // no proximity trigger can ever fire and the quest can never be started or completed
        string x = $$"""{ "PlayerX": {{coordinate}}, "PlayerY": 0, "Quests": [] }""";
        string y = $$"""{ "PlayerX": 0, "PlayerY": {{coordinate}}, "Quests": [] }""";

        Assert.Null(SaveGameSerializer.Deserialize(x));
        Assert.Null(SaveGameSerializer.Deserialize(y));
    }

    [Theory]
    // finite, so IsFinite admits them, and one character away in the file from the 1e400 above
    [InlineData("1e300")]
    [InlineData("1.7976931348623157e308")]
    [InlineData("-1e300")]
    public void Deserialize_ReturnsNull_ForAPositionBeyondTheEdgeOfTheWorld(string coordinate)
    {
        // finiteness was never the condition the guard wanted. A coordinate this far out is not
        // somewhere play can have put the player, and it is somewhere play cannot get them back
        // from — the same brick as an infinite one, reached by a value the finite check accepts.
        string x = $$"""{ "PlayerX": {{coordinate}}, "PlayerY": 0, "Quests": [] }""";
        string y = $$"""{ "PlayerX": 0, "PlayerY": {{coordinate}}, "Quests": [] }""";

        Assert.Null(SaveGameSerializer.Deserialize(x));
        Assert.Null(SaveGameSerializer.Deserialize(y));
    }

    [Fact]
    public void Deserialize_StillReadsASaveFarFromTheCampaign()
    {
        // the guard is on positions outside the world, not on distant ones — a save a thousand
        // marker-lengths out is a long flight home, but it is a game, and refusing it would throw
        // the player's progress away
        string content = """
        { "PlayerX": -123456.75, "PlayerY": 987654.5, "Quests": [] }
        """;

        SaveGame? loaded = SaveGameSerializer.Deserialize(content);

        Assert.NotNull(loaded);
        Assert.Equal(-123456.75, loaded.PlayerX);
        Assert.Equal(987654.5, loaded.PlayerY);
    }

    [Fact]
    public void Deserialize_ReadsASaveOnTheWorldsEdge_AndRefusesTheOneJustOutside()
    {
        // the boundary is stated, so it is worth pinning which side of it each answer falls on
        string onTheEdge = $$"""
        { "PlayerX": {{Invariant(BattleForceWorld.Extent)}}, "PlayerY": 0, "Quests": [] }
        """;
        string justOutside = $$"""
        { "PlayerX": {{Invariant(Math.BitIncrement(BattleForceWorld.Extent))}}, "PlayerY": 0, "Quests": [] }
        """;

        Assert.NotNull(SaveGameSerializer.Deserialize(onTheEdge));
        Assert.Null(SaveGameSerializer.Deserialize(justOutside));
    }

    [Fact]
    public void Deserialize_ReadsASaveOnTheWorldsNegativeEdge_AndRefusesTheOneJustOutside()
    {
        // the edge is stated as a distance from the origin, so it has two sides and only one of
        // them was pinned. A guard written as "greater than -Extent" rather than "at least" would
        // pass every test above and still refuse a save sitting exactly on the near edge.
        string onTheEdge = $$"""
        { "PlayerX": 0, "PlayerY": {{Invariant(-BattleForceWorld.Extent)}}, "Quests": [] }
        """;
        string justOutside = $$"""
        { "PlayerX": 0, "PlayerY": {{Invariant(Math.BitDecrement(-BattleForceWorld.Extent))}}, "Quests": [] }
        """;

        Assert.NotNull(SaveGameSerializer.Deserialize(onTheEdge));
        Assert.Null(SaveGameSerializer.Deserialize(justOutside));
    }

    [Fact]
    public void Deserialize_RefusesAPositionOutsideTheWorld_OnEitherAxisAndEitherSign()
    {
        // the guard is applied per axis, so each of the four ways out of the world is its own path
        // through it, and a copy-paste that checked X twice would still satisfy the theory above.
        double outside = Math.BitIncrement(BattleForceWorld.Extent);

        Assert.Null(SaveGameSerializer.Deserialize(
            $$"""{ "PlayerX": {{Invariant(outside)}}, "PlayerY": 0, "Quests": [] }"""));
        Assert.Null(SaveGameSerializer.Deserialize(
            $$"""{ "PlayerX": {{Invariant(-outside)}}, "PlayerY": 0, "Quests": [] }"""));
        Assert.Null(SaveGameSerializer.Deserialize(
            $$"""{ "PlayerX": 0, "PlayerY": {{Invariant(outside)}}, "Quests": [] }"""));
        Assert.Null(SaveGameSerializer.Deserialize(
            $$"""{ "PlayerX": 0, "PlayerY": {{Invariant(-outside)}}, "Quests": [] }"""));
    }

    static string Invariant(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
