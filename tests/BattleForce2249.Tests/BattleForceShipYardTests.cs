using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the ships the game ships with. The yard is what stands between a save game's ship
/// identifier and a player with something to fly, so its failure mode is a loaded game where the
/// player is flying nothing.
/// </summary>
public sealed class BattleForceShipYardTests
{
    readonly BattleForceShipYard _yard = new();

    [Fact]
    public void ANewGameAwardsTheDisgracedTheirOwnShip()
    {
        Assert.Same(DisgracedShip.Ship, _yard.StartingShip);
    }

    [Fact]
    public void TheStartingShipIsOneOfTheShipsTheGameHas()
    {
        // otherwise a new game awards a ship that its own save could never be loaded back into
        Assert.Contains(_yard.StartingShip, BattleForceShipYard.Ships);
    }

    [Fact]
    public void Find_ReturnsTheShipASaveNames()
    {
        Assert.Same(DisgracedShip.Ship, _yard.Find(DisgracedShip.Id));
    }

    [Fact]
    public void Find_ReturnsNull_ForAShipThisBuildDoesNotHave()
    {
        // a save from a build that shipped a hull this one does not; the caller decides what to do
        Assert.Null(_yard.Find("a-ship-from-another-build"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("DISGRACED")]
    public void Find_ReturnsNull_ForAnIdentifierThatIsNotOne(string shipId)
    {
        // identifiers match exactly: a save written before ships were recorded has no id at all,
        // and matching loosely would let two ships collide as the roster grows
        Assert.Null(_yard.Find(shipId));
    }

    [Fact]
    public void EveryShipHasADistinctIdentifier()
    {
        // two ships sharing an id would make Find return whichever came first, silently
        Assert.Equal(
            BattleForceShipYard.Ships.Count,
            BattleForceShipYard.Ships.Select(ship => ship.Id).Distinct().Count());
    }

    [Fact]
    public void EveryShipCanBeFoundByItsOwnIdentifier()
    {
        foreach (Ship ship in BattleForceShipYard.Ships)
        {
            Assert.Same(ship, _yard.Find(ship.Id));
        }
    }

    [Fact]
    public void EveryShipIsFlyableAndHasSomethingToDrawIt()
    {
        // a ship with no asset key is a ship the player cannot see
        Assert.All(BattleForceShipYard.Ships, ship =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ship.Id));
            Assert.False(string.IsNullOrWhiteSpace(ship.AssetKey));
            Assert.True(ship.Handling.Acceleration > 0);
        });
    }
}
