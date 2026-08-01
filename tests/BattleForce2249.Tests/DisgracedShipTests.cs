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
    public void TheStartingShipFliesOnTheseNumbers()
    {
        // the hull the player is awarded and the numbers it is flown on are one thing, so a
        // change to the tuning cannot leave the awarded ship handling like the old one
        Assert.Same(Handling, DisgracedShip.Starting.Handling);
    }

    [Fact]
    public void TheStartingShipIsDrawnByTheShipGraphic()
    {
        // ship1.png in the content build; #16 draws it, this only says which asset stands for
        // the ship so nothing downstream has to guess
        Assert.Equal("ship1", DisgracedShip.Starting.AssetKey);
    }

    [Fact]
    public void TheStartingShipIsSavedUnderAStableId()
    {
        // saves record this, so changing it orphans every save already written
        Assert.Equal("disgraced", DisgracedShip.Starting.Id);
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
    public void AtFullSpeed_AFrameStaysWellInsideQuest1sTriggers()
    {
        // pillar 1: a trigger a fast ship flies straight through is a bug, not a tuning detail.
        // The watcher samples the player once a frame, so the ground covered between two frames
        // has to stay comfortably inside the trigger it is meant to fire.
        double perFrameAt30Hz = Handling.MaximumSpeed / 30;

        Assert.True(
            perFrameAt30Hz < Quest1.Start.Distance,
            $"{perFrameAt30Hz} units a frame can step over a {Quest1.Start.Distance} unit start trigger");
        Assert.True(
            perFrameAt30Hz < Quest1.End.Distance,
            $"{perFrameAt30Hz} units a frame can step over a {Quest1.End.Distance} unit end trigger");
    }
}
