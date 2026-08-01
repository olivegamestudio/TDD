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

    // ---- what Restore refuses ----
    //
    // Restore is forgiving about which quests a save names, because saves and campaigns drift apart.
    // It is not forgiving about progress it cannot read at all: a game that hands it one of these
    // has a defect somewhere earlier, and saying so is more use than a NullReferenceException from
    // inside the loop.

    [Fact]
    public void Restore_RefusesAMissingList()
    {
        QuestLog log = new();

        Assert.Throws<ArgumentNullException>("progress", () => log.Restore(null!));
    }

    [Fact]
    public void Restore_RefusesAMissingEntry()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentException>("progress", () => log.Restore([null!]));
    }

    [Fact]
    public void Restore_RefusesAnEntryWithNoQuestId()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentException>("progress", () => log.Restore([new QuestProgress(null!, QuestState.Active)]));
    }

    [Fact]
    public void Restore_RefusesAStateThatIsNotAState()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        Assert.Throws<ArgumentOutOfRangeException>("progress", () =>
            log.Restore([new QuestProgress("quest-1", (QuestState)99)]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(99)]
    public void Restore_ChangesNothingWhenItRefuses(int? badState)
    {
        // A refusal part-way through used to leave the log half restored: the quests before the bad
        // entry moved, the ones after it did not, and the game carried on with a mixture of the save
        // and a new game. Either the whole save is applied or none of it is.
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Register(Definition("quest-2"));
        QuestProgress bad = badState is null
            ? null!
            : new QuestProgress("quest-2", (QuestState)badState.Value);

        Assert.ThrowsAny<ArgumentException>(() => log.Restore([
            new QuestProgress("quest-1", QuestState.Completed),
            bad,
        ]));

        Assert.Equal(QuestState.NotStarted, log.Find("quest-1")!.State);
        Assert.Equal(QuestState.NotStarted, log.Find("quest-2")!.State);
    }

    [Fact]
    public void Restore_AcceptsAnEntryWithABlankQuestId_AsAQuestItDoesNotHave()
    {
        // blank is not unreadable — it names no registered quest, which is the drift case Restore
        // already tolerates. A game that never writes blank ids rejects them at its own save
        // boundary; the library has no business guessing that for it.
        QuestLog log = new();
        log.Register(Definition("quest-1"));

        log.Restore([new QuestProgress("", QuestState.Active), new QuestProgress("quest-1", QuestState.Active)]);

        Assert.Equal(QuestState.Active, log.Find("quest-1")!.State);
    }

    [Fact]
    public void Find_RefusesAMissingId()
    {
        // the dictionary throws on a null key; documented rather than quietly turned into "no quest"
        QuestLog log = new();

        Assert.Throws<ArgumentNullException>("id", () => log.Find(null!));
    }
}
