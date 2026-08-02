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

    // ---- sweeping the ground covered, rather than sampling where the frame ended ----

    [Fact]
    public void StartsTheQuest_WhenOneFrameCarriesThePlayerStraightPastTheStartMarker()
    {
        // The reproduction. Neither end of this frame is inside the 25 unit start trigger — the
        // player begins 200 short of the marker and finishes 200 past it — so sampling either end
        // fires nothing and the ship flies through a trigger it passed dead centre of.
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
    public void StartsTheQuest_AtAnyFrameLength(double unitsCoveredThisFrame)
    {
        // a stalled frame, a faster ship, or a tighter trigger authored for a small object all
        // make the frame longer relative to the marker; none of them may skip it
        QuestProximityWatcher watcher = CreateWatcher();
        int started = 0;
        _quests.QuestStarted += (_, _) => started++;

        watcher.Update(
            _quests,
            new Position(0, -unitsCoveredThisFrame / 2),
            new Position(0, unitsCoveredThisFrame / 2));

        Assert.Equal(1, started);
    }

    [Fact]
    public void StartsAndCompletesTheQuest_WhenOneFrameCoversTheWholeField()
    {
        // A frame long enough to fly the whole of quest 1 plays it rather than skipping it. New,
        // and correct: the player covered every unit of ground the quest asks for.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    // ---- the sweep must not widen a trigger sideways ----

    [Fact]
    public void DoesNotStartTheQuest_ForAJourneyThatPassesWideOfTheMarker()
    {
        // the length of the field, but 200 units to the side of it
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(200, -500), new Position(200, 1500));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void DoesNotStartTheQuest_ForAJourneyThatStopsShortOfTheMarker()
    {
        // aimed straight at the marker, but the frame ended 200 units before reaching it: a
        // segment, not the infinite line through it
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, -500), new Position(0, -200));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    [Fact]
    public void StartsTheQuest_ForAJourneyPassingAtExactlyTheTriggerDistance()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(25, -500), new Position(25, 500));

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void DoesNotStartTheQuest_ForAJourneyPassingJustOutsideTheTriggerDistance()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(25.0001, -500), new Position(25.0001, 500));

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

    // ---- the order the ground was covered in ----

    [Fact]
    public void AJourneyFlownBackwards_StartsTheQuestWithoutCompletingIt()
    {
        // The player passed the exit before they passed the entrance, so they cannot have finished
        // the quest on this frame — they reached its objective before they had begun it. Sweeping
        // measures the ground covered; it does not lose the order it was covered in.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, new Position(0, 1100), new Position(0, -100));

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void AJourneyFlownBackwards_CompletesAQuestThatWasAlreadyUnderway()
    {
        // The ordering rule only governs a quest that began on this very frame. A player who has
        // been on the quest since an earlier frame reaches the objective by arriving at it, and
        // which way round they flew through it is not the quest's business.
        QuestProximityWatcher watcher = CreateWatcher();
        Quest1.Start();

        watcher.Update(_quests, new Position(0, 1100), new Position(0, -100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    [Fact]
    public void AQuestStartedBackwards_CompletesOnTheNextFrameFlownForward()
    {
        // and it is a delay, not a lockout: the quest that began on the way back finishes the
        // moment the player flies to its exit
        QuestProximityWatcher watcher = CreateWatcher();
        watcher.Update(_quests, new Position(0, 1100), new Position(0, -100));

        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    // ---- a standing frame is a swept frame whose ends coincide ----

    [Fact]
    public void SweepingAJourneyThatWentNowhere_IsTheSameAsSamplingThatPoint()
    {
        // why the single-position overload can delegate rather than live alongside a second rule
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker, StartMarker);

        Assert.Equal(QuestState.Active, Quest1.State);
    }

    [Fact]
    public void FiresNothing_ForAJourneyWithAnEndThatIsNotANumber()
    {
        // Pinned rather than accepted quietly: a non-finite position measures NaN against every
        // marker and every comparison against NaN is false, so the sweep fires nothing at all
        // rather than firing wrongly. Keeping positions finite is the save boundary's rule.
        QuestProximityWatcher watcher = CreateWatcher();

        watcher.Update(_quests, StartMarker, new Position(double.NaN, double.NaN));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }
}
