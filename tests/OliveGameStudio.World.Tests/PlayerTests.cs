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
        // the engine ships no ships; a game awards one when it starts or resumes
        Player player = new();

        Assert.Null(player.Ship);
    }

    [Fact]
    public void GiveShip_PutsThePlayerInThatShip()
    {
        Player player = new();
        ShipModel ship = new("scout", "scout1", new ShipHandling(100, 1, 2));

        player.GiveShip(ship);

        Assert.Same(ship, player.Ship);
    }

    [Fact]
    public void GiveShip_ReplacesTheShipThePlayerWasIn()
    {
        // resuming a save awards the starting ship and then the saved one over the top of it
        Player player = new();
        ShipModel first = new("first", "first1", new ShipHandling(100, 1, 2));
        ShipModel second = new("second", "second1", new ShipHandling(200, 1, 2));
        player.GiveShip(first);

        player.GiveShip(second);

        Assert.Same(second, player.Ship);
    }

    [Fact]
    public void GiveShip_RejectsNoShipAtAll()
    {
        // clearing the ship is not a state the game has any use for, and would only show up as
        // a player who cannot be drawn
        Player player = new();

        Assert.Throws<ArgumentNullException>(() => player.GiveShip(null!));
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
