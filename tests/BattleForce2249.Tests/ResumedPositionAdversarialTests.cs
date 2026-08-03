using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's adversarial cover for the two rules #44 decided at the save file's edge: an entry naming no
/// quest is dropped and the rest of the file read, and a saved position is used only when the
/// campaign it was taken beside actually came back.
/// </summary>
/// <remarks>
/// <para>
/// The cover already on the branch asserts each edge on its own. These attack the joins between
/// them: that the two edges give the <em>same</em> answer to the same identifier — which is
/// criterion 1, and is a claim about a pair rather than about either half — that refusing a file
/// never destroys it, and that declining a position puts the player where the world says a game
/// begins rather than at the origin.
/// </para>
/// <para>
/// That last one is the gap worth naming. Every world in the suite and in the shipping game starts
/// at <c>(0, 0)</c>, so an implementation that moved the player to the origin instead of leaving
/// <c>Reset</c>'s work alone would pass every test on the branch and strand the player in the first
/// campaign that spawns anywhere else. The world here deliberately starts somewhere else.
/// </para>
/// </remarks>
public sealed class ResumedPositionAdversarialTests
{
    /// <summary>
    /// A start that is not the origin, so "the position was declined" and "the position was zeroed"
    /// are different observations rather than the same one.
    /// </summary>
    static readonly Position OffOrigin = new(11, 22);

    static readonly Position End = new(0, 1000);

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    sealed class TestWorld(Position start, params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => start;

        // a test world that names no places: whatever enters it enters where it starts
        public Position Introduce(Ship ship, string location) => PlayerStart;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    static QuestDefinition Quest(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true);

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(params string[] questIds)
    {
        string[] ids = questIds.Length == 0 ? ["quest-1"] : questIds;

        return new GameSession(
            _saves,
            new TestCampaign([.. ids.Select(Quest)]),
            new TestRoster(),
            new TestWorld(OffOrigin, [.. ids.Select(id => new QuestMarkers(id, OffOrigin, End))]));
    }

    static string SaveAt(double y, params string[] questEntries) =>
        $$"""{ "PlayerX": 0, "PlayerY": {{y}}, "Quests": [ {{string.Join(", ", questEntries)}} ] }""";

    /// <summary>
    /// Every shape the framework treats as blank, quoted as it appears inside JSON. The rule is
    /// stated as "names no quest", so it has to cover a tab or a pasted no-break space as well as
    /// the empty string.
    /// </summary>
    public static TheoryData<string, string> IdentifiersNamingNothing =>
        new()
        {
            { "empty", "" },
            { "spaces", "   " },
            { "tab", "\\t" },
            { "line feed", "\\n" },
            { "no-break space", " " },
            { "em space", " " },
        };

    // ---- the two edges give the same answer ----

    /// <summary>
    /// Criterion 1 asserted as the pair it is. Each edge having its own passing test does not say
    /// they agree; this reads one identifier and checks the serializer drops it while the quest log
    /// skips it, so the two cannot drift apart again without a failure naming the shape they
    /// disagreed on.
    /// </summary>
    [Theory]
    [MemberData(nameof(IdentifiersNamingNothing))]
    public void TheSerializerAndTheQuestLogAgree_AboutAnIdentifierThatNamesNothing(
        string because,
        string questId)
    {
        Assert.NotNull(because);

        SaveGame? save = SaveGameSerializer.Deserialize(
            SaveAt(700, $$"""{ "QuestId": "{{questId}}", "State": "Active" }""",
                        """{ "QuestId": "quest-1", "State": "Completed" }"""));

        // the file edge: read, with the unnamed entry gone and the one beside it kept
        Assert.NotNull(save);
        QuestProgress kept = Assert.Single(save.Quests);
        Assert.Equal("quest-1", kept.QuestId);

        // the log edge: the same entry skipped rather than refused, reached directly so the
        // serializer having already dropped it cannot mask a disagreement
        QuestLog log = new();
        log.Register(Quest("quest-1"));
        log.Restore([
            new QuestProgress(questId.Replace("\\t", "\t").Replace("\\n", "\n"), QuestState.Active),
            new QuestProgress("quest-1", QuestState.Completed),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }

    // ---- refusing a file never destroys it ----

    /// <summary>
    /// A <c>null</c> entry refuses the file wherever it sits, and the refusal is survivable: the
    /// content is set aside intact rather than played over. Every doc comment on this branch leans
    /// on that — the reason dropping an unnamed entry is preferred to refusing the file is that a
    /// refusal is final — so it is worth pinning that the refusal path really does keep the bytes.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Continue_SetsTheFileAsideIntact_WhereverTheEntryThatIsNotThereSits(int index)
    {
        List<string> entries = [
            """{ "QuestId": "quest-1", "State": "Completed" }""",
            """{ "QuestId": "  ", "State": "Active" }""",
        ];
        entries.Insert(index, "null");

        string content = SaveAt(700, [.. entries]);
        _saves.Content = content;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(content, _saves.SetAsideContent);
        Assert.True(session.IsReady);
        Assert.Equal(OffOrigin, session.Player.Position);
        Assert.Equal(QuestState.NotStarted, session.Quests.Find("quest-1")!.State);
    }

    // ---- declining a position ----

    /// <summary>
    /// The gap the rest of the suite cannot see. Declining the saved position must leave the player
    /// where the world says a game begins; moving them to the origin is a different rule that
    /// happens to agree everywhere the origin is the start.
    /// </summary>
    [Theory]
    [InlineData("""{ "QuestId": "   ", "State": "Completed" }""")]
    [InlineData("""{ "QuestId": "quest-99", "State": "Completed" }""")]
    [InlineData("""{ "QuestId": "quest-1", "State": "NotStarted" }""")]
    public async Task Continue_PutsThePlayerAtTheWorldStart_NotAtTheOrigin_WhenItDeclinesThePosition(
        string questEntry)
    {
        _saves.Content = SaveAt(700, questEntry);
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(OffOrigin, session.Player.Position);
        Assert.NotEqual(new Position(0, 0), session.Player.Position);
    }

    /// <summary>
    /// An identifier that names a quest once padded is not that quest. Trimming it would be a
    /// kindness the campaign never asked for, and it would make the two edges disagree — the
    /// serializer keeps a padded entry, so only the log can decide it names nothing.
    /// </summary>
    [Fact]
    public async Task Continue_DeclinesThePosition_WhenTheOnlyEntryNamesAQuestOnlyOnceItIsTrimmed()
    {
        _saves.Content = SaveAt(700, """{ "QuestId": " quest-1 ", "State": "Completed" }""");
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(OffOrigin, session.Player.Position);
        Assert.Equal(QuestState.NotStarted, session.Quests.Find("quest-1")!.State);
    }

    /// <summary>
    /// The campaign has begun if <em>any</em> registered quest came back started, not only the first
    /// one. A check written against the first quest, or against a specific one, passes the
    /// single-quest cover on the branch and fails here.
    /// </summary>
    [Theory]
    [InlineData("quest-1")]
    [InlineData("quest-2")]
    [InlineData("quest-3")]
    public async Task Continue_KeepsTheSavedPosition_WhenAnyRegisteredQuestCameBackStarted(string started)
    {
        _saves.Content = SaveAt(700,
            $$"""{ "QuestId": "{{started}}", "State": "Active" }""",
            """{ "QuestId": "  ", "State": "Completed" }""",
            """{ "QuestId": "quest-99", "State": "Completed" }""");
        GameSession session = CreateSession("quest-1", "quest-2", "quest-3");

        await session.Continue();

        Assert.Equal(700, session.Player.Position.Y);
    }

    /// <summary>
    /// The session is registered as a singleton, so one instance reads every save a run opens. The
    /// answer must come from the save just restored rather than from the game before it — a
    /// campaign that had begun leaving anything behind would carry a position into a game whose
    /// file says nothing about any quest.
    /// </summary>
    [Fact]
    public async Task Continue_DoesNotCarryAPositionFromTheGameBefore()
    {
        _saves.Content = SaveAt(700, """{ "QuestId": "quest-1", "State": "Completed" }""");
        GameSession session = CreateSession();

        await session.Continue();
        Assert.Equal(700, session.Player.Position.Y);

        _saves.Content = SaveAt(900, """{ "QuestId": "quest-99", "State": "Completed" }""");

        await session.Continue();

        Assert.Equal(OffOrigin, session.Player.Position);
    }

    /// <summary>
    /// Declining a position is not a judgement that the file is damaged, so nothing is set aside and
    /// nothing is written over it. The file keeps its coordinates until real progress replaces them,
    /// which is the whole reason declining was preferred to refusing.
    /// </summary>
    [Theory]
    [InlineData("""{ "QuestId": "   ", "State": "Completed" }""")]
    [InlineData("""{ "QuestId": "quest-99", "State": "Completed" }""")]
    public async Task Continue_LeavesTheFileExactlyAsItWas_WhenItDeclinesThePosition(string questEntry)
    {
        string content = SaveAt(700, questEntry);
        _saves.Content = content;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(content, _saves.Content);
        Assert.Null(_saves.SetAsideContent);
        Assert.Equal(0, _saves.SaveCount);
    }

    /// <summary>
    /// A file with no quest list at all is the same case as one whose every entry is drift: readable,
    /// restoring nothing, so its coordinates are declined. It reaches the rule by a different route —
    /// the list is replaced with an empty one before any entry is looked at — and an implementation
    /// that answered "has begun" from the file's entries rather than from the restored log would
    /// part company from the rest here.
    /// </summary>
    [Theory]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 700 }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 700, "Quests": null }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 700, "Quests": [] }""")]
    public async Task Continue_DeclinesThePosition_WhenTheSaveHoldsNoQuestListAtAll(string content)
    {
        _saves.Content = content;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Equal(OffOrigin, session.Player.Position);
        Assert.Null(_saves.SetAsideContent);
    }

    /// <summary>
    /// A file where every entry names nothing is read, not refused — which is the reported defect's
    /// exact inverse. Many entries rather than one, because the drop is a filter and a filter is
    /// where an off-by-one lives.
    /// </summary>
    [Fact]
    public async Task Continue_ReadsASave_WhenEveryOneOfManyEntriesNamesNothing()
    {
        _saves.Content = SaveAt(700, [.. Enumerable.Range(0, 50)
            .Select(i => $$"""{ "QuestId": "{{new string(' ', i % 4)}}", "State": "Completed" }""")]);
        GameSession session = CreateSession();

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Null(_saves.SetAsideContent);
        Assert.Equal(OffOrigin, session.Player.Position);
    }

    /// <summary>
    /// The player put back at the start has a game to play: the quest log is a new game's, so the
    /// start trigger they are standing on fires and the campaign begins. Declining the position is
    /// only the cheaper mistake if what is left is playable.
    /// </summary>
    [Fact]
    public async Task Continue_LeavesTheCampaignPlayable_WhenItDeclinesThePosition()
    {
        _saves.Content = SaveAt(700, """{ "QuestId": "quest-99", "State": "Completed" }""");
        GameSession session = CreateSession();

        await session.Continue();

        QuestProximityWatcher watcher = new(
            new TestWorld(OffOrigin, new QuestMarkers("quest-1", OffOrigin, End)));
        watcher.Update(session.Quests, session.Player.Position);

        Assert.Equal(QuestState.Active, session.Quests.Find("quest-1")!.State);
    }
}
