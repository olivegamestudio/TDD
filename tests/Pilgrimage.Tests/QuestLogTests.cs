namespace Pilgrimage.Tests;

public sealed class QuestLogTests
{
    static readonly QuestMarker Start = new(new Position(0, 0), 25);
    static readonly QuestMarker End = new(new Position(0, 1000), 50);

    static QuestDefinition Definition(string id, bool autoStarts = true) =>
        new(id, $"Title of {id}", Start, End, autoStarts);

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
    public void Update_DrivesEveryRegisteredQuest()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Register(Definition("quest-2"));

        log.Update(new Position(0, 0));

        Assert.All(log.Quests, quest => Assert.Equal(QuestState.Active, quest.State));
    }

    [Fact]
    public void RaisesQuestStarted_WithTheQuestThatStarted()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        Quest? started = null;
        log.QuestStarted += (_, e) => started = e.Quest;

        log.Update(new Position(0, 0));

        Assert.NotNull(started);
        Assert.Equal("quest-1", started.Id);
    }

    [Fact]
    public void RaisesQuestCompleted_WithTheQuestThatCompleted()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Update(new Position(0, 0));
        Quest? completed = null;
        log.QuestCompleted += (_, e) => completed = e.Quest;

        log.Update(new Position(0, 1000));

        Assert.NotNull(completed);
        Assert.Equal("quest-1", completed.Id);
    }

    [Fact]
    public void SeparatesActiveFromCompletedQuests()
    {
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        log.Register(Definition("quest-2", autoStarts: false));

        log.Update(new Position(0, 0));

        Assert.Equal(["quest-1"], log.Active.Select(quest => quest.Id));
        Assert.Empty(log.Completed);

        log.Update(new Position(0, 1000));

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
        // a stale subscription would fire a save for a quest no longer in the log
        QuestLog log = new();
        log.Register(Definition("quest-1"));
        Quest quest = log.Find("quest-1")!;
        int started = 0;
        log.QuestStarted += (_, _) => started++;

        log.Clear();
        quest.Start();

        Assert.Equal(0, started);
    }
}
