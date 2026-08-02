using System.Globalization;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Pins what a resumed save's coordinates mean when the save says nothing about any quest the
/// campaign ships.
/// </summary>
/// <remarks>
/// <para>
/// Reading a save is deliberately forgiving about quests: an entry naming a quest this build no
/// longer ships is ignored, and so is one naming no quest at all. Both are drift, and tolerating
/// them is what stops a stale entry costing a player their campaign. But a file whose every entry
/// is drift restores <em>no</em> progress, and its coordinates then place the player inside a
/// campaign nobody has begun.
/// </para>
/// <para>
/// The campaign and the world here are the shipped <see cref="BattleForceCampaign"/> and
/// <see cref="BattleForceWorld"/>, not stand-ins, because the defect is a relationship between a
/// saved coordinate and the distance an authored trigger actually reaches. A suite that invents its
/// own markers cannot see it.
/// </para>
/// </remarks>
public sealed class ResumedPositionTests
{
    /// <summary>How far the player travels in one frame, matching <see cref="QuestPlayabilityTests"/>.</summary>
    const double UnitsPerFrame = 10;

    /// <summary>Frames the player may fly before the campaign is called unreachable rather than far.</summary>
    const int FrameLimit = 10_000;

    /// <summary>Far enough out that quest 1's start trigger is well behind the player.</summary>
    const double WellPastTheStartMarker = 700;

    /// <summary>
    /// How close quest 1's end marker has to be for it to complete, so the flying tests can assert
    /// arrival without restating the authored distance as a coordinate of their own.
    /// </summary>
    static readonly double Quest1EndTriggerRadius =
        new BattleForceCampaign().Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id)
            .End.Distance;

    readonly ICampaign _campaign = new BattleForceCampaign();
    readonly IWorld _world = new BattleForceWorld();
    readonly InMemorySaveProgressService _saves = new();

    /// <summary>
    /// Saves whose quest entries name nothing the campaign ships, so restoring one leaves the quest
    /// log exactly as a new game's. Every one of them is readable — this is drift, not damage.
    /// </summary>
    public static TheoryData<string, string> SavesHoldingNoCampaignProgress =>
        new()
        {
            { "an entry naming no quest", """{ "QuestId": "  ", "State": "Active" }""" },
            { "an entry naming a dropped quest", """{ "QuestId": "quest-99", "State": "Completed" }""" },
        };

    GameSession CreateSession() => new(_saves, _campaign, _world);

    QuestMarkers Quest1Markers =>
        _world.QuestMarkers.Single(markers => markers.QuestId == BattleForceCampaign.Quest1Id);

    /// <summary>
    /// A save at the given distance out holding the given quest entries verbatim, so the shapes
    /// under test are the JSON a hand-edited or older build's file actually holds.
    /// </summary>
    static string SaveAt(double y, params string[] questEntries) =>
        // invariant, because JSON has one number format and the machine writing a save may not be
        // running the culture the machine reading it is
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            { "PlayerX": 0, "PlayerY": {{y}}, "Quests": [ {{string.Join(", ", questEntries)}} ] }
            """);

    /// <summary>
    /// Flies the player forward — the one direction the game teaches — driving the watcher every
    /// frame, exactly as a frame does, and stops when quest 1 completes or the limit is reached.
    /// Deliberately never flies backwards: a campaign only reachable by reversing into the debris
    /// field quest 1 is about escaping is the defect, not the recovery.
    /// </summary>
    void FlyForwardUntilQuest1Completes(GameSession session)
    {
        QuestProximityWatcher watcher = new(_world);
        Quest quest1 = session.Quests.Find(BattleForceCampaign.Quest1Id)!;

        for (int frame = 0; frame < FrameLimit && !quest1.IsCompleted; frame++)
        {
            watcher.Update(session.Quests, session.Player.Position);
            session.Player.MoveBy(0, UnitsPerFrame);
        }
    }

    // ---- a save holding no campaign progress ----

    [Theory]
    [MemberData(nameof(SavesHoldingNoCampaignProgress))]
    public async Task Continue_PutsThePlayerAtTheWorldStart_WhenTheSaveHoldsNoCampaignProgress(
        string because,
        string questEntry)
    {
        Assert.NotNull(because);
        _saves.Content = SaveAt(WellPastTheStartMarker, questEntry);
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(_world.PlayerStart, session.Player.Position);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task Continue_PutsThePlayerAtTheWorldStart_WhenTheSaveNamesNoQuestAtAll()
    {
        _saves.Content = SaveAt(WellPastTheStartMarker);
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(_world.PlayerStart, session.Player.Position);
    }

    [Fact]
    public async Task Continue_PutsThePlayerAtTheWorldStart_WhenTheSaveNamesQuest1AsNotStarted()
    {
        // the campaign is registered, but nothing in it has been played
        _saves.Content = SaveAt(
            WellPastTheStartMarker,
            $$"""{ "QuestId": "{{BattleForceCampaign.Quest1Id}}", "State": "NotStarted" }""");
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(_world.PlayerStart, session.Player.Position);
    }

    [Theory]
    [MemberData(nameof(SavesHoldingNoCampaignProgress))]
    public async Task Continue_LeavesQuest1Completable_WhenTheSaveHoldsNoCampaignProgress(
        string because,
        string questEntry)
    {
        // Ready is not playable. Quest 1's start trigger is 25 units around a marker at the origin,
        // so a player set down 700 units out with nothing active gets further from the only trigger
        // that could help with every frame of flying forward.
        Assert.NotNull(because);
        _saves.Content = SaveAt(WellPastTheStartMarker, questEntry);
        GameSession session = CreateSession();

        await session.Continue();
        FlyForwardUntilQuest1Completes(session);

        Assert.Equal(QuestState.Completed, session.Quests.Find(BattleForceCampaign.Quest1Id)!.State);
    }

    // ---- a save the campaign has actually been played into ----

    [Theory]
    [InlineData("Active")]
    [InlineData("Completed")]
    public async Task Continue_KeepsTheSavedPosition_WhenTheCampaignHasBegun(string state)
    {
        // the rule must not cost a resumed game its position, which is the whole point of a save
        _saves.Content = SaveAt(
            WellPastTheStartMarker,
            $$"""{ "QuestId": "{{BattleForceCampaign.Quest1Id}}", "State": "{{state}}" }""");
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(new Position(0, WellPastTheStartMarker), session.Player.Position);
    }

    [Fact]
    public async Task Continue_KeepsTheSavedPosition_WhenProgressSitsBesideAnEntryNamingNoQuest()
    {
        // the save from #44's report: one good entry, one junk one, and progress worth keeping
        _saves.Content = SaveAt(
            WellPastTheStartMarker,
            $$"""{ "QuestId": "{{BattleForceCampaign.Quest1Id}}", "State": "Completed" }""",
            """{ "QuestId": "  ", "State": "Active" }""");
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(new Position(0, WellPastTheStartMarker), session.Player.Position);
        Assert.Equal(QuestState.Completed, session.Quests.Find(BattleForceCampaign.Quest1Id)!.State);
    }

    [Fact]
    public async Task Continue_LeavesTheSaveOnDisk_WhenItDeclinesThePosition()
    {
        // declining the position rather than refusing the file is only cheaper if the file survives
        string content = SaveAt(WellPastTheStartMarker, """{ "QuestId": "  ", "State": "Active" }""");
        _saves.Content = content;
        GameSession session = CreateSession();

        await session.Continue();

        Assert.Equal(content, _saves.Content);
        Assert.Null(_saves.SetAsideContent);
        Assert.Equal(0, _saves.SaveCount);
    }

    [Fact]
    public async Task ThePlayerResumedAtTheStart_CanStillCompleteTheCampaignAndSaveIt()
    {
        // the recovery has to be a game, not just a position: flying it through must record it
        _saves.Content = SaveAt(WellPastTheStartMarker, """{ "QuestId": "  ", "State": "Active" }""");
        GameSession session = CreateSession();

        await session.Continue();
        FlyForwardUntilQuest1Completes(session);
        await session.PendingSave;

        // flown forward into quest 1's end trigger, never past the marker and never backwards
        Assert.InRange(session.Player.Position.Y, 0, Quest1Markers.End.Y);
        Assert.True(session.Player.Position.DistanceTo(Quest1Markers.End) <= Quest1EndTriggerRadius);
        Assert.Equal(
            QuestState.Completed,
            _saves.Saved!.Quests.Single(quest => quest.QuestId == BattleForceCampaign.Quest1Id).State);
    }
}
