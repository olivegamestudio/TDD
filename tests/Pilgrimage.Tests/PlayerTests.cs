namespace Pilgrimage.Tests;

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
}
