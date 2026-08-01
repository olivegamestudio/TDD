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
    public void MoveBy_AccumulatesAcrossCalls()
    {
        // many small steps, the shape movement will take once it is driven by input
        Player player = new();

        player.MoveBy(0, 10);
        player.MoveBy(0, 10);
        player.MoveBy(5, 0);

        Assert.Equal(new Position(5, 20), player.Position);
    }

    static Ship AShip(string id = "ship-a") =>
        new(id, $"{id}-art", new ShipHandling(Acceleration: 100, Drag: 1, TurnRate: 2));

    [Fact]
    public void StartsFlyingNothing()
    {
        // a player exists before a game does; the session awards the ship when one begins
        Player player = new();

        Assert.Null(player.Ship);
    }

    [Fact]
    public void Award_GivesThePlayerTheShipToFly()
    {
        Player player = new();
        Ship ship = AShip();

        player.Award(ship);

        Assert.Same(ship, player.Ship);
    }

    [Fact]
    public void Award_ReplacesTheShipThePlayerWasFlying()
    {
        // the player earns a better ship; they do not end up flying both
        Player player = new();
        Ship better = AShip("ship-b");
        player.Award(AShip());

        player.Award(better);

        Assert.Same(better, player.Ship);
    }

    [Fact]
    public void Award_DoesNotMoveThePlayer()
    {
        // being handed a ship is not a teleport, so resuming a save can place and award in any order
        Player player = new();
        player.MoveTo(new Position(40, 120));

        player.Award(AShip());

        Assert.Equal(new Position(40, 120), player.Position);
    }
}
