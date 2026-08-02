namespace Pilgrimage.Tests;

/// <summary>
/// QA's adversarial cover for how far <see cref="QuestLog.Restore"/>'s refusals reach.
/// </summary>
/// <remarks>
/// <para>
/// The cover already on the branch pins that an entry that is not there is refused and that
/// nothing beside it is applied. What none of it pins is the <em>edge</em> of that guarantee. The
/// batch is now walked twice — once to refuse it whole, once to apply it — and only the first walk
/// is a check, so a refusal raised during the second walk leaves the log half restored. That is
/// the state of things and it is defensible, but it was not asserted anywhere, so a later change
/// tightening or loosening it would go unnoticed either way.
/// </para>
/// <para>
/// The distinction matters to a caller: catching one of these two exceptions leaves the log it
/// started with, and catching the other does not.
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

    // ---- where the all-or-nothing guarantee starts and stops ----

    /// <summary>
    /// The guarantee is scoped to an entry that is <em>not there</em>, and this pins that it does
    /// not quietly extend to every refusal. An entry holding a state outside the lifecycle is
    /// refused where it is met rather than before the batch is applied, so the entries ahead of it
    /// have already been applied when the exception arrives.
    /// </summary>
    [Fact]
    public void Restore_HasAlreadyAppliedTheEntriesAhead_WhenItRefusesAStateOutsideTheLifecycle()
    {
        QuestLog log = new();
        Quest ahead = log.Register(Quest("quest-1"));
        Quest behind = log.Register(Quest("quest-2"));

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Restore(
        [
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-2", OutsideTheLifecycle),
        ]));

        // the entry ahead of the refused one is applied; the one it refused is not
        Assert.Equal(QuestState.Completed, ahead.State);
        Assert.Equal(QuestState.NotStarted, behind.State);
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
    /// or rather, it is not, because the entry is skipped before its state is ever looked at.
    /// Pinned because the two rules are checked in the same loop in the order that decides this,
    /// and the order is not obvious from either doc comment.
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
