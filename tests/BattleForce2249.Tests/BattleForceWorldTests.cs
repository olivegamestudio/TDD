using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers where things stand in the world, and the one fact that spans the world and the campaign:
/// that quest 1's start marker is close enough to the spawn point to fire its trigger.
/// </summary>
public sealed class BattleForceWorldTests
{
    readonly BattleForceWorld _world = new();

    readonly BattleForceCampaign _campaign = new();

    QuestMarkers Quest1Markers =>
        _world.QuestMarkers.Single(markers => markers.QuestId == BattleForceCampaign.Quest1Id);

    QuestDefinition Quest1 =>
        _campaign.Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

    [Fact]
    public void PlacesMarkersForQuest1()
    {
        Assert.Contains(_world.QuestMarkers, markers => markers.QuestId == BattleForceCampaign.Quest1Id);
    }

    [Fact]
    public void Quest1_StartsWithinReachOfWhereANewGamePutsThePlayer()
    {
        // this is what makes quest 1 begin on a new game launch; if the spawn point drifted out of
        // range the quest would silently never start
        Assert.True(_world.PlayerStart.DistanceTo(Quest1Markers.Start) <= Quest1.Start.Distance);
    }

    [Fact]
    public void Quest1_EndsForwardOfItsStart()
    {
        Assert.True(Quest1Markers.End.Y > Quest1Markers.Start.Y);
    }

    [Fact]
    public void Quest1_EndsOutOfReachOfTheSpawnPoint()
    {
        // otherwise the quest would complete the instant it started, and there would be no game
        Assert.True(_world.PlayerStart.DistanceTo(Quest1Markers.End) > Quest1.End.Distance);
    }

    [Fact]
    public void EveryMarkerStandsWellInsideTheWorldsEdge()
    {
        // the edge is what a save is refused for being outside, so a marker anywhere near it would
        // mean the campaign itself was authored somewhere the game will not load a save from
        Assert.All(
            _world.QuestMarkers,
            markers => Assert.All(
                new[] { markers.Start, markers.End },
                marker =>
                {
                    Assert.True(Math.Abs(marker.X) < BattleForceWorld.Extent / 1000);
                    Assert.True(Math.Abs(marker.Y) < BattleForceWorld.Extent / 1000);
                }));
    }

    [Fact]
    public void ANewGameStartsInsideTheWorld()
    {
        Assert.True(Math.Abs(_world.PlayerStart.X) <= BattleForceWorld.Extent);
        Assert.True(Math.Abs(_world.PlayerStart.Y) <= BattleForceWorld.Extent);
    }

    [Fact]
    public void TheWorldsEdge_IsSomewhereTheShipCanStillMoveFrom()
    {
        // this is the whole difference between the edge and the astronomical coordinates it was
        // drawn to refuse. A save is accepted for being inside the edge, and "accepted" has to mean
        // the player can fly out of it: at 1e300 a ten unit step returned the position it started
        // from, so the ship sat still while the engine reported motion. Raising Extent towards the
        // scale where that begins would pass every other test on this class and fail here.
        foreach (double edge in new[] { BattleForceWorld.Extent, -BattleForceWorld.Extent })
        {
            Position corner = new(edge, edge);

            Assert.NotEqual(corner, corner.Offset(0, 10));
            Assert.NotEqual(corner, corner.Offset(10, 0));
        }
    }

    [Fact]
    public void FromTheWorldsEdge_FlyingTowardsQuest1_ActuallyClosesTheDistance()
    {
        // the symptom defect 6 was reported for was a quest that can never start: every marker an
        // unreachable distance away, and flying making no difference to it. Measured from the
        // furthest place a save is still accepted, the distance has to be a number and it has to
        // come down — otherwise the guard is admitting the same brick it was written to refuse.
        Position edge = new(BattleForceWorld.Extent, BattleForceWorld.Extent);
        Position target = Quest1Markers.Start;

        double before = edge.DistanceTo(target);
        Assert.True(double.IsFinite(before), $"distance from the world's edge was {before}");

        // one frame of ordinary flight, straight at the marker
        double toward = 10 / before;
        Position after = edge.Offset(
            (target.X - edge.X) * toward,
            (target.Y - edge.Y) * toward);

        Assert.True(
            after.DistanceTo(target) < before,
            $"flying from the edge did not close the distance: {before} -> {after.DistanceTo(target)}");
    }

    [Fact]
    public void EveryQuestInTheCampaignHasMarkers()
    {
        Assert.All(
            _campaign.Quests,
            quest => Assert.Contains(_world.QuestMarkers, markers => markers.QuestId == quest.Id));
    }
}
