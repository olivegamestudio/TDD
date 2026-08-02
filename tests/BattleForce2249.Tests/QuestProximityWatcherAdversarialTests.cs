using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's adversarial cover for the swept trigger (#8). These are the edges the definition of done
/// does not name: the ordering rule's failure modes, degenerate journeys, degenerate content, and
/// the boundary the sweep is allowed to reach but not cross.
/// </summary>
/// <remarks>
/// Written against the shipped behaviour rather than the implementation, so they stay meaningful
/// if the geometry is rewritten. Nothing here asserts which triggers an unmeasurable journey
/// fires — that is <see href="https://github.com/olivegamestudio/TDD/issues/76">#76</see>, and a
/// test pinning today's answer would have to be deleted when it lands.
/// </remarks>
public sealed class QuestProximityWatcherAdversarialTests
{
    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => Position.Origin;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    readonly QuestLog _quests = new();

    QuestProximityWatcher Watcher(
        Position start,
        Position end,
        double startDistance = 25,
        double endDistance = 50,
        string questId = "quest-1")
    {
        _quests.Register(new QuestDefinition(
            questId,
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, startDistance),
            new QuestTrigger(QuestTriggerKind.Proximity, endDistance),
            AutoStarts: true));

        return new QuestProximityWatcher(new TestWorld(new QuestMarkers(questId, start, end)));
    }

    QuestState StateOf(string questId = "quest-1") => _quests.Find(questId)!.State;

    // ---- the ordering rule: does it only ever delay a completion, or can it lose one? ----

    [Fact]
    public void TheOrderingRuleDelaysACompletion_ItNeverLocksOneOut()
    {
        // Content that puts the end marker *behind* the start marker along the line of travel, with
        // an end trigger generous enough to still cover where the frame finishes. A point sample at
        // the end of this frame would have started and completed the quest in one go; the ordering
        // rule refuses that, because the player was at the objective before they were at the
        // beginning. The refusal must cost a frame and no more — a quest that can never be finished
        // is a worse defect than the one the ordering rule exists to prevent.
        QuestProximityWatcher watcher = Watcher(
            start: new Position(0, 1000),
            end: new Position(0, 500),
            endDistance: 600);

        watcher.Update(_quests, new Position(0, 0), new Position(0, 1000));

        Assert.Equal(QuestState.Active, StateOf());

        // the very next frame, without the player moving an inch
        watcher.Update(_quests, new Position(0, 1000));

        Assert.Equal(QuestState.Completed, StateOf());
    }

    [Fact]
    public void AJourneyFlownBackwards_StartsTheQuestWithoutCompletingIt()
    {
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));

        watcher.Update(_quests, new Position(0, 1100), new Position(0, -100));

        Assert.Equal(QuestState.Active, StateOf());
    }

    [Fact]
    public void AQuestAlreadyUnderWay_IsCompletedByABackwardsJourney()
    {
        // The order matters only for the frame a quest begins on. Once the player is on the quest,
        // arriving at its objective finishes it whichever way round they flew through.
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));

        watcher.Update(_quests, new Position(0, 0));
        Assert.Equal(QuestState.Active, StateOf());

        watcher.Update(_quests, new Position(0, 1100), new Position(0, -100));

        Assert.Equal(QuestState.Completed, StateOf());
    }

    [Fact]
    public void MarkersThatCoincide_AreStartedAndCompletedByOneJourney()
    {
        // Both markers are reached at the same fraction, so the ordering comparison has to be
        // inclusive or a quest whose beginning is its objective could never be finished.
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 0));

        watcher.Update(_quests, new Position(0, -200), new Position(0, 200));

        Assert.Equal(QuestState.Completed, StateOf());
    }

    // ---- the boundary the sweep may reach but not cross ----

    [Fact]
    public void AJourneyPassingAtExactlyTheTriggerDistance_Fires_AndAHairFurtherDoesNot()
    {
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));

        watcher.Update(_quests, new Position(25, -500), new Position(25, 500));

        Assert.Equal(QuestState.Active, StateOf());

        QuestLog otherLog = new();
        otherLog.Register(new QuestDefinition(
            "quest-2",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true));

        watcher = new QuestProximityWatcher(
            new TestWorld(new QuestMarkers("quest-2", new Position(0, 0), new Position(0, 1000))));

        watcher.Update(otherLog, new Position(25.0000001, -500), new Position(25.0000001, 500));

        Assert.Equal(QuestState.NotStarted, otherLog.Find("quest-2")!.State);
    }

    [Fact]
    public void AJourneyThatStopsShortOfTheMarker_IsNotArrivingAtIt()
    {
        // Aimed straight at the marker, but the frame ended 200 units out. Measuring against the
        // infinite line rather than the segment would fire this.
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));

        watcher.Update(_quests, new Position(0, -500), new Position(0, -200));

        Assert.Equal(QuestState.NotStarted, StateOf());
    }

    [Fact]
    public void TheSweepFiresEverythingAPointSampleWouldHave()
    {
        // Sweeping is only ever allowed to bring a marker nearer. Anything the old point-sampled
        // watcher fired on must still fire, or this change traded one silently missed trigger for
        // another.
        Position marker = new(0, 0);

        foreach (Position to in new Position[]
        {
            new(0, 0), new(25, 0), new(0, 25), new(15, 20), new(-15, -20), new(24.9, 0), new(0, -25),
        })
        {
            QuestLog quests = new();
            quests.Register(new QuestDefinition(
                "q",
                "A title",
                new QuestTrigger(QuestTriggerKind.Proximity, 25),
                new QuestTrigger(QuestTriggerKind.Proximity, 50),
                AutoStarts: true));

            QuestProximityWatcher watcher = new(
                new TestWorld(new QuestMarkers("q", marker, new Position(0, 100000))));

            Assert.True(
                marker.DistanceTo(to) <= 25,
                $"the fixture is wrong: a point sample at {to} would not have fired");

            watcher.Update(quests, new Position(0, -100000), to);

            Assert.Equal(QuestState.Active, quests.Find("q")!.State);
        }
    }

    // ---- degenerate journeys ----

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    public void AJourneyEndingSomewhereThatIsNotAPlace_FiresNothingAndDoesNotThrow(double x, double y)
    {
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));

        watcher.Update(_quests, new Position(0, 0), new Position(x, y));

        Assert.Equal(QuestState.NotStarted, StateOf());
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void AJourneyBeginningSomewhereThatIsNotAPlace_FiresNothingAndDoesNotThrow(double x, double y)
    {
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));

        watcher.Update(_quests, new Position(x, y), new Position(0, 0));

        Assert.Equal(QuestState.NotStarted, StateOf());
    }

    [Fact]
    public void AJourneyLongEnoughToOverflowItsOwnLength_IsHandledWithoutThrowing()
    {
        // Squaring the length of a journey this long overflows a double. Which triggers it fires is
        // #76's question, deliberately not asserted here; that it does not take the frame down with
        // an exception is this issue's.
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));

        watcher.Update(_quests, new Position(0, -1e200), new Position(0, 1e200));
    }

    // ---- repetition and independence ----

    [Fact]
    public void SweepingTheSameGroundEveryFrame_RaisesQuestStartedOnce()
    {
        QuestProximityWatcher watcher = Watcher(new Position(0, 0), new Position(0, 1000));
        int started = 0;
        _quests.QuestStarted += (_, _) => started++;

        for (int frame = 0; frame < 10; frame++)
        {
            watcher.Update(_quests, new Position(0, -200), new Position(0, 200));
        }

        Assert.Equal(1, started);
    }

    [Fact]
    public void AZeroDistanceTrigger_FiresOnlyOnTheMarkerItself()
    {
        QuestProximityWatcher watcher = Watcher(
            new Position(0, 0),
            new Position(0, 1000),
            startDistance: 0);

        watcher.Update(_quests, new Position(1, -100), new Position(1, 100));
        Assert.Equal(QuestState.NotStarted, StateOf());

        watcher.Update(_quests, new Position(0, -100), new Position(0, 100));
        Assert.Equal(QuestState.Active, StateOf());
    }

    [Fact]
    public void OneJourneySweepingTwoQuests_DrivesThemIndependently()
    {
        _quests.Register(new QuestDefinition(
            "near",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true));
        _quests.Register(new QuestDefinition(
            "far",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true));

        QuestProximityWatcher watcher = new(new TestWorld(
            new QuestMarkers("near", new Position(0, 0), new Position(0, 500)),
            new QuestMarkers("far", new Position(9000, 0), new Position(9000, 500))));

        watcher.Update(_quests, new Position(0, -100), new Position(0, 600));

        Assert.Equal(QuestState.Completed, _quests.Find("near")!.State);
        Assert.Equal(QuestState.NotStarted, _quests.Find("far")!.State);
    }
}
