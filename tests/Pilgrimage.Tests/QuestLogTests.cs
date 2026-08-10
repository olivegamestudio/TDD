namespace Pilgrimage.Tests;

public sealed class QuestLogTests
{
    static QuestDefinition Definition(string id, bool autoStarts = true) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            autoStarts);

    [Fact]
    public void StartsEmpty()
    {
        QuestLog log = new();

        Assert.Empty(log.Quests);
        Assert.Null(log.Find("anything"));
    }

    [Fact]
    public void Register_AddsAQuestThatCanBeFoundById()
    {
        QuestLog log = new();

        log.Register(Definition("quest-1"));

        Quest? quest = log.Find("quest-1");
        Assert.NotNull(quest);
        Assert.Equal("Title of quest-1", quest.Title);
        Assert.Single(log.Quests);
    }

    [Fact]
    public void Register_RejectsADuplicateId()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentException>(() => log.Register(Definition("quest-1")));
    }

    /// <summary>
    /// A definition that is not there and one whose identifier names nothing are two different
    /// mistakes, and the caller has to be able to tell them apart. Reading <c>definition.Id</c> off
    /// a reference that is not there reports neither: a <see cref="NullReferenceException"/> names
    /// no parameter and points at a line inside the log rather than at the call that got it wrong.
    /// </summary>
    [Fact]
    public void Register_RefusesADefinitionThatIsNotThere()
    {
        QuestLog log = new();

        ArgumentNullException refusal =
            Assert.Throws<ArgumentNullException>(() => log.Register(null!));

        Assert.Equal("definition", refusal.ParamName);
    }

    /// <summary>
    /// Every form of identifier that names nothing. A quest registered under one would be captured
    /// with its progress and then restored to nothing, because <see cref="QuestLog.Restore"/> skips
    /// exactly these — so the log refuses them at the door instead of accepting a quest it cannot
    /// bring back.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\u00a0")] // a non-breaking space is whitespace to IsNullOrWhiteSpace, so it is not a name either
    public void Register_RefusesAnIdentifierThatNamesNoQuest(string? id)
    {
        QuestLog log = new();

        ArgumentException refusal =
            Assert.Throws<ArgumentException>(() => log.Register(Definition(id!)));

        Assert.Equal("definition", refusal.ParamName);
    }

    /// <summary>
    /// The refusal is inert: a log that turned a registration away is the log it was beforehand,
    /// not one holding a half-registered quest.
    /// </summary>
    [Fact]
    public void Register_RefusingAnIdentifierThatNamesNoQuest_LeavesTheLogAsItWas()
    {
        QuestLog log = new();
        Quest kept = log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentException>(() => log.Register(Definition("  ")));

        Assert.Single(log.Quests);
        Assert.Same(kept, Assert.Single(log.Quests));
    }

    /// <summary>
    /// The bound of the refusal. An identifier with whitespace <em>in</em> it, or around it, still
    /// names something — only one made of nothing but whitespace does not — so the guard must not
    /// widen into trimming or rejecting identifiers a campaign is entitled to choose.
    /// </summary>
    [Theory]
    [InlineData("quest-1")]
    [InlineData(" quest-1 ")]
    [InlineData("quest 1")]
    [InlineData("0")]
    [InlineData("_")]
    [InlineData("クエスト")]
    public void Register_AcceptsAnyIdentifierThatNamesSomething(string id)
    {
        QuestLog log = new();

        Quest quest = log.Register(Definition(id));

        Assert.Equal(id, quest.Id);
        Assert.Same(quest, log.Find(id));
    }

    [Fact]
    public void RaisesQuestStarted_WithTheQuestThatStarted()
    {
        QuestLog log = new();
        Quest quest = log.Register(Definition("quest-1"));
        Quest? started = null;
        log.QuestStarted += (_, e) => started = e.Quest;

        quest.Start();

        Assert.Same(quest, started);
    }

    [Fact]
    public void RaisesQuestCompleted_WithTheQuestThatCompleted()
    {
        QuestLog log = new();
        Quest quest = log.Register(Definition("quest-1"));
        quest.Start();
        Quest? completed = null;
        log.QuestCompleted += (_, e) => completed = e.Quest;

        quest.Complete();

        Assert.Same(quest, completed);
    }

    [Fact]
    public void SeparatesActiveFromCompletedQuests()
    {
        QuestLog log = new();
        Quest first = log.Register(Definition("quest-1"));
        log.Register(Definition("quest-2"));

        first.Start();

        Assert.Equal(["quest-1"], log.Active.Select(quest => quest.Id));
        Assert.Empty(log.Completed);

        first.Complete();

        Assert.Empty(log.Active);
        Assert.Equal(["quest-1"], log.Completed.Select(quest => quest.Id));
    }

    [Fact]
    public void Clear_RemovesEveryQuest()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Clear();

        Assert.Empty(log.Quests);
        Assert.Null(log.Find("quest-1"));
    }

    [Fact]
    public void Clear_StopsARemovedQuestFromRaisingEvents()
    {
        // a stale subscription would report progress for a quest no longer in the log
        QuestLog log = new();
        Quest quest = log.Register(Definition("quest-1"));
        int started = 0;
        log.QuestStarted += (_, _) => started++;

        log.Clear();
        quest.Start();

        Assert.Equal(0, started);
    }

    // ---- persistence ----

    [Fact]
    public void Capture_ReportsTheStateOfEveryQuest()
    {
        QuestLog log = new();
        Quest first = log.Register(Definition("quest-1"));
        log.Register(Definition("quest-2"));
        first.Start();

        Assert.Equal(
            [new QuestProgress("quest-1", QuestState.Active), new QuestProgress("quest-2", QuestState.NotStarted)],
            log.Capture());
    }

    [Fact]
    public void Restore_PutsEveryQuestBackWithoutRaisingEvents()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Register(Definition("quest-2"));
        int events = 0;
        log.QuestStarted += (_, _) => events++;
        log.QuestCompleted += (_, _) => events++;

        log.Restore([
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-2", QuestState.Active),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
        Assert.Equal(QuestState.Active, log.Find("quest-2")!.State);
        Assert.Equal(0, events);
    }

    [Fact]
    public void Restore_IgnoresAQuestThatIsNoLongerRegistered()
    {
        // a save written by an older build must still load
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore([
            new QuestProgress("quest-1", QuestState.Active),
            new QuestProgress("quest-removed", QuestState.Completed),
        ]);

        Assert.Single(log.Quests);
        Assert.Equal(QuestState.Active, log.Find("quest-1")!.State);
    }

    [Fact]
    public void Restore_LeavesAQuestTheSaveSaysNothingAboutAlone()
    {
        // a quest added since the save was written starts from the beginning
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Register(Definition("quest-added-since"));

        log.Restore([new QuestProgress("quest-1", QuestState.Completed)]);

        Assert.Equal(QuestState.NotStarted, log.Find("quest-added-since")!.State);
    }

    // ---- an entry that names no quest, and an entry that is not there ----
    // Two shapes that look alike and are not the same. An entry naming no quest is drift of the
    // kind above — it matches nothing registered, so there is nothing to apply it to and it is
    // skipped. An entry that is *not there* is not drift: no build wrote it, and the batch is
    // refused. A game reading its own save file draws the same line one file earlier.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Restore_SkipsAnEntryNamingNoQuest_AndKeepsTheProgressBesideIt(string? questId)
    {
        // An identifier that is absent names a quest exactly as poorly as one that is merely
        // unknown, and refusing the batch over it would throw away the progress standing beside
        // it — which is the part that was real. A null id used to come out of the dictionary as
        // ArgumentNullException, which is neither a skip nor a refusal, just a crash.
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore([
            new QuestProgress(questId!, QuestState.Active),
            new QuestProgress("quest-1", QuestState.Completed),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }

    [Fact]
    public void Restore_RefusesAnEntryThatIsNotThere_AndAppliesNothingBesideIt()
    {
        // The refusal has to be all-or-nothing, or a caller that catches it is left holding half a
        // save: quest-1 restored, the rest of the batch abandoned mid-way. It reached callers as a
        // NullReferenceException before, which is a crash rather than a contract.
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentException>("progress", () => log.Restore([
            new QuestProgress("quest-1", QuestState.Completed),
            null!,
        ]));

        Assert.Equal(QuestState.NotStarted, log.Find("quest-1")!.State);
    }

    // ---- a save that names the same quest twice ----

    [Fact]
    public void Restore_KeepsTheFurthestState_WhenALaterEntryWouldHandProgressBack()
    {
        // the case that decides the rule: the campaign is finished, and an entry standing behind
        // it says otherwise. Applying entries in order would undo a completed campaign on the
        // strength of whichever one the file happened to list last.
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore([
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-1", QuestState.NotStarted),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }

    [Fact]
    public void Restore_TakesALaterEntryThatCarriesTheQuestFurther()
    {
        // the other direction, so the rule is "keep the furthest" rather than "keep the first"
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore([
            new QuestProgress("quest-1", QuestState.NotStarted),
            new QuestProgress("quest-1", QuestState.Completed),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }

    [Theory]
    [InlineData(QuestState.NotStarted, QuestState.Active, QuestState.Completed)]
    [InlineData(QuestState.Completed, QuestState.Active, QuestState.NotStarted)]
    [InlineData(QuestState.Active, QuestState.Completed, QuestState.NotStarted)]
    [InlineData(QuestState.Completed, QuestState.NotStarted, QuestState.Active)]
    public void Restore_ReachesTheSameStateWhateverOrderTheEntriesAreIn(
        QuestState first, QuestState second, QuestState third)
    {
        // the property the rule is chosen for. A duplicate means a hand-edited or merged file, and
        // the order entries ended up in is an accident of whoever merged it — so the answer must
        // not depend on it.
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore([
            new QuestProgress("quest-1", first),
            new QuestProgress("quest-1", second),
            new QuestProgress("quest-1", third),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }

    [Fact]
    public void Restore_ResolvesDuplicatesWithinOneCall_AndNotAcrossCalls()
    {
        // the rule is about one save disagreeing with itself, not about the log refusing to move
        // backwards ever. A later Restore is a different save being read, and it is authoritative.
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Restore([new QuestProgress("quest-1", QuestState.Completed)]);

        log.Restore([new QuestProgress("quest-1", QuestState.NotStarted)]);

        Assert.Equal(QuestState.NotStarted, log.Find("quest-1")!.State);
    }

    [Fact]
    public void Restore_ResolvesDuplicatesPerQuest_NotAcrossTheWholeSave()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Register(Definition("quest-2"));

        log.Restore([
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-2", QuestState.NotStarted),
            new QuestProgress("quest-1", QuestState.NotStarted),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
        Assert.Equal(QuestState.NotStarted, log.Find("quest-2")!.State);
    }

    [Fact]
    public void Restore_StillRefusesAStateThatIsNotAState_InADuplicateEntry()
    {
        // a state outside the lifecycle is refused wherever it appears. Treating it as merely
        // "behind" would drop it silently in second place and throw in first, which is the kind of
        // answer-by-position this rule exists to remove.
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Restore([
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("quest-1", (QuestState)99),
        ]));
    }

    [Fact]
    public void Restore_RaisesNoEvents_ForADuplicatedQuest()
    {
        // restoring never replays the moments the player already lived, however many entries name
        // the quest
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        int events = 0;
        log.QuestStarted += (_, _) => events++;
        log.QuestCompleted += (_, _) => events++;

        log.Restore([
            new QuestProgress("quest-1", QuestState.Active),
            new QuestProgress("quest-1", QuestState.Completed),
        ]);

        Assert.Equal(0, events);
    }
}
