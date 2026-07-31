using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the presentation half of a proximity quest: measuring the player against the world's
/// markers and driving the quest API from the result.
/// </summary>
public sealed class QuestProximityWatcherTests
{
    static readonly Position StartMarker = new(0, 0);
    static readonly Position EndMarker = new(0, 1000);

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => StartMarker;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    readonly QuestLog _quests = new();

    QuestProximityWatcher CreateWatcher(bool autoStarts = true, double startDistance = 25, double endDistance = 50)
    {
        _quests.Register(new QuestDefinition(
            "quest-1",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, startDistance),
            new QuestTrigger(QuestTriggerKind.Proximity, endDistance),
            autoStarts));

        return new QuestProximityWatcher(new TestWorld(new QuestMarkers("quest-1", StartMarker, EndMarker)));
    }

    Quest Quest1 => _quests.Find("quest-1")!;

    // ---- starting ----

    [Fact]
    public void StartsTheQuest_WhenThePlayerIsOnTheStartMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker);

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void StartsTheQuest_WhenThePlayerIsWithinTheStartDistance()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(15, 20));    // exactly 25 away

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void DoesNotStartTheQuest_WhenThePlayerIsBeyondTheStartDistance()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, 25.001));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void DoesNotStartTheQuest_WhenItDoesNotAutoStart()
    {
        QuestProximityWatcher watcher = CreateWatcher(autoStarts: false);

        watcher.Update(_quests, StartMarker);

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void RaisesStartedOnce_HoweverManyFramesThePlayerSpendsOnTheMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        int started = 0;
        _quests.QuestStarted += (_, _) => started++;

        for (int frame = 0; frame < 10; frame++)
        {
            watcher.Update(_quests, StartMarker);
        }

        Assert.Equal(1, started);
    }

    // ---- completing ----

    [Fact]
    public void CompletesTheQuest_WhenThePlayerReachesTheEndMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 960));    // within the 50 unit end distance

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void LeavesTheQuestInProgress_ShortOfTheEndMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 940));

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void DoesNotCompleteAQuestThatNeverStarted()
    {
        QuestProximityWatcher watcher = CreateWatcher(autoStarts: false);

        watcher.Update(_quests, EndMarker);

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void RaisesCompletedOnce_HoweverManyFramesThePlayerSpendsOnTheMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();
        int completed = 0;
        _quests.QuestCompleted += (_, _) => completed++;

        for (int frame = 0; frame < 10; frame++)
        {
            watcher.Update(_quests, EndMarker);
        }

        Assert.Equal(1, completed);
    }

    [Fact]
    public void DoesNotRestartACompletedQuest()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        watcher.Update(_quests, StartMarker);
        watcher.Update(_quests, EndMarker);

        watcher.Update(_quests, StartMarker);              // back where it began

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    // ---- travelling ----

    [Fact]
    public void CarriesTheQuestFromStartToFinish_AsThePlayerMovesForward()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        for (Position at = StartMarker; at.Y <= EndMarker.Y; at = at.Offset(0, 10))
        {
            watcher.Update(_quests, at);
        }

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void CompletesInOneUpdate_WhenTheStartAndEndMarkersCoincide()
    {
        // a quest whose objective is where it begins is finished on arrival
        QuestLog quests = new();
        quests.Register(new QuestDefinition(
            "quest-1",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 25)));
        QuestProximityWatcher watcher =
            new(new TestWorld(new QuestMarkers("quest-1", StartMarker, StartMarker)));

        watcher.Update(quests, StartMarker);

        Assert.Equal(QuestState.Completed, quests.Find("quest-1")!.State);
    }

    [Fact]
    public void IgnoresMarkersForAQuestThatIsNotRegistered()
    {
        // markers left behind for a quest this campaign no longer ships
        QuestProximityWatcher watcher =
            new(new TestWorld(new QuestMarkers("quest-gone", StartMarker, EndMarker)));

        watcher.Update(new QuestLog(), StartMarker);       // must not throw
    }
}
