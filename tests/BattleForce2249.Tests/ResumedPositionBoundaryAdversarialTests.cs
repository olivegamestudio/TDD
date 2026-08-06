using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's adversarial cover for the outer edges of the position rule #44 introduced, and for the
/// ordinary save-and-resume loop it had to leave alone.
/// </summary>
/// <remarks>
/// <para>
/// The rule declines a saved position unless a registered quest came back started or completed.
/// The cover on the branch exercises it against campaigns that ship a quest. Two edges it does not
/// reach: a campaign that ships none — where the condition can never be met, so the position can
/// never be used — and the round trip through the real serializer, which is the only path a
/// player's save actually takes and the one thing the rule must not have broken.
/// </para>
/// <para>
/// As elsewhere in this suite the world start is deliberately off the origin, so "the position was
/// declined" and "the position was zeroed" are different observations.
/// </para>
/// </remarks>
public sealed class ResumedPositionBoundaryAdversarialTests
{
    static readonly Position OffOrigin = new(11, 22);

    static readonly Position End = new(0, 1000);

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => OffOrigin;

        // a test world that names no places: whatever enters it enters where it starts
        public Position Introduce(Ship ship, string location) => PlayerStart;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;

        // nobody stands in these worlds; what they are for is quests and saves
        public IReadOnlyList<Npc> Npcs { get; } = [];
    }

    static QuestDefinition Quest(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50));

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(params string[] questIds) =>
        new(_saves,
            new TestCampaign([.. questIds.Select(Quest)]),
            new TestRoster(),
            new TestWorld([.. questIds.Select(id => new QuestMarkers(id, OffOrigin, End))]));

    // ---- a campaign that ships no quests ----

    /// <summary>
    /// A campaign shipping no quests can never satisfy the condition the position rule asks about,
    /// so the saved coordinates are always declined. That is the rule applied honestly rather than
    /// a special case, and it is worth pinning because it is the one campaign for which the rule
    /// has no upper bound on what it costs: every resume begins at the world start.
    /// </summary>
    [Fact]
    public async Task Continue_DeclinesTheSavedPosition_WhenTheCampaignShipsNoQuests()
    {
        _saves.Content = """{ "PlayerX": 0, "PlayerY": 700, "Quests": [] }""";

        GameSession session = CreateSession();
        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Equal(OffOrigin, session.Player.Position);
    }

    /// <summary>
    /// The same campaign, and a file whose entries all name quests it does not ship. The save is
    /// read rather than refused — nothing is set aside — and the position is still declined.
    /// </summary>
    [Fact]
    public async Task Continue_ReadsTheSave_WhenTheCampaignShipsNoQuestsAndTheFileNamesSome()
    {
        _saves.Content =
            """{ "PlayerX": 0, "PlayerY": 700, "Quests": [ { "QuestId": "quest-1", "State": "Completed" } ] }""";

        GameSession session = CreateSession();
        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Equal(OffOrigin, session.Player.Position);
        Assert.Null(_saves.SetAsideContent);
        Assert.NotNull(_saves.Content);
    }

    // ---- the ordinary loop the rule must not have broken ----

    /// <summary>
    /// The whole round trip through the real serializer: a game that saved itself where the player
    /// had got to comes back there. This is the path every player's save takes, and the position
    /// rule sits directly across it, so a rule that declined a genuine save would strand a real
    /// game rather than a hand-edited one.
    /// </summary>
    /// <remarks>
    /// Deliberately not written by hand. A file this build wrote is the only file the rule is
    /// obliged to honour, and building it through the session means the capture, the serialiser and
    /// the condition are all exercised against each other rather than against a fixture that
    /// happens to agree with them.
    /// </remarks>
    [Fact]
    public async Task Continue_ResumesWhereItWasSaved_ForASaveThisBuildWroteItself()
    {
        Position travelled = new(0, 700);

        GameSession saving = CreateSession("quest-1", "quest-2");
        await saving.StartNewGame();

        saving.Player.MoveTo(travelled);
        saving.Quests.Find("quest-1")!.Start();
        await saving.PendingSave;

        GameSession resuming = CreateSession("quest-1", "quest-2");
        await resuming.Continue();

        Assert.Equal(travelled, resuming.Player.Position);
        Assert.Equal(QuestState.Active, resuming.Quests.Find("quest-1")!.State);
        Assert.Equal(QuestState.NotStarted, resuming.Quests.Find("quest-2")!.State);
    }

    /// <summary>
    /// The same round trip with one quest finished rather than begun, since the condition names
    /// both states and a save holding only completed quests is what the end of a campaign looks
    /// like.
    /// </summary>
    [Fact]
    public async Task Continue_ResumesWhereItWasSaved_WhenEveryQuestIsAlreadyFinished()
    {
        Position travelled = new(0, 900);

        GameSession saving = CreateSession("quest-1");
        await saving.StartNewGame();

        saving.Player.MoveTo(travelled);
        Quest quest = saving.Quests.Find("quest-1")!;
        quest.Start();
        quest.Complete();
        await saving.PendingSave;

        GameSession resuming = CreateSession("quest-1");
        await resuming.Continue();

        Assert.Equal(travelled, resuming.Player.Position);
        Assert.Equal(QuestState.Completed, resuming.Quests.Find("quest-1")!.State);
    }

    /// <summary>
    /// A save written by this build and then edited to carry one entry naming no quest beside the
    /// real progress — the file from the report, built the way a player's file is built rather than
    /// quoted. The junk entry is dropped, the real progress survives, and because it survives the
    /// position is honoured.
    /// </summary>
    [Fact]
    public async Task Continue_KeepsTheSavedPosition_WhenAJunkEntryIsAddedToASaveThisBuildWrote()
    {
        Position travelled = new(0, 700);

        GameSession saving = CreateSession("quest-1");
        await saving.StartNewGame();

        saving.Player.MoveTo(travelled);
        saving.Quests.Find("quest-1")!.Start();
        await saving.PendingSave;

        // spliced into the file this build wrote, rather than replacing it
        SaveGame written = Assert.IsType<SaveGame>(_saves.Saved);
        _saves.Content = SaveGameSerializer.Serialize(written with
        {
            Quests = [.. written.Quests, new QuestProgress("   ", QuestState.Active)],
        });

        GameSession resuming = CreateSession("quest-1");
        await resuming.Continue();

        Assert.Equal(travelled, resuming.Player.Position);
        Assert.Equal(QuestState.Active, resuming.Quests.Find("quest-1")!.State);
        Assert.Null(_saves.SetAsideContent);
    }

    // ---- refusal outranks the drop, at the file's edge ----

    /// <summary>
    /// The serializer's two rules meeting in one file: an entry that is not there beside entries
    /// naming no quest. Refusal is per file and the drop is per entry, so the file is refused whole
    /// — and, because it is refused, set aside intact rather than dropped down to the entries that
    /// happened to survive the filter.
    /// </summary>
    [Theory]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 700, "Quests": [ { "QuestId": "", "State": "Active" }, null ] }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 700, "Quests": [ null, { "QuestId": "   ", "State": "Active" } ] }""")]
    [InlineData("""{ "PlayerX": 0, "PlayerY": 700, "Quests": [ { "QuestId": null, "State": "Active" }, null, { "QuestId": "quest-1", "State": "Completed" } ] }""")]
    public async Task Continue_RefusesTheFileWhole_WhenAnEntryThatIsNotThereStandsBesideEntriesNamingNoQuest(
        string content)
    {
        Assert.Null(SaveGameSerializer.Deserialize(content));

        _saves.Content = content;

        GameSession session = CreateSession("quest-1");
        await session.Continue();

        // set aside exactly as it was, and a new game played over it
        Assert.Equal(content, _saves.SetAsideContent);
        Assert.True(session.IsReady);
        Assert.Equal(OffOrigin, session.Player.Position);
        Assert.Equal(QuestState.NotStarted, session.Quests.Find("quest-1")!.State);
    }
}
