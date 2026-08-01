using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the game's ships as content: which ship a new game awards, and that a ship named in a
/// save can be found again. Losing either would only show up as a save that loaded into the wrong
/// ship, which nothing else in the game would complain about.
/// </summary>
public sealed class BattleForceShipyardTests
{
    readonly BattleForceShipyard _shipyard = new();

    [Fact]
    public void ANewGameAwardsTheDisgracedShip()
    {
        Assert.Equal(DisgracedShip.Ship, _shipyard.StartingShip);
    }

    [Fact]
    public void TheStartingShipIsOneOfTheShipsTheGameHas()
    {
        // a starting ship missing from the yard could not be found again when the save was read
        Assert.Contains(_shipyard.StartingShip, _shipyard.Ships);
    }

    [Fact]
    public void EveryShipCanBeFoundByTheIdentifierItIsSavedUnder()
    {
        Assert.All(_shipyard.Ships, ship => Assert.Equal(ship, _shipyard.Find(ship.Id)));
    }

    [Fact]
    public void EveryShipHasADistinctIdentifier()
    {
        // two ships sharing an id would mean a save could not say which one the player has
        Assert.Equal(
            _shipyard.Ships.Count,
            _shipyard.Ships.Select(ship => ship.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryShipHasAGraphicAndFlyableHandling()
    {
        Assert.All(_shipyard.Ships, ship =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ship.Id));
            Assert.False(string.IsNullOrWhiteSpace(ship.AssetKey));
            Assert.True(ship.Handling.Acceleration > 0);
            Assert.True(ship.Handling.Drag > 0);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a-ship-this-build-does-not-have")]
    public void Find_ReportsNothing_ForAShipTheGameDoesNotHave(string? id)
    {
        // reported rather than thrown: the caller decides what to do with an unreadable ship, and
        // what it does is fall back rather than throw the save away
        Assert.Null(_shipyard.Find(id));
    }

    [Fact]
    public void TheStartingShipFliesTheHandlingTheGameIsTunedOn()
    {
        // one set of numbers: the ship the player is awarded flies the numbers DisgracedShipTests
        // holds the line on, rather than a second copy that could drift from them
        Assert.Same(DisgracedShip.Handling, _shipyard.StartingShip.Handling);
    }
}
