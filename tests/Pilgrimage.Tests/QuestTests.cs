namespace Pilgrimage.Tests;

public sealed class QuestTests
{
    static Quest CreateQuest(bool autoStarts = true) =>
        new(new QuestDefinition(
            "quest-under-test",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            autoStarts));

    [Fact]
    public void BeginsNotStarted()
    {
        Quest quest = CreateQuest();

        Assert.Equal(QuestState.NotStarted, quest.State);
        Assert.False(quest.IsActive);
        Assert.False(quest.IsCompleted);
    }

    [Fact]
    public void ExposesItsIdentityAndTriggersFromTheDefinition()
    {
        Quest quest = CreateQuest();

        Assert.Equal("quest-under-test", quest.Id);
        Assert.Equal("A title", quest.Title);
        Assert.Equal(25, quest.Definition.Start.Distance);
        Assert.Equal(50, quest.Definition.End.Distance);
    }

    [Fact]
    public void Start_MakesTheQuestActive()
    {
        Quest quest = CreateQuest();

        quest.Start();

        Assert.Equal(QuestState.Active, quest.State);
        Assert.True(quest.IsActive);
    }

    [Fact]
    public void Start_RaisesStartedOnce_HoweverOftenItIsCalled()
    {
        // the caller watching proximity has no memory; it may report arrival every frame
        Quest quest = CreateQuest();
        int started = 0;
        quest.Started += (_, _) => started++;

        quest.Start();
        quest.Start();
        quest.Start();

        Assert.Equal(1, started);
    }

    [Fact]
    public void Complete_FinishesAnActiveQuest()
    {
        Quest quest = CreateQuest();
        quest.Start();

        quest.Complete();

        Assert.Equal(QuestState.Completed, quest.State);
        Assert.True(quest.IsCompleted);
        Assert.False(quest.IsActive);
    }

    [Fact]
    public void Complete_RaisesCompletedOnce_HoweverOftenItIsCalled()
    {
        Quest quest = CreateQuest();
        quest.Start();
        int completed = 0;
        quest.Completed += (_, _) => completed++;

        quest.Complete();
        quest.Complete();

        Assert.Equal(1, completed);
    }

    [Fact]
    public void Complete_IsIgnored_BeforeTheQuestHasStarted()
    {
        // standing on the end marker without ever starting is not a completion
        Quest quest = CreateQuest();
        int completed = 0;
        quest.Completed += (_, _) => completed++;

        quest.Complete();

        Assert.Equal(QuestState.NotStarted, quest.State);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void Start_IsIgnored_AfterTheQuestHasCompleted()
    {
        Quest quest = CreateQuest();
        quest.Start();
        quest.Complete();

        quest.Start();

        Assert.Equal(QuestState.Completed, quest.State);
    }

    [Fact]
    public void Restore_PutsTheQuestIntoASavedState_WithoutRaisingEvents()
    {
        Quest quest = CreateQuest();
        int events = 0;
        quest.Started += (_, _) => events++;
        quest.Completed += (_, _) => events++;

        quest.Restore(QuestState.Active);

        Assert.Equal(QuestState.Active, quest.State);
        Assert.Equal(0, events);
    }

    [Fact]
    public void Restore_ToActive_CanStillBeCompleted()
    {
        // the shape of a resumed save game: carry on and finish the quest
        Quest quest = CreateQuest();
        quest.Restore(QuestState.Active);

        quest.Complete();

        Assert.Equal(QuestState.Completed, quest.State);
    }
}
