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

    // ---- sweeping: the ground a frame covered, not the point it ended on ----

    [Fact]
    public void StartsTheQuest_WhenOneFrameCarriesThePlayerStraightPastTheStartMarker()
    {
        // neither end of the frame is inside the 25 unit trigger, so point sampling misses it
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -200), new Position(0, 200));

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
    public void FiresTheStartTrigger_AtAnyFrameLength(double unitsCovered)
    {
        // a stalled frame is the case this exists for: however far the frame carried the player,
        // a marker on the line they covered is one they passed
        QuestProximityWatcher watcher = CreateWatcher();
        int started = 0;
        _quests.QuestStarted += (_, _) => started++;

        watcher.Update(
            _quests,
            new Position(0, -unitsCovered / 2),
            new Position(0, unitsCovered / 2));

        Assert.Equal(1, started);
    }

    [Fact]
    public void DoesNotStartTheQuest_ForAJourneyThatPassesWideOfTheMarker()
    {
        // the sweep must not widen the trigger sideways
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(200, -500), new Position(200, 500));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void DoesNotStartTheQuest_ForAJourneyAimedAtTheMarkerThatStopsShort()
    {
        // a segment, not the line through it — ground the player has not covered yet
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -500), new Position(0, -200));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void StartsTheQuest_ForAJourneyPassingExactlyAtTheTriggerDistance()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(25, -500), new Position(25, 500));

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void DoesNotStartTheQuest_ForAJourneyPassingJustOutsideTheTriggerDistance()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(25.001, -500), new Position(25.001, 500));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void RaisesStartedOnce_HoweverManyFramesSweepTheSameMarker()
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

    [Fact]
    public void StartsAndCompletesInOneFrame_WhenTheFrameCoversTheWholeField()
    {
        // Correct rather than surprising: the quest was played, in a single very long frame,
        // rather than skipped. Start() is applied before the end trigger is checked, which is what
        // lets one frame legitimately carry a quest from NotStarted to Completed.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void AFrameFlownBackwardsAcrossTheField_AlsoStartsAndCompletesTheQuest()
    {
        // The accepted consequence of measuring a frame rather than ordering the moments inside
        // it: a frame is the smallest unit of time the game has, so within one frame there is no
        // "before". Honouring direction would mean a trigger that depends on which way the ship
        // was pointing, and QuestDefinition makes no claim about direction at all. It takes a
        // 1,000+ unit frame — a several second stall — to reach, and the alternative on such a
        // stall is a quest that silently does not complete, which is the worse of the two.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, 1100), new Position(0, -100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void ANonFiniteJourney_FiresNothingRatherThanThrowing()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker, new Position(double.NaN, double.NaN));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void TheSinglePositionUpdate_IsAJourneyThatWentNowhere()
    {
        // the overload the existing tests use is the standstill case of the sweep, not a second
        // rule kept alongside it
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker, StartMarker);

        Assert.Equal(QuestState.Active, Quest1.State);
    }
}
