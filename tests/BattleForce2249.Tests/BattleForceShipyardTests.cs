using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the shipyard as the catalogue a save is resolved against: what the game ships, what a
/// new player is given, and what happens to a save naming a hull this build has dropped.
/// </summary>
public sealed class BattleForceShipyardTests
{
    readonly BattleForceShipyard _shipyard = new();

    [Fact]
    public void StartsThePlayerInTheDisgracedShip()
    {
        Assert.Same(DisgracedShip.Starting, _shipyard.Starting);
    }

    [Fact]
    public void TheStartingShipIsOneOfTheShipsItLists()
    {
        // a starting ship missing from the catalogue would be awarded on a new game and then
        // fail to resolve the first time that game was reloaded
        Assert.Contains(DisgracedShip.Starting, _shipyard.Ships);
    }

    [Fact]
    public void EveryShipHasADistinctId()
    {
        // ids are what saves record, so two hulls sharing one would make a save ambiguous
        Assert.Equal(
            _shipyard.Ships.Count,
            _shipyard.Ships.Select(ship => ship.Id).Distinct().Count());
    }

    [Fact]
    public void EveryShipCanBeFoundByItsId()
    {
        Assert.All(_shipyard.Ships, ship => Assert.Same(ship, _shipyard.Find(ship.Id)));
    }

    [Fact]
    public void Find_ReturnsNull_ForAShipThisBuildNoLongerHas()
    {
        Assert.Null(_shipyard.Find("a-hull-from-a-later-episode"));
    }

    [Fact]
    public void Find_ReturnsNull_ForNoShipAtAll()
    {
        // the blank id a save written before ships were recorded carries
        Assert.Null(_shipyard.Find(""));
    }
}
