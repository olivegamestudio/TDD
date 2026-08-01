namespace OliveGameStudio.World.Tests;

public sealed class PlayerTests
{
    [Fact]
    public void StartsAtTheOrigin()
    {
        Player player = new();

        Assert.Equal(Position.Origin, player.Position);
    }

    [Fact]
    public void MoveTo_PutsThePlayerAtTheGivenPosition()
    {
        Player player = new();

        player.MoveTo(new Position(40, 120));

        Assert.Equal(new Position(40, 120), player.Position);
    }

    [Fact]
    public void StartsWithNoShip()
    {
        // a player exists before a game does; the ship arrives when one is awarded
        Player player = new();

        Assert.Null(player.Ship);
    }

    [Fact]
    public void Award_GivesThePlayerTheShip()
    {
        Player player = new();
        Ship ship = new("disgraced", new ShipHandling(180, 0.9, 2.5), "ship1");

        player.Award(ship);

        Assert.Same(ship, player.Ship);
    }

    [Fact]
    public void Award_ReplacesTheShipThePlayerWasFlying()
    {
        Player player = new();
        Ship better = new("interceptor", new ShipHandling(240, 0.8, 3), "ship2");
        player.Award(new Ship("disgraced", new ShipHandling(180, 0.9, 2.5), "ship1"));

        player.Award(better);

        Assert.Same(better, player.Ship);
    }

    [Fact]
    public void Award_DoesNotMoveThePlayer()
    {
        // changing ship is not travel
        Player player = new();
        player.MoveTo(new Position(40, 120));

        player.Award(new Ship("disgraced", new ShipHandling(180, 0.9, 2.5), "ship1"));

        Assert.Equal(new Position(40, 120), player.Position);
    }

    [Fact]
    public void MoveBy_AccumulatesAcrossCalls()
    {
        // many small steps, the shape movement will take once it is driven by input
        Player player = new();

        player.MoveBy(0, 10);
        player.MoveBy(0, 10);
        player.MoveBy(5, 0);

        Assert.Equal(new Position(5, 20), player.Position);
    }
}
