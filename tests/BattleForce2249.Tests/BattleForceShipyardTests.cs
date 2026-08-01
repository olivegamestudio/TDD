using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the shipyard as game content. The starting ship is what a new game hands the player, and
/// its id is what every save from this build will name — a careless edit to either is a save
/// compatibility break rather than a cosmetic one.
/// </summary>
public sealed class BattleForceShipyardTests
{
    readonly BattleForceShipyard _shipyard = new();

    [Fact]
    public void ANewGameAwardsTheDisgracedsShip()
    {
        Assert.Same(DisgracedShip.Model, _shipyard.StartingShip);
    }

    [Fact]
    public void TheStartingShipIsOneOfTheShipsItStocks()
    {
        // otherwise a save written on a new game could not be read back
        Assert.Contains(_shipyard.StartingShip, _shipyard.Ships);
    }

    [Fact]
    public void EveryShipCarriesAnIdAndAGraphic()
    {
        Assert.NotEmpty(_shipyard.Ships);
        Assert.All(_shipyard.Ships, ship =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ship.Id), "a ship with no id cannot be saved");
            Assert.False(
                string.IsNullOrWhiteSpace(ship.AssetKey),
                $"'{ship.Id}' has no graphic, so nothing can draw it");
        });
    }

    [Fact]
    public void EveryShipHasADistinctId()
    {
        // two ships sharing an id would make a save ambiguous about which one it meant
        Assert.Equal(
            _shipyard.Ships.Count,
            _shipyard.Ships.Select(ship => ship.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryShipCanBeFlown()
    {
        // the same rules ShipMovement enforces, so bad content fails here rather than on launch
        Assert.All(_shipyard.Ships, ship => Assert.NotNull(new ShipMovement(ship.Handling)));
    }

    [Fact]
    public void Find_ResolvesAShipBackFromTheIdASaveRecorded()
    {
        Assert.Same(DisgracedShip.Model, _shipyard.Find(DisgracedShip.Model.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-ship-from-a-later-build")]
    [InlineData("DISGRACED")]
    public void Find_ReturnsNothing_ForAnIdThisBuildDoesNotShip(string? id)
    {
        // ids are identifiers, matched exactly — a near miss is a miss, and the caller falls back
        Assert.Null(_shipyard.Find(id));
    }
}
