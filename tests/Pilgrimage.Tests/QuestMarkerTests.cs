namespace Pilgrimage.Tests;

public sealed class QuestMarkerTests
{
    [Fact]
    public void IsReachedBy_ThePositionItSitsOn()
    {
        QuestMarker marker = new(new Position(100, 100), 10);

        Assert.True(marker.IsReachedBy(new Position(100, 100)));
    }

    [Fact]
    public void IsReachedBy_APositionInsideTheRadius()
    {
        QuestMarker marker = new(new Position(0, 0), 50);

        Assert.True(marker.IsReachedBy(new Position(30, 40)));   // exactly 50 away
        Assert.True(marker.IsReachedBy(new Position(3, 4)));     // well inside
    }

    [Fact]
    public void IsNotReachedBy_APositionOutsideTheRadius()
    {
        QuestMarker marker = new(new Position(0, 0), 50);

        Assert.False(marker.IsReachedBy(new Position(0, 50.001)));
        Assert.False(marker.IsReachedBy(new Position(1000, 0)));
    }

    [Fact]
    public void RejectsANegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuestMarker(Position.Origin, -1));
    }

    [Fact]
    public void AZeroRadiusRequiresAnExactPosition()
    {
        QuestMarker marker = new(new Position(5, 5), 0);

        Assert.True(marker.IsReachedBy(new Position(5, 5)));
        Assert.False(marker.IsReachedBy(new Position(5, 5.0001)));
    }
}
