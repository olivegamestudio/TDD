namespace Pilgrimage.Tests;

/// <summary>
/// QA's adversarial cover for how far <see cref="QuestLog.Restore"/>'s refusals reach.
/// </summary>
/// <remarks>
/// <para>
/// The cover already on the branch pins that an entry that is not there is refused and that
/// nothing beside it is applied. What none of it pinned was the <em>edge</em> of that guarantee,
/// and the edge turned out to be in the wrong place: the batch was walked twice, once to refuse it
/// whole and once to apply it, but only the entries that were <c>null</c> were checked in the first
/// walk. A state outside the lifecycle was met during the second, so the entries ahead of it had
/// already been applied when it threw — a log that was neither what it was before the call nor what
/// the save described, and one whose contents depended on the order the entries happened to be in
/// (#161).
/// </para>
/// <para>
/// Both refusals are now raised before anything is applied, and these tests say so from the
/// caller's side: whichever of the two exceptions a caller catches, it still has the log it started
/// with.
/// </para>
/// </remarks>
public sealed class QuestLogRestoreScopeAdversarialTests
{
    static QuestDefinition Quest(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50));

    /// <summary>
    /// The exact state outside the lifecycle used throughout: a number no <see cref="QuestState"/>
    /// member has, which no save this build wrote can hold because states are persisted by name.
    /// </summary>
    const QuestState OutsideTheLifecycle = (QuestState)99;

    // ---- how far the all-or-nothing guarantee reaches ----

    /// <summary>
    /// A state outside the lifecycle is refused before any of the batch is applied, so the entries
    /// standing ahead of it are left alone. This is #161: the check used to run as the entry was
    /// met, which left the log holding the first quest's saved state and not the second's — neither
    /// the log the caller had before the call nor the one the save described, and nothing it could
    /// finish or undo.
    /// </summary>
    [Fact]
    public void Restore_HasAppliedNothing_WhenItRefusesAStateOutsideTheLifecycle()
    {
        QuestLog log = new();
        Quest ahead = log.Register(Quest("quest-1"));
        Quest behind = log.Register(Quest("quest-2"));

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Restore(
        [
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-2", OutsideTheLifecycle),
        ]));

        Assert.Equal(QuestState.NotStarted, ahead.State);
        Assert.Equal(QuestState.NotStarted, behind.State);
    }

    /// <summary>
    /// The same two entries in either order leave the log in the same place: untouched. The
    /// duplicate rule was chosen because its answer does not depend on the order entries ended up
    /// in after a merge, and a refusal that half-applied the batch handed that dependence straight
    /// back — the same file restored differently depending on which line came first.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restore_HasAppliedNothing_WhicheverSideOfTheBatchTheStateOutsideTheLifecycleSitsOn(
        bool refusedEntryFirst)
    {
        QuestLog log = new();
        Quest good = log.Register(Quest("quest-1"));
        Quest bad = log.Register(Quest("quest-2"));

        QuestProgress[] entries = refusedEntryFirst
            ?
            [
                new QuestProgress("quest-2", OutsideTheLifecycle),
                new QuestProgress("quest-1", QuestState.Completed),
            ]
            :
            [
                new QuestProgress("quest-1", QuestState.Completed),
                new QuestProgress("quest-2", OutsideTheLifecycle),
            ];

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Restore(entries));

        Assert.Equal(QuestState.NotStarted, good.State);
        Assert.Equal(QuestState.NotStarted, bad.State);
    }

    /// <summary>
    /// Refusing the batch earlier does not widen what is refused. An entry naming a quest this
    /// build no longer ships is drift, and drift is ignored — including the state it carries, which
    /// belongs to a quest that is not here to be bricked by it. Pinned because the check that now
    /// runs before the batch is applied is the one place that could start reading states belonging
    /// to quests it will never apply them to.
    /// </summary>
    [Fact]
    public void Restore_StillIgnoresAnEntryNamingAQuestThatIsNotRegistered_WhateverStateItHolds()
    {
        QuestLog log = new();
        Quest quest = log.Register(Quest("quest-1"));

        log.Restore(
        [
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-from-an-older-build", OutsideTheLifecycle),
        ]);

        Assert.Equal(QuestState.Completed, quest.State);
    }

    /// <summary>
    /// The other half of the same boundary, and the one the doc comment promises: an entry that is
    /// not there is caught before anything is applied, however far down the batch it sits and
    /// whatever stands ahead of it.
    /// </summary>
    [Fact]
    public void Restore_HasAppliedNothing_WhenItRefusesAnEntryThatIsNotThere()
    {
        QuestLog log = new();
        Quest ahead = log.Register(Quest("quest-1"));
        Quest behind = log.Register(Quest("quest-2"));

        Assert.Throws<ArgumentException>(() => log.Restore(
        [
            new QuestProgress("quest-1", QuestState.Completed),
            null!,
            new QuestProgress("quest-2", QuestState.Active),
        ]));

        Assert.Equal(QuestState.NotStarted, ahead.State);
        Assert.Equal(QuestState.NotStarted, behind.State);
    }

    /// <summary>
    /// An entry that is not there outranks an entry naming no quest: the file is refused rather
    /// than read with the junk dropped. The two rules meet in one batch here, because the skip runs
    /// in the second walk and the refusal in the first, so an implementation that folded them into
    /// one loop would answer "read it" for this batch instead.
    /// </summary>
    [Fact]
    public void Restore_RefusesTheBatch_WhenAnEntryThatIsNotThereStandsBesideEntriesNamingNoQuest()
    {
        QuestLog log = new();
        Quest quest = log.Register(Quest("quest-1"));

        Assert.Throws<ArgumentException>(() => log.Restore(
        [
            new QuestProgress("   ", QuestState.Active),
            new QuestProgress("quest-1", QuestState.Completed),
            null!,
        ]));

        Assert.Equal(QuestState.NotStarted, quest.State);
    }

    /// <summary>
    /// A state outside the lifecycle is still refused when the entry carrying it names no quest —
    /// or rather, it is not, because the entry is skipped before its state is ever looked at. The
    /// skip outranks the refusal, and it has to keep doing so now that the refusal runs in a walk
    /// of its own: an entry naming nothing is drift whatever it says about a quest it does not
    /// name, and refusing the file over it would throw away the progress standing beside it.
    /// </summary>
    [Fact]
    public void Restore_SkipsAnEntryNamingNoQuest_BeforeItsStateIsJudged()
    {
        QuestLog log = new();
        Quest quest = log.Register(Quest("quest-1"));

        log.Restore([new QuestProgress("", OutsideTheLifecycle)]);

        Assert.Equal(QuestState.NotStarted, quest.State);
    }

    // ---- the premise the skip rests on ----

    /// <summary>
    /// The premise the skip rests on, now that <see cref="QuestLog.Register"/> enforces it (#106):
    /// progress made on a registered quest survives a capture-and-restore round trip, whatever the
    /// campaign chose to call it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was <c>Skip</c>-ped, and it registered a quest under a blank identifier to show the
    /// premise failing: captured <see cref="QuestState.Completed"/>, restored to
    /// <see cref="QuestState.NotStarted"/>, silently. That setup is now impossible — the blank
    /// registration is refused at the door — so the test keeps its claim and drops the one
    /// identifier that can no longer reach a log. What it asserts is unchanged and is still the
    /// outcome rather than the mechanism: nothing a campaign can register is lost by saving it.
    /// </para>
    /// <para>
    /// The identifiers are chosen to be awkward rather than tidy. An identifier that is only
    /// <em>mostly</em> whitespace is the interesting case, because it is the nearest thing to the
    /// one now refused and it must still round-trip.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("quest-1")]
    [InlineData("quest 1")] // a space in it, not made of nothing but space
    [InlineData("  x  ")]
    [InlineData("0")]
    [InlineData("クエスト")]
    public void Restore_BringsBackTheProgressOfEveryRegisteredQuest_WhateverItsIdentifier(string id)
    {
        QuestLog saved = new();
        Quest before = saved.Register(Quest(id));
        before.Start();
        before.Complete();

        QuestLog reloaded = new();
        Quest after = reloaded.Register(Quest(id));
        reloaded.Restore(saved.Capture());

        Assert.Equal(QuestState.Completed, after.State);
    }

    /// <summary>
    /// The two halves of the rule, checked against each other rather than each against itself: an
    /// identifier <see cref="QuestLog.Restore"/> skips is one <see cref="QuestLog.Register"/>
    /// refuses. That agreement is the whole of #106 — while it held only by assumption, a quest
    /// could be registered under an identifier its own log guaranteed to throw away.
    /// </summary>
    /// <remarks>
    /// Written as one test over both so the pair cannot drift apart. A later change that widened
    /// the skip without widening the refusal — or the other way about — reopens the defect, and
    /// this fails rather than the round trip quietly starting to lose a quest again.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("\u00a0")] // a non-breaking space, which is whitespace too
    public void AnIdentifierRestoreSkips_IsOneRegisterRefuses(string? id)
    {
        QuestLog log = new();
        Quest registered = log.Register(Quest("quest-1"));

        // Restore skips it: the entry names no quest, so the log is left as it was...
        log.Restore([new QuestProgress(id!, QuestState.Completed)]);
        Assert.Equal(QuestState.NotStarted, registered.State);

        // ...and because it would be skipped, no quest can be registered under it in the first place
        Assert.Throws<ArgumentException>(() => log.Register(Quest(id!)));
    }
}
