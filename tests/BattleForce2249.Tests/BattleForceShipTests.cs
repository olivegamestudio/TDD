using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the ship's handling as game content: the physics is the engine's and is covered there,
/// but the numbers decide whether the game is flyable, and whether the quests it ships with can
/// actually be reached.
/// </summary>
public sealed class BattleForceShipTests
{
    static readonly ShipHandling Handling = BattleForceShip.Handling;

    static QuestDefinition Quest1 =>
        new BattleForceCampaign().Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

    static QuestMarkers Quest1Markers =>
        new BattleForceWorld().QuestMarkers.Single(marker => marker.QuestId == BattleForceCampaign.Quest1Id);

    [Fact]
    public void IsSavedUnderAnIdentifier_NotUnderSomethingThePlayerReads()
    {
        // a save written in one language has to load in another, so the identifier is never
        // translated and never changes once a build has written it into a file
        Assert.Equal("disgraced", BattleForceShip.Id);
        Assert.Equal(BattleForceShip.Id, BattleForceShip.Ship.Id);
    }

    [Fact]
    public void KnowsTheGraphicThatStandsForIt()
    {
        // #5: ship1.png. The asset key is an identifier naming a file, not text, so it is the same
        // in every language — and it lives on the ship so nothing downstream has to hard-code it.
        Assert.Equal("ship1", BattleForceShip.AssetKey);
        Assert.Equal(BattleForceShip.AssetKey, BattleForceShip.Ship.AssetKey);
    }

    [Fact]
    public void TheShipCarriesTheHandlingTheGameIsTunedFor()
    {
        // built from Handling rather than from a second copy of the numbers, so the ship the
        // player owns and the ship the physics flies cannot become two ships that fly differently
        Assert.Same(Handling, BattleForceShip.Ship.Handling);
    }

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
        Assert.True(
            Handling.SecondsForAFullCircle < 2,
            $"the ship is too slow to turn: {Handling.SecondsForAFullCircle:0.###} seconds to come about");
    }

    [Fact]
    public void ComesAbout_InALittleUnderASecondAndAHalf()
    {
        // the feel the handling is documented as having, pinned so the number and the prose cannot
        // drift apart again — they had, along with the ceiling above, into three separate claims
        Assert.InRange(Handling.SecondsForAFullCircle, 1.25, 1.5);
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
