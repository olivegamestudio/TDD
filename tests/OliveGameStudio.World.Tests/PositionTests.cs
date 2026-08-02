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

    // ---- measuring against a journey rather than a point ----

    [Fact]
    public void DistanceToSegment_IsMeasuredAtTheClosestApproach_NotAtEitherEnd()
    {
        // the whole point of the segment measure: a marker 3 units off the middle of a journey is
        // 3 away, however far away both ends of that journey are
        Position marker = new(3, 500);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000));

        Assert.Equal(3, distance);
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheNearerEnd_WhenTheClosestApproachIsBeyondIt()
    {
        // a marker behind the start of the journey is measured to the start, not to the infinite
        // line the journey lies on — a segment ends where the travelling stopped
        Position marker = new(0, -10);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000));

        Assert.Equal(10, distance);
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheFarEnd_WhenTheJourneyStopsShort()
    {
        Position marker = new(0, 1010);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000));

        Assert.Equal(10, distance);
    }

    [Fact]
    public void DistanceToSegment_OfAJourneyThatWentNowhere_IsTheDistanceToThatPoint()
    {
        // a frame in which the player did not move: the segment collapses to its own endpoint,
        // and the answer has to be the point measure rather than a division by zero
        Position marker = new(3, 4);
        Position stoodStill = new(0, 0);

        Assert.Equal(5, marker.DistanceToSegment(stoodStill, stoodStill));
    }

    [Fact]
    public void DistanceToSegment_DoesNotWidenTheApproachSideways()
    {
        // the segment measure must not turn a trigger into a corridor: a marker abeam of a long
        // journey is as far away as it ever was
        Position marker = new(400, 500);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000));

        Assert.Equal(400, distance);
    }

    [Fact]
    public void DistanceToSegment_IsTheSameJourneyTravelledEitherWay()
    {
        Position marker = new(17, 240);
        Position from = new(-30, 40);
        Position to = new(90, 900);

        // to ten decimal places rather than exactly: the two directions do the same arithmetic on
        // opposite signs, which is not required to land on the same bits
        Assert.Equal(marker.DistanceToSegment(from, to), marker.DistanceToSegment(to, from), 10);
    }

    [Fact]
    public void DistanceToSegment_OfADiagonalJourney_IsThePerpendicularDistance()
    {
        // (10,0) against the line y = x: half the diagonal of a 10 unit square
        Position marker = new(10, 0);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(100, 100));

        Assert.Equal(Math.Sqrt(50), distance, 10);
    }

    [Fact]
    public void DistanceToSegment_SurvivesAJourneyLongEnoughToOverflowItsOwnSquare()
    {
        // the length is never squared, so a journey past ~1.3e154 — where a double squares to
        // infinity — still reports the closest approach rather than degrading to one end
        Position marker = new(5, 1e200);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 2e200));

        Assert.Equal(5, distance);
    }

    [Fact]
    public void DistanceToSegment_OfAJourneyThatIsNotANumber_IsNotANumber()
    {
        // it does not throw, and NaN compares false against any trigger distance, so a journey
        // that cannot be measured fires nothing rather than firing everything
        Position marker = new(0, 0);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(double.NaN, 0));

        Assert.True(double.IsNaN(distance));
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
}
