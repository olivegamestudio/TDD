using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the ship's handling as game content: the physics is the engine's and is covered there,
/// but the numbers decide whether the game is flyable, and whether the quests it ships with can
/// actually be reached.
/// </summary>
public sealed class DisgracedShipTests
{
    static readonly ShipHandling Handling = DisgracedShip.Handling;

    static QuestDefinition Quest1 =>
        new BattleForceCampaign().Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

    static QuestMarkers Quest1Markers =>
        new BattleForceWorld().QuestMarkers.Single(marker => marker.QuestId == BattleForceCampaign.Quest1Id);

    [Fact]
    public void CanBeFlown()
    {
        // the same rules ShipMovement enforces, checked against the shipping numbers so a bad
        // edit to the content fails here rather than the first time the game is launched
        Assert.True(Handling.Acceleration > 0);
        Assert.True(Handling.Drag > 0);
        Assert.True(Handling.TurnRate > 0);
    }

    [Fact]
    public void HasATopSpeed()
    {
        Assert.Equal(200, Handling.MaximumSpeed, 6);
    }

    [Fact]
    public void TurnsRightRoundInUnderTwoSeconds()
    {
        // a ship that takes longer than this to come about makes every correction an argument
        Assert.True(Math.Tau / Handling.TurnRate < 2, "the ship is too slow to turn");
    }

    [Fact]
    public void ReachesQuest1sExitMarker_AtFullThrust_InAPlayableTime()
    {
        Player player = new();
        ShipMovement ship = new(Handling);
        Position exit = Quest1Markers.End;

        int frames = 0;
        while (player.Position.DistanceTo(exit) > Quest1.End.Distance)
        {
            Assert.True(frames < 60 * 30, "the ship never got clear of the debris field");
            ship.Update(player, new ShipControls(thrust: 1, turn: 0), TimeSpan.FromMilliseconds(20));
            frames++;
        }

        // a run measured in seconds: long enough to be a flight, short enough to be an opening
        double seconds = frames * 0.02;
        Assert.InRange(seconds, 3, 15);
    }

    [Fact]
    public void AtFullSpeed_AFrameLandsInsideQuest1sTriggersRatherThanMerelyCrossingThem()
    {
        // no longer what stands between the game and pillar 1 — the watcher sweeps the journey now,
        // so a marker cannot be stepped over whatever the numbers are. Kept because a trigger the
        // ship lands inside is still better content than one it only ever flies through: a player
        // who eases off near the marker, or stops on it, should be inside it and not beside it.
        double perFrameAt30Hz = Handling.MaximumSpeed / 30;

        Assert.True(
            perFrameAt30Hz < Quest1.Start.Distance,
            $"{perFrameAt30Hz} units a frame overshoots a {Quest1.Start.Distance} unit start trigger");
        Assert.True(
            perFrameAt30Hz < Quest1.End.Distance,
            $"{perFrameAt30Hz} units a frame overshoots a {Quest1.End.Distance} unit end trigger");
    }

    [Fact]
    public void AFrameLongEnoughToCrossTheWholeDebrisField_StillFiresQuest1sTriggers()
    {
        // pillar 1 at the shipping numbers: the ship cannot outrun quest 1's markers however badly
        // a frame stalls. A single frame carrying the player from before the start marker to well
        // past the exit fires both, where sampling the end point alone fires neither.
        QuestProximityWatcher watcher = new(new BattleForceWorld());
        QuestLog quests = new();
        quests.Register(Quest1);

        Position before = new(0, -500);
        Position after = new(0, 5000);

        // neither end of the journey is inside either trigger, so nothing here fires by luck
        Assert.True(before.DistanceTo(Quest1Markers.Start) > Quest1.Start.Distance);
        Assert.True(after.DistanceTo(Quest1Markers.End) > Quest1.End.Distance);

        watcher.Update(quests, before, after);

        Assert.Equal(QuestState.Completed, quests.Find(BattleForceCampaign.Quest1Id)!.State);
    }
}
