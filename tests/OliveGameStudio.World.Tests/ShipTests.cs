namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers the ship as a thing the player owns, rather than as the physics that flies it.
/// </summary>
public sealed class ShipTests
{
    static readonly ShipHandling Handling = new(Acceleration: 180, Drag: 0.9, TurnRate: 2.5);

    [Fact]
    public void CarriesItsIdItsHandlingAndTheAssetThatDrawsIt()
    {
        Ship ship = new("disgraced", Handling, "ship1");

        Assert.Equal("disgraced", ship.Id);
        Assert.Same(Handling, ship.Handling);
        Assert.Equal("ship1", ship.AssetKey);
    }

    [Fact]
    public void ComparesByValue_SoASavedShipEqualsTheOneItWasRestoredFrom()
    {
        Ship saved = new("disgraced", Handling, "ship1");
        Ship restored = new("disgraced", Handling, "ship1");

        Assert.Equal(saved, restored);
    }

    [Fact]
    public void ShipsWithDifferentIdsAreDifferentShips()
    {
        // two hulls could share a handling profile and a picture and still not be the same ship
        Ship first = new("disgraced", Handling, "ship1");
        Ship second = new("interceptor", Handling, "ship1");

        Assert.NotEqual(first, second);
    }
}
