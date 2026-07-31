namespace Pilgrimage.Tests;

public sealed class QuestTests
{
    static readonly QuestMarker Start = new(new Position(0, 0), 25);
    static readonly QuestMarker End = new(new Position(0, 1000), 50);

    static Quest CreateQuest(bool autoStarts = true) =>
        new(new QuestDefinition("quest-under-test", "A title", Start, End, autoStarts));

    [Fact]
    public void BeginsNotStarted()
    {
        Quest quest = CreateQuest();

        Assert.Equal(QuestState.NotStarted, quest.State);
        Assert.False(quest.IsActive);
        Assert.False(quest.IsCompleted);
    }

    [Fact]
    public void ExposesItsIdentityFromTheDefinition()
    {
        Quest quest = CreateQuest();

        Assert.Equal("quest-under-test", quest.Id);
        Assert.Equal("A title", quest.Title);
    }

    [Fact]
    public void AutoStarts_WhenThePlayerIsAtTheStartMarker()
    {
        Quest quest = CreateQuest();

        quest.Update(new Position(0, 0));

        Assert.Equal(QuestState.Active, quest.State);
        Assert.True(quest.IsActive);
    }

    [Fact]
    public void DoesNotAutoStart_WhenThePlayerIsAwayFromTheStartMarker()
    {
        Quest quest = CreateQuest();

        quest.Update(new Position(0, 500));

        Assert.Equal(QuestState.NotStarted, quest.State);
    }

    [Fact]
    public void DoesNotAutoStart_WhenTheDefinitionOptsOut()
    {
        Quest quest = CreateQuest(autoStarts: false);

        quest.Update(new Position(0, 0));

        Assert.Equal(QuestState.NotStarted, quest.State);
    }

    [Fact]
    public void RaisesStartedOnce_HoweverManyFramesPass()
    {
        Quest quest = CreateQuest();
        int started = 0;
        quest.Started += (_, _) => started++;

        quest.Update(new Position(0, 0));
        quest.Update(new Position(0, 0));
        quest.Update(new Position(0, 10));

        Assert.Equal(1, started);
    }

    [Fact]
    public void StaysInProgress_WhileTheEndMarkerIsNotReached()
    {
        Quest quest = CreateQuest();
        quest.Start();

        quest.Update(new Position(0, 500));
        quest.Update(new Position(0, 940));

        Assert.Equal(QuestState.Active, quest.State);
        Assert.False(quest.IsCompleted);
    }

    [Fact]
    public void Completes_WhenTheEndMarkerIsReached()
    {
        Quest quest = CreateQuest();
        quest.Start();

        quest.Update(new Position(0, 960));   // within the 50 unit end radius

        Assert.Equal(QuestState.Completed, quest.State);
        Assert.True(quest.IsCompleted);
        Assert.False(quest.IsActive);
    }

    [Fact]
    public void RaisesCompletedOnce_HoweverManyFramesPass()
    {
        Quest quest = CreateQuest();
        quest.Start();
        int completed = 0;
        quest.Completed += (_, _) => completed++;

        quest.Update(new Position(0, 1000));
        quest.Update(new Position(0, 1000));
        quest.Update(new Position(0, 1010));

        Assert.Equal(1, completed);
    }

    [Fact]
    public void DoesNotComplete_BeforeItHasStarted()
    {
        // standing on the end marker without ever starting is not a completion
        Quest quest = CreateQuest(autoStarts: false);

        quest.Update(new Position(0, 1000));

        Assert.Equal(QuestState.NotStarted, quest.State);
    }

    [Fact]
    public void DoesNotRestart_AfterCompleting()
    {
        Quest quest = CreateQuest();
        quest.Start();
        quest.Update(new Position(0, 1000));

        quest.Update(new Position(0, 0));     // back at the start marker

        Assert.Equal(QuestState.Completed, quest.State);
    }

    [Fact]
    public void Start_IsIgnored_WhenAlreadyStarted()
    {
        Quest quest = CreateQuest();
        int started = 0;
        quest.Started += (_, _) => started++;

        quest.Start();
        quest.Start();

        Assert.Equal(1, started);
    }

    [Fact]
    public void Restore_PutsTheQuestBackIntoASavedState_WithoutRaisingEvents()
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

        quest.Update(new Position(0, 1000));

        Assert.Equal(QuestState.Completed, quest.State);
    }
}
