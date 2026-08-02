namespace Pilgrimage.Tests;

/// <summary>
/// QA's adversarial cover for the rule #72 decided: a save naming one quest more than once
/// restores it to the furthest state it names.
/// </summary>
/// <remarks>
/// <para>
/// The cover already on the branch pins the two-entry case and the six orderings of three states.
/// These attack the same rule from where it is easier to get wrong: many entries rather than two
/// or three, several quests duplicated at once, and an expected value computed by an oracle that
/// knows nothing about the loop — the furthest state named, taken directly — rather than written
/// out by hand per case.
/// </para>
/// <para>
/// The property being defended is the one the decision was made for: whatever a file says and in
/// whatever order it says it, restoring must never leave the player behind the furthest progress
/// the file records. A rule that resolved duplicates by position would satisfy the existing tests
/// on some inputs and fail these.
/// </para>
/// </remarks>
public sealed class QuestLogDuplicateEntryAdversarialTests
{
    static QuestDefinition Definition(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true);

    static readonly QuestState[] States =
        [QuestState.NotStarted, QuestState.Active, QuestState.Completed];

    public static TheoryData<int> Seeds
    {
        get
        {
            TheoryData<int> seeds = [];
            for (int seed = 0; seed < 250; seed++)
            {
                seeds.Add(seed);
            }

            return seeds;
        }
    }

    /// <summary>
    /// The rule, checked against an oracle rather than against hand-written expectations: for any
    /// number of entries naming one quest, in any order, the quest lands on the furthest state
    /// named. Up to twelve entries, which is far past anything the existing cover reaches.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Restore_LandsOnTheFurthestStateNamed_ForAnyNumberOfEntriesInAnyOrder(int seed)
    {
        Random random = new(seed);

        QuestLog log = new();
        log.Register(Definition("quest-1"));

        int count = random.Next(1, 13);
        List<QuestProgress> entries = [];
        for (int entry = 0; entry < count; entry++)
        {
            entries.Add(new QuestProgress("quest-1", States[random.Next(States.Length)]));
        }

        // the oracle: the furthest state the file names, with no knowledge of how Restore walks it
        QuestState furthest = entries.Max(e => e.State);

        log.Restore(entries);

        Assert.Equal(furthest, log.Find("quest-1")!.State);
    }

    /// <summary>
    /// The same property with several quests duplicated and interleaved in one file. Each quest
    /// must be resolved against its own entries only — a shared "furthest so far" would show up
    /// here as one quest dragging another forward.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Restore_ResolvesEachQuestAgainstItsOwnEntriesOnly(int seed)
    {
        Random random = new(seed);

        string[] ids = ["quest-1", "quest-2", "quest-3"];
        QuestLog log = new();
        foreach (string id in ids)
        {
            log.Register(Definition(id));
        }

        List<QuestProgress> entries = [];
        int count = random.Next(1, 20);
        for (int entry = 0; entry < count; entry++)
        {
            entries.Add(new QuestProgress(
                ids[random.Next(ids.Length)],
                States[random.Next(States.Length)]));
        }

        log.Restore(entries);

        foreach (string id in ids)
        {
            // a quest the file never names stays where it started
            QuestState expected = entries.Any(e => e.QuestId == id)
                ? entries.Where(e => e.QuestId == id).Max(e => e.State)
                : QuestState.NotStarted;

            Assert.Equal(expected, log.Find(id)!.State);
        }
    }

    /// <summary>
    /// The decision's whole purpose, stated as the property it exists to guarantee: restoring can
    /// never leave a quest behind the furthest progress the file records. Checked over the same
    /// spread, because "never loses progress" is the claim a player would care about and it is
    /// stronger than any single expected value.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Restore_NeverLandsBehindTheFurthestProgressTheFileRecords(int seed)
    {
        Random random = new(seed);

        QuestLog log = new();
        log.Register(Definition("quest-1"));

        List<QuestProgress> entries = [];
        int count = random.Next(2, 9);
        for (int entry = 0; entry < count; entry++)
        {
            entries.Add(new QuestProgress("quest-1", States[random.Next(States.Length)]));
        }

        log.Restore(entries);

        foreach (QuestProgress entry in entries)
        {
            Assert.True(
                log.Find("quest-1")!.State >= entry.State,
                $"restored to {log.Find("quest-1")!.State} but the file records {entry.State}");
        }
    }

    /// <summary>
    /// A hundred entries of the same quest still resolve, and still resolve to the furthest. Long
    /// runs are where a rule that accidentally compared against the previous entry rather than
    /// against the quest's current state would drift.
    /// </summary>
    [Fact]
    public void Restore_ResolvesAHundredEntriesForOneQuest()
    {
        Random random = new(12345);

        QuestLog log = new();
        log.Register(Definition("quest-1"));

        List<QuestProgress> entries = [];
        for (int entry = 0; entry < 100; entry++)
        {
            entries.Add(new QuestProgress("quest-1", States[random.Next(States.Length)]));
        }

        log.Restore(entries);

        Assert.Equal(entries.Max(e => e.State), log.Find("quest-1")!.State);
    }

    /// <summary>
    /// A state that is not a state is refused wherever it sits in the run, not only in the first
    /// entry. This is the case the rule could most easily have swallowed: an unrecognised value
    /// standing behind a real one looks droppable, and dropping it would restore the very
    /// answer-by-position that #72 exists to remove.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Restore_RefusesAStateThatIsNotAState_WhereverItAppearsInTheRun(int position)
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        List<QuestProgress> entries =
        [
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-1", QuestState.Active),
            new QuestProgress("quest-1", QuestState.NotStarted),
        ];
        entries.Insert(position, new QuestProgress("quest-1", (QuestState)99));

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Restore(entries));
    }

    /// <summary>
    /// A negative value is not "before NotStarted" — it is not on the lifecycle at all, so it is
    /// refused rather than quietly treated as the earliest possible progress and dropped.
    /// </summary>
    [Fact]
    public void Restore_RefusesANegativeState_RatherThanReadingItAsEarlyProgress()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Restore(
        [
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-1", (QuestState)(-1)),
        ]));
    }

    /// <summary>
    /// Reading a duplicated save and writing it straight back out produces a file with the
    /// duplicate resolved — one entry per quest, at the furthest state. The duplicate is a
    /// property of the file rather than of the log, so it must not survive a round trip.
    /// </summary>
    [Fact]
    public void Capture_AfterRestoringADuplicate_WritesOneEntryAtTheFurthestState()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore(
        [
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-1", QuestState.NotStarted),
        ]);

        IReadOnlyList<QuestProgress> captured = log.Capture();

        QuestProgress only = Assert.Single(captured.Where(e => e.QuestId == "quest-1"));
        Assert.Equal(QuestState.Completed, only.State);
    }

    /// <summary>
    /// The rule is scoped to one call, and this pins the boundary from the far side: a second
    /// <see cref="QuestLog.Restore"/> is a different save being read and is authoritative, so it
    /// may move a quest backwards. Without this, "never hands progress back" could quietly widen
    /// into "a log can never move backwards", which would break loading an earlier save.
    /// </summary>
    [Fact]
    public void Restore_FromASecondSave_MayMoveAQuestBackwards()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore([new QuestProgress("quest-1", QuestState.Completed)]);
        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);

        log.Restore([new QuestProgress("quest-1", QuestState.NotStarted)]);

        Assert.Equal(QuestState.NotStarted, log.Find("quest-1")!.State);
    }

    /// <summary>
    /// Duplicate entries naming a quest this build no longer ships are ignored as a group, and do
    /// not consume or disturb the resolution of the quests that are still shipped beside them.
    /// </summary>
    [Fact]
    public void Restore_IgnoresDuplicatesOfAQuestTheCampaignNoLongerShips()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore(
        [
            new QuestProgress("retired-quest", QuestState.Completed),
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("retired-quest", QuestState.NotStarted),
            new QuestProgress("quest-1", QuestState.NotStarted),
        ]);

        Assert.Null(log.Find("retired-quest"));
        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }
}
