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

    // ---- sweeping: a marker cannot be stepped over ----

    [Fact]
    public void StartsTheQuest_WhenOneFrameCarriesThePlayerStraightPastTheStartMarker()
    {
        // The reproduction. Neither end of the frame is inside the 25 unit trigger, so measuring
        // either point on its own finds nothing and the quest silently never begins — which is
        // asserted first, so this cannot pass for free if the sweep were ever undone.
        QuestProximityWatcher watcher = CreateWatcher();
        Position from = new(0, -200);
        Position to = new(0, 200);

        watcher.Update(_quests, from);
        watcher.Update(_quests, to);
        Assert.Equal(QuestState.NotStarted, Quest1.State);

        watcher.Update(_quests, from, to);

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void CompletesTheQuest_WhenOneFrameCarriesThePlayerStraightPastTheEndMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 800), new Position(0, 1200));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(200)]
    [InlineData(1_000)]
    [InlineData(50_000)]
    public void FiresAtAnyFrameLength(double lengthOfTheFrameInUnits)
    {
        // the point of the change: how long a frame is must stop deciding whether a trigger fires
        QuestProximityWatcher watcher = CreateWatcher();
        int started = 0;
        _quests.QuestStarted += (_, _) => started++;

        double before = StartMarker.Y - (lengthOfTheFrameInUnits / 2);
        watcher.Update(_quests, new Position(0, before), new Position(0, before + lengthOfTheFrameInUnits));

        Assert.Equal(1, started);
    }

    [Fact]
    public void StartsAndCompletesInOneFrame_WhenTheFrameSweepsPastBothMarkers()
    {
        // A stalled frame long enough to fly the whole field plays the quest rather than skipping
        // it. This is also the mirror of the backwards case below, so the ordering rule there is
        // doing work rather than simply refusing to finish a quest on the frame it began.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void DoesNotFire_ForAJourneyThatPassesWideOfTheMarkers()
    {
        // sweeping must not widen a trigger sideways, however much ground the frame covers
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(200, -5_000), new Position(200, 5_000));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void DoesNotFire_ForAJourneyThatStopsShortOfTheMarker()
    {
        // heading for the marker is not reaching it
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, 900), new Position(0, 200));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void RaisesStartedOnce_HoweverManyFramesSweepTheMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        int started = 0;
        _quests.QuestStarted += (_, _) => started++;

        for (int frame = 0; frame < 10; frame++)
        {
            watcher.Update(_quests, new Position(0, -200), new Position(0, 200));
        }

        Assert.Equal(1, started);
    }

    // ---- the two triggers are ordered along the journey ----

    [Fact]
    public void DoesNotCompleteTheQuest_WhenOneFrameFliesTheFieldBackwards()
    {
        // past the exit marker and on to the start marker, in one frame. The quest begins, because
        // the player really did arrive at its start — but it must not also finish, because the
        // ground that would finish it was covered before the ground that began it.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, 1_100), new Position(0, -100));

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void CompletesAQuestAlreadyUnderWay_WhicheverWayTheFrameIsFlown()
    {
        // the ordering rule constrains only a quest that started on this frame; one already active
        // was started before any of this ground was covered
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 1_100), new Position(0, -100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }
}
