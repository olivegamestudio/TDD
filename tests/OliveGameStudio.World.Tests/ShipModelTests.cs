namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers the ship model as a value. It is written to and read back from a save, so what counts
/// as "the same ship" is a persistence question rather than a cosmetic one.
/// </summary>
public sealed class ShipModelTests
{
    static ShipModel Ship(string id = "disgraced", string assetKey = "ship1") =>
        new(id, assetKey, new ShipHandling(180, 0.9, 2.5));

    [Fact]
    public void CarriesItsIdentity_ItsGraphicAndItsHandling()
    {
        ShipModel ship = Ship();

        Assert.Equal("disgraced", ship.Id);
        Assert.Equal("ship1", ship.AssetKey);
        Assert.Equal(200, ship.Handling.MaximumSpeed, 6);
    }

    [Fact]
    public void TwoShipsWithTheSameIdentity_AreTheSameShip()
    {
        // a save records the id and the ship is rebuilt from content on load, so the ship that
        // comes back is a different instance of the same thing
        Assert.Equal(Ship(), Ship());
    }

    [Fact]
    public void ShipsWithDifferentIds_AreDifferentShips()
    {
        Assert.NotEqual(Ship(), Ship(id: "scout"));
    }

    [Fact]
    public void ShipsWithDifferentGraphics_AreDifferentShips()
    {
        // the same hull repainted is still a different thing to draw
        Assert.NotEqual(Ship(), Ship(assetKey: "ship2"));
    }
}
