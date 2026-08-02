using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's adversarial cover for the swept watcher: the state transitions and marker layouts a frame
/// long enough to cover ground can reach, which point sampling could never produce.
/// </summary>
/// <remarks>
/// Sweeping widens what one frame can touch, so it also widens what one frame can get wrong. A
/// frame that used to see one marker can now see several, in an order nobody chose. These pin that
/// the quest state machine still gates every transition, that a sweep cannot drive a quest
/// backwards, and that quests do not interfere with each other.
/// </remarks>
public sealed class QuestProximityWatcherAdversarialTests
{
    static readonly Position StartMarker = new(0, 0);
    static readonly Position EndMarker = new(0, 1000);

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => StartMarker;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    readonly QuestLog _quests = new();

    QuestProximityWatcher CreateWatcher(
        bool autoStarts = true,
        double startDistance = 25,
        double endDistance = 50)
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

    // ---- the state machine still gates every transition ----

    /// <summary>
    /// A frame that sweeps the end marker without ever having started the quest must not complete
    /// it. Sweeping makes this reachable: a single frame can now cross the far end of a field the
    /// player never entered.
    /// </summary>
    [Fact]
    public void AFrameSweepingOnlyTheEndMarker_DoesNotCompleteAQuestThatNeverStarted()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        // straight across the end marker, nowhere near the start
        watcher.Update(_quests, new Position(-400, 1000), new Position(400, 1000));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    /// <summary>
    /// A completed quest is not restarted by a later frame sweeping back across its start marker,
    /// which is the ordinary thing a player does when they fly home.
    /// </summary>
    [Fact]
    public void ACompletedQuest_IsNotRestartedByASweepBackAcrossItsStartMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));
        Assert.Equal(QuestState.Completed, Quest1.State);

        watcher.Update(_quests, new Position(0, 1100), new Position(0, -100));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    /// <summary>
    /// A quest that does not auto-start is not started by a sweep either, however much ground the
    /// frame covers.
    /// </summary>
    [Fact]
    public void AQuestThatDoesNotAutoStart_IsNotStartedByASweepAcrossTheWholeField()
    {
        QuestProximityWatcher watcher = CreateWatcher(autoStarts: false);

        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    /// <summary>
    /// The completion event is raised once, however many frames go on sweeping the end marker.
    /// </summary>
    /// <remarks>
    /// The started-once case is already covered; this is its twin, and it is the one a player
    /// reaches by loitering at the end of a field.
    /// </remarks>
    [Fact]
    public void RaisesCompletedOnce_HoweverManyFramesSweepTheEndMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher();
        int completions = 0;
        watcher.Update(_quests, StartMarker);
        Quest1.Completed += (_, _) => completions++;

        for (int frame = 0; frame < 5; frame++)
        {
            watcher.Update(_quests, new Position(0, 900), new Position(0, 1100));
        }

        Assert.Equal(1, completions);
    }

    // ---- degenerate content ----

    /// <summary>
    /// A trigger authored at zero distance fires only for a journey that passes exactly through
    /// its marker, and is not widened by the sweep.
    /// </summary>
    /// <remarks>
    /// Zero is a legal distance — <see cref="QuestTrigger"/> refuses only negatives — so it is
    /// content the model accepts. A sweep that rounded the projection outward would turn it into a
    /// trigger that fires for a near miss.
    /// </remarks>
    [Fact]
    public void AZeroDistanceTrigger_FiresOnlyForAJourneyStraightThroughItsMarker()
    {
        QuestProximityWatcher watcher = CreateWatcher(startDistance: 0);

        watcher.Update(_quests, new Position(-100, 0.5), new Position(100, 0.5));
        Assert.Equal(QuestState.NotStarted, Quest1.State);

        watcher.Update(_quests, new Position(-100, 0), new Position(100, 0));
        Assert.Equal(QuestState.Active, Quest1.State);
    }

    /// <summary>
    /// A quest whose start and end markers stand at the same place is played, not skipped, by the
    /// frame that reaches it.
    /// </summary>
    /// <remarks>
    /// Degenerate content rather than anything shipping, but it is the smallest case of the
    /// start-and-complete-in-one-frame rule the watcher documents, and it must not deadlock at
    /// <see cref="QuestState.Active"/>.
    /// </remarks>
    [Fact]
    public void AQuestWhoseMarkersCoincide_StartsAndCompletesRatherThanSticking()
    {
        _quests.Register(new QuestDefinition(
            "quest-1",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            AutoStarts: true));
        QuestProximityWatcher watcher = new(
            new TestWorld(new QuestMarkers("quest-1", StartMarker, StartMarker)));

        watcher.Update(_quests, new Position(-500, 0), new Position(500, 0));

        Assert.Equal(QuestState.Completed, Quest1.State);
    }

    // ---- quests do not interfere ----

    /// <summary>
    /// One frame crossing two quests' fields drives both of them, and a frame crossing only one
    /// leaves the other alone.
    /// </summary>
    /// <remarks>
    /// The watcher loops over every marker set, so a sweep long enough to cross two fields now
    /// touches both in a single frame — a combination point sampling could not produce.
    /// </remarks>
    [Fact]
    public void OneFrameCrossingTwoFields_DrivesBothQuestsIndependently()
    {
        _quests.Register(new QuestDefinition(
            "quest-1",
            "The first",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true));
        _quests.Register(new QuestDefinition(
            "quest-2",
            "The second",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true));
        QuestProximityWatcher watcher = new(new TestWorld(
            new QuestMarkers("quest-1", new Position(0, 0), new Position(0, 1000)),
            new QuestMarkers("quest-2", new Position(5000, 0), new Position(5000, 1000))));

        // up quest 1's field only — quest 2 is 5,000 units to starboard
        watcher.Update(_quests, new Position(0, -100), new Position(0, 1100));

        Assert.Equal(QuestState.Completed, _quests.Find("quest-1")!.State);
        Assert.Equal(QuestState.NotStarted, _quests.Find("quest-2")!.State);

        watcher.Update(_quests, new Position(5000, -100), new Position(5000, 1100));

        Assert.Equal(QuestState.Completed, _quests.Find("quest-2")!.State);
    }

    // ---- the sweep does not reach further than it should ----

    /// <summary>
    /// A frame that flies parallel to a field, just outside both triggers for its whole length,
    /// fires nothing — the case a sweep is most likely to get wrong, because it is near the
    /// markers for the entire journey rather than at one moment of it.
    /// </summary>
    [Fact]
    public void AFrameFlownAlongsideTheField_JustOutsideTheTriggers_FiresNothing()
    {
        QuestProximityWatcher watcher = CreateWatcher();

        // 25.001 to starboard of the line through both markers, the whole way up it
        watcher.Update(_quests, new Position(25.001, -500), new Position(25.001, 1500));

        Assert.Equal(QuestState.NotStarted, Quest1.State);
    }

    /// <summary>
    /// A frame whose two ends straddle a marker at exactly the trigger distance on each side still
    /// fires, because the ground between them passes closer than either end does.
    /// </summary>
    /// <remarks>
    /// This is the sweep's whole purpose stated at the boundary: point sampling either end gives
    /// exactly the trigger distance and fires by luck of the comparison, while the journey between
    /// them passes at zero.
    /// </remarks>
    [Fact]
    public void AFrameStraddlingAMarker_FiresFromTheGroundBetweenItsEnds()
    {
        QuestProximityWatcher watcher = CreateWatcher(startDistance: 25);

        watcher.Update(_quests, new Position(-25, 0), new Position(25, 0));

        Assert.Equal(QuestState.Active, Quest1.State);
    }
}
