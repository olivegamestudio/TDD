using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the yard the game supplies its ships through: which ship a new game awards, and whether
/// an identifier read back out of a save finds the ship it named.
/// </summary>
/// <remarks>
/// Everything here goes through <see cref="IShipYard"/> rather than the concrete yard, because the
/// session only ever sees the interface — a test that reaches past it can pass while the seam the
/// game actually uses is broken.
/// </remarks>
public sealed class BattleForceShipYardTests
{
    readonly IShipYard _yard = new BattleForceShipYard();

    [Fact]
    public void StocksTheDisgracedsShip()
    {
        Assert.Contains(_yard.Ships, ship => ship.Id == BattleForceShip.Id);
    }

    [Fact]
    public void AwardsTheDisgracedsShipToANewGame()
    {
        // the one hull the game has, which is also the one the fiction starts the player in
        Assert.Equal(BattleForceShip.Id, _yard.StartingShip.Id);
    }

    [Fact]
    public void TheStartingShipCarriesTheGraphicTheIssueAskedFor()
    {
        // #5: ship1.png is what stands for the player's ship on screen. The model names the asset
        // so whatever draws it has nothing to decide and no table to keep in step.
        Assert.Equal("ship1", _yard.StartingShip.AssetKey);
    }

    [Fact]
    public void TheStartingShipFliesTheHandlingTheGameIsTunedFor()
    {
        // the same instance, not an equal copy: two sets of numbers for one hull is how the ship
        // the player owns and the ship the physics flies drift apart
        Assert.Same(BattleForceShip.Handling, _yard.StartingShip.Handling);
    }

    [Fact]
    public void FindsTheShipItAwards()
    {
        // the round trip the save depends on: the identifier captured for the awarded ship has to
        // be one that resolves again, or every single reload downgrades the player
        Assert.Same(_yard.StartingShip, _yard.Find(_yard.StartingShip.Id));
    }

    [Fact]
    public void FindsEveryShipItStocks()
    {
        // so adding a hull to the list is all it takes for a save to be able to name it
        Assert.All(_yard.Ships, ship => Assert.Same(ship, _yard.Find(ship.Id)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("nonesuch")]
    [InlineData("Disgraced")]
    [InlineData("DISGRACED")]
    [InlineData("disgraced ")]
    [InlineData(" disgraced")]
    public void DoesNotStockAnythingItWasNotGiven(string shipId)
    {
        // matching is exact and ordinal. A near miss out of a hand-edited save is a miss: case
        // folding or trimming here would put a hull in the cockpit that nothing ever awarded.
        Assert.Null(_yard.Find(shipId));
    }

    [Fact]
    public void MatchesTheIdentifierTheSameWayInEveryLanguage()
    {
        // a save written in one language has to load in another, and the dotted I is where a
        // careless case fold shows up first
        foreach (string culture in new[] { "tr", "de", "ja" })
        {
            using CultureScope _ = new(culture);

            Assert.Same(_yard.StartingShip, _yard.Find(BattleForceShip.Id));
            Assert.Null(_yard.Find(BattleForceShip.Id.ToUpperInvariant()));
        }
    }
}
