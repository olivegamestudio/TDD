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
    public void DistanceToSegment_MeasuresPerpendicularlyToTheGroundCovered()
    {
        // the marker is 30 to the side of a journey that passes right by it, and 500 from either
        // end of it, so only measuring the segment gives 30
        Position marker = new(30, 500);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000));

        Assert.Equal(30, distance, 10);
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheNearerEnd_ForAMarkerBeyondTheJourney()
    {
        // 200 past the end of the journey: the segment stops, so this must not be measured against
        // the infinite line through it, which would say 0
        Position marker = new(0, 1200);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000));

        Assert.Equal(200, distance, 10);
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheStart_ForAMarkerBehindTheJourney()
    {
        Position marker = new(0, -200);

        double distance = marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000));

        Assert.Equal(200, distance, 10);
    }

    [Fact]
    public void DistanceToSegment_OfAJourneyThatWentNowhere_IsTheDistanceToThatPoint()
    {
        // a standing frame is a segment whose ends coincide, and sweeping it must be the same
        // measurement as not sweeping at all
        Position marker = new(3, 4);
        Position standingStill = new(0, 0);

        Assert.Equal(
            marker.DistanceTo(standingStill),
            marker.DistanceToSegment(standingStill, standingStill));
    }

    [Fact]
    public void DistanceToSegment_IsNeverFurtherThanEitherEnd()
    {
        // the property that matters: sweeping can only bring a marker closer, so nothing that
        // fired under point sampling stops firing under a sweep
        Position from = new(-40, 17);
        Position to = new(910, -230);

        foreach (Position marker in new Position[]
                 {
                     new(0, 0), new(500, 500), new(-1000, -1000), new(900, -229), new(12, -3),
                 })
        {
            double swept = marker.DistanceToSegment(from, to);

            Assert.True(swept <= marker.DistanceTo(from), $"{marker} is further from the journey than from its start");
            Assert.True(swept <= marker.DistanceTo(to), $"{marker} is further from the journey than from its end");
        }
    }

    [Fact]
    public void DistanceToSegment_OfAJourneyWithAnEndThatIsNotANumber_IsNotANumber()
    {
        // A journey with a non-finite end is not a journey, and nothing sensible can be measured
        // against it. It answers NaN, and every comparison against NaN is false, so a caller
        // testing a trigger distance fires nothing rather than firing wrongly. Pinned so the
        // failure stays deliberate: positions that cannot go non-finite are a separate rule kept
        // at the save boundary, not here.
        Position marker = new(0, 0);

        Assert.True(double.IsNaN(marker.DistanceToSegment(new Position(0, 0), new Position(double.NaN, 0))));
        Assert.True(double.IsNaN(marker.DistanceToSegment(new Position(double.NaN, 0), new Position(0, 0))));
    }

    [Fact]
    public void FractionAlongSegment_IsWhereTheJourneyPassedTheMarker()
    {
        Position quarterOfTheWay = new(0, 250);

        Assert.Equal(0.25, quarterOfTheWay.FractionAlongSegment(new Position(0, 0), new Position(0, 1000)), 10);
    }

    [Fact]
    public void FractionAlongSegment_IsClampedToTheEndsOfTheJourney()
    {
        Position from = new(0, 0);
        Position to = new(0, 1000);

        Assert.Equal(0, new Position(0, -500).FractionAlongSegment(from, to));
        Assert.Equal(1, new Position(0, 1500).FractionAlongSegment(from, to));
    }

    [Fact]
    public void FractionAlongSegment_OfAJourneyThatWentNowhere_IsTheStart()
    {
        Position standingStill = new(7, 7);

        Assert.Equal(0, new Position(0, 500).FractionAlongSegment(standingStill, standingStill));
    }

    [Fact]
    public void FractionAlongSegment_OrdersTwoMarkersByWhenTheJourneyReachedThem()
    {
        // what the quest watcher needs from it: the journey met the near marker before the far one
        Position from = new(0, -100);
        Position to = new(0, 1100);

        Assert.True(new Position(0, 0).FractionAlongSegment(from, to)
            < new Position(0, 1000).FractionAlongSegment(from, to));

        // and flown the other way round, the far marker comes first
        Assert.True(new Position(0, 1000).FractionAlongSegment(to, from)
            < new Position(0, 0).FractionAlongSegment(to, from));
    }
}
