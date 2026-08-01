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

    // ---- the ship the player is flying ----

    static Ship AShip(string id = "starter") =>
        new(id, $"{id}-art", new ShipHandling(Acceleration: 100, Drag: 1, TurnRate: 2));

    [Fact]
    public void FliesNothing_UntilAShipIsAwarded()
    {
        // a player exists before a game does: the engine ships no ships, so there is nothing to
        // put here until a game awards one
        Player player = new();

        Assert.Null(player.Ship);
    }

    [Fact]
    public void Award_GivesThePlayerAShipToFly()
    {
        Player player = new();
        Ship ship = AShip();

        player.Award(ship);

        Assert.Same(ship, player.Ship);
    }

    [Fact]
    public void Award_ReplacesTheShipThePlayerWasFlying()
    {
        // earning a better hull is one award over another, not a second player
        Player player = new();
        Ship earned = AShip("earned");

        player.Award(AShip());
        player.Award(earned);

        Assert.Same(earned, player.Ship);
    }

    [Fact]
    public void Award_DoesNotMoveThePlayer()
    {
        // awarding is not a move, so placing a player and giving them a ship are independent and
        // neither ordering on the resume path can quietly teleport them
        Player player = new();
        player.MoveTo(new Position(12, -34));

        player.Award(AShip());

        Assert.Equal(new Position(12, -34), player.Position);
    }

    [Fact]
    public void Award_RefusesNothing()
    {
        // flying null is not a state the player can be in: the caller has a bug, and swallowing it
        // here would surface as a null reference on whatever draws the ship, a long way from cause
        Player player = new();

        Assert.Throws<ArgumentNullException>(() => player.Award(null!));
    }
}
