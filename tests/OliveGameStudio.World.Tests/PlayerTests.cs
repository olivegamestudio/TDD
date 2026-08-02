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

    // ---- the ground covered since the last frame ----

    [Fact]
    public void TakeJourney_OfAPlayerWhoHasNotMoved_BeginsAndEndsWhereTheyAre()
    {
        Player player = new();
        player.MoveTo(new Position(0, 700));

        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 700), from);
        Assert.Equal(new Position(0, 700), to);
    }

    [Fact]
    public void TakeJourney_SpansEveryStepFlownSinceItWasLastTaken()
    {
        Player player = new();
        player.TakeJourney();

        player.MoveBy(0, 10);
        player.MoveBy(0, 10);
        player.MoveBy(5, 0);

        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(Position.Origin, from);
        Assert.Equal(new Position(5, 20), to);
    }

    [Fact]
    public void TakeJourney_BeginsTheNextOneWhereTheLastEnded()
    {
        Player player = new();
        player.MoveBy(0, 10);
        player.TakeJourney();

        player.MoveBy(0, 10);
        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 10), from);
        Assert.Equal(new Position(0, 20), to);
    }

    [Fact]
    public void TakeJourney_AfterAPlacement_CoversNoGround()
    {
        // The distinction this exists for. A player who was placed — a new game, or a save resumed
        // a thousand units out — did not fly there, and sweeping the line from wherever they last
        // were would run them through every marker in between on a journey nobody made.
        Player player = new();
        player.MoveBy(0, 10);

        player.MoveTo(new Position(0, 700));
        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 700), from);
        Assert.Equal(new Position(0, 700), to);
    }

    [Fact]
    public void TakeJourney_AfterAPlacementMidFlight_StartsFromWhereThePlayerWasPut()
    {
        Player player = new();
        player.MoveBy(0, 10);
        player.MoveTo(new Position(0, 700));

        player.MoveBy(0, 40);
        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 700), from);
        Assert.Equal(new Position(0, 740), to);
    }
}
