namespace OliveGameStudio.World.Tests;

public sealed class PositionTests
{
    [Fact]
    public void DistanceTo_Itself_IsZero()
    {
        Position position = new(12, -4);

        Assert.Equal(0, position.DistanceTo(position));
    }

    [Fact]
    public void DistanceTo_MeasuresAlongBothAxes()
    {
        // 3-4-5 triangle, so the expected value is exact
        Position origin = new(0, 0);

        Assert.Equal(5, origin.DistanceTo(new Position(3, 4)));
    }

    [Fact]
    public void DistanceTo_IsSymmetric()
    {
        Position a = new(-7, 2);
        Position b = new(11, 30);

        Assert.Equal(a.DistanceTo(b), b.DistanceTo(a));
    }

    [Fact]
    public void Offset_MovesByTheGivenAmount()
    {
        Position moved = new Position(10, 20).Offset(-2, 5);

        Assert.Equal(new Position(8, 25), moved);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(new Position(1, 2), new Position(1, 2));
        Assert.NotEqual(new Position(1, 2), new Position(2, 1));
    }

    // ---- measuring against a journey rather than a point ----

    [Fact]
    public void DistanceToSegment_OfAStandstill_IsTheDistanceToWhereTheTravellerStood()
    {
        Position marker = new(0, 0);
        Position standingAt = new(3, 4);

        Assert.Equal(5, marker.DistanceToSegment(standingAt, standingAt));
    }

    [Fact]
    public void DistanceToSegment_MeasuresSideways_ForAMarkerBesideTheJourney()
    {
        // the journey runs up the Y axis; the marker is 7 units to the side of its middle
        Position marker = new(7, 500);

        Assert.Equal(7, marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000)));
    }

    [Fact]
    public void DistanceToSegment_IsZero_ForAMarkerTheJourneyPassesThrough()
    {
        Position marker = new(0, 500);

        Assert.Equal(0, marker.DistanceToSegment(new Position(0, -200), new Position(0, 900)));
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheFarEnd_ForAMarkerBeyondIt()
    {
        // a segment, not the line through it: the journey stopped, so the marker is 100 away
        Position marker = new(0, 1100);

        Assert.Equal(100, marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000)));
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheNearEnd_ForAMarkerBehindIt()
    {
        // ground the traveller never covered, so the marker is measured to where they set off
        Position marker = new(0, -100);

        Assert.Equal(100, marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000)));
    }

    [Fact]
    public void DistanceToSegment_IsTheSameEitherWayAlongTheJourney()
    {
        // direction of travel is not a thing a distance knows about
        Position marker = new(7, 500);
        Position from = new(0, 0);
        Position to = new(0, 1000);

        Assert.Equal(marker.DistanceToSegment(from, to), marker.DistanceToSegment(to, from));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 500)]
    [InlineData(-300, 1200)]
    [InlineData(1e6, -1e6)]
    [InlineData(0.5, -0.25)]
    public void DistanceToSegment_IsNeverFurtherThanEitherEnd(double x, double y)
    {
        // sweeping can only bring a marker closer, so nothing that fired before stops firing
        Position marker = new(x, y);
        Position from = new(-40, 60);
        Position to = new(900, 1300);

        double swept = marker.DistanceToSegment(from, to);

        Assert.True(swept <= marker.DistanceTo(from), "the sweep pushed the marker further than the start");
        Assert.True(swept <= marker.DistanceTo(to), "the sweep pushed the marker further than the end");
    }

    [Fact]
    public void DistanceToSegment_OfANonFiniteJourney_IsNotANumberRatherThanAThrow()
    {
        // Pinned because it decides what a trigger does with one: every comparison against NaN is
        // false, so a marker measured against a journey that went nowhere real fires nothing. That
        // is the safe answer, but it is safe by arithmetic rather than by intent — worth stating
        // here so a later change to how positions are bounded can see what it would be changing.
        Position marker = new(0, 0);

        Assert.True(double.IsNaN(marker.DistanceToSegment(new Position(0, 0), new Position(double.NaN, 0))));
    }
}
