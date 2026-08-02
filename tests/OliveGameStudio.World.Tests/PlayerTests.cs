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

    [Fact]
    public void TravelledFrom_StartsWhereThePlayerDoes()
    {
        Player player = new();

        Assert.Equal(Position.Origin, player.TravelledFrom);
    }

    [Fact]
    public void MoveBy_LeavesTheGroundCovered_Measurable()
    {
        Player player = new();
        player.MoveTo(new Position(0, 500));

        player.MoveBy(0, 900);

        Assert.Equal(new Position(0, 500), player.TravelledFrom);
        Assert.Equal(new Position(0, 1400), player.Position);
    }

    [Fact]
    public void MoveTo_IsAPlacementAndNotAJourney()
    {
        // resuming a save must not read as having flown from wherever the player last was, or
        // everything between the two places gets treated as ground the player covered
        Player player = new();
        player.MoveBy(0, 40);

        player.MoveTo(new Position(0, 9000));

        Assert.Equal(new Position(0, 9000), player.TravelledFrom);
        Assert.Equal(new Position(0, 9000), player.Position);
    }
}
