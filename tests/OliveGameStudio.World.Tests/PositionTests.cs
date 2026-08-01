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

    static readonly Position From = new(0, 0);
    static readonly Position To = new(0, 100);

    [Fact]
    public void DistanceToSegment_IsZero_ForAPointTheJourneyPassesThrough()
    {
        // the case point sampling misses: the traveller was never measured here, but went through it
        Assert.Equal(0, new Position(0, 50).DistanceToSegment(From, To));
    }

    [Fact]
    public void DistanceToSegment_MeasuresAcrossTheJourney_ForAPointBesideIt()
    {
        Assert.Equal(3, new Position(3, 50).DistanceToSegment(From, To));
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheNearerEnd_ForAPointBeyondTheJourney()
    {
        // 50 past the far end, so it is 50 away — the journey stopped, and so does the measurement
        Assert.Equal(50, new Position(0, 150).DistanceToSegment(From, To));
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheNearerEnd_ForAPointBehindTheJourney()
    {
        Assert.Equal(30, new Position(0, -30).DistanceToSegment(From, To));
    }

    [Fact]
    public void DistanceToSegment_DoesNotMeasureAgainstTheLineThroughTheJourney()
    {
        // on the infinite line through the segment, but a long way past where the travel stopped.
        // Measuring the line would report zero and pull in somewhere never actually visited.
        Assert.Equal(900, new Position(0, 1000).DistanceToSegment(From, To));
    }

    [Fact]
    public void DistanceToSegment_IsThePointDistance_WhenNothingMoved()
    {
        // a frame in which the traveller stood still; the segment collapses to a point
        Position point = new(3, 4);

        Assert.Equal(5, point.DistanceToSegment(From, From));
        Assert.Equal(point.DistanceTo(From), point.DistanceToSegment(From, From));
    }

    [Fact]
    public void DistanceToSegment_DoesNotCareWhichWayTheJourneyWasTravelled()
    {
        Position point = new(7, 15);

        // to nine places rather than exactly: the two orders reach the same closest point by
        // different arithmetic, and which end the fraction is measured from is not worth an ulp
        Assert.Equal(point.DistanceToSegment(From, To), point.DistanceToSegment(To, From), 9);
    }

    [Theory]
    [InlineData(0, 50)]         // beside the middle
    [InlineData(400, 400)]      // off in the corner
    [InlineData(-20, -70)]      // behind the start
    [InlineData(0, 100)]        // on the far end
    public void DistanceToSegment_IsNeverFurtherThanEitherEnd(double x, double y)
    {
        // the property that matters: sweeping can only ever bring a marker closer, never push it
        // away, so nothing that fired under point sampling stops firing
        Position point = new(x, y);

        double swept = point.DistanceToSegment(From, To);

        Assert.True(swept <= point.DistanceTo(From), "the sweep pushed the start further away");
        Assert.True(swept <= point.DistanceTo(To), "the sweep pushed the end further away");
    }
}
