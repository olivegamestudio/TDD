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

        // a test world that names no places: whatever enters it enters where it starts
        public Position Introduce(Ship ship, string location) => PlayerStart;

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

    // ---- sweeping the frame, rather than sampling the end of it ----

    [Fact]
    public void CompletesTheQuest_WhenOneFrameSteppedStraightOverTheEndMarker()
    {
        // the case pillar 1 calls a bug: neither end of this frame is within the 50 unit end
        // trigger, and the ship flew through the middle of it
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 900), new Position(0, 1100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void StartsTheQuest_WhenOneFrameSteppedStraightOverTheStartMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -200), new Position(0, 200));

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void SweepsAtAnyFrameLength()
    {
        // the trigger must not depend on how long the frame was. A frame carrying the ship the
        // whole 1000 units in one step passes both markers, so it both starts and finishes.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void DoesNotFire_WhenTheFrameOnlyPassedWideOfTheMarker()
    {
        // the sweep measures a line, not a corridor: a ship that crossed the marker's latitude
        // 400 units off to one side never came near it
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(400, -200), new Position(400, 200));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void DoesNotFire_WhenTheFrameStoppedShortOfTheMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 100), new Position(0, 940));

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void SweepsTheFrameTheShipActuallyFlew_NotTheGroundBehindIt()
    {
        // only this frame's journey is measured, not everything flown since the game began: a
        // frame between 400 and 600 never came near the marker back at the origin
        QuestProximityWatcher watcher = CreateWatcher();
        watcher.Update(_quests, new Position(0, 400), new Position(0, 600));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void RaisesStartedOnce_WhenSuccessiveFramesKeepSweepingTheMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        int started = 0;
        _quests.QuestStarted += (_, _) => started++;

        for (int frame = 0; frame < 10; frame++)
        {
            watcher.Update(_quests, new Position(0, -50), new Position(0, 50));
        }

        Assert.Equal(1, started);
    }

    [Fact]
    public void CompletesAQuestStartedByTheSameFrame()
    {
        // one frame long enough to sweep both markers is a frame that flew through the whole
        // quest. Start is applied before end, exactly as it is for a frame that lands on one.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker, EndMarker);

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void FiresOnTheSweep_EvenWhenTheFrameWasFlownBackwards()
    {
        // a proximity trigger is a disc round a marker and has no sense of direction — the
        // existing point measure completes a quest approached from beyond the exit, and the
        // sweep does not quietly acquire an opinion the point measure never had
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 1100), new Position(0, 900));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void MeasuresAJourneyThatWentNowhere_AsThePointItStoodAt()
    {
        // the three argument call with both ends the same is the two argument call
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker, StartMarker);

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void FiresNothing_WhenTheFrameCannotBeMeasured()
    {
        // a journey to nowhere fires nothing rather than firing everything: the distance is not a
        // number, and every comparison against it is false
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker, new Position(double.NaN, double.NaN));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }
}
