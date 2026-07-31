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
}
