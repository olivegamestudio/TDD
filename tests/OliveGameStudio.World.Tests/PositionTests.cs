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

    // ---- a journey too long to square: #76 ----

    [Fact]
    public void FractionAlongSegment_FindsTheMiddleOfAJourneyTooLongToSquare()
    {
        // A double squares to infinity somewhere above ~1.34e154. Measuring the journey by
        // squaring its length therefore reported an infinite length, a fraction of zero, and a
        // closest approach at the start — the sweep quietly answering the question point sampling
        // answered while presenting itself as having swept.
        Position halfway = new(0, 1e200);

        Assert.Equal(0.5, halfway.FractionAlongSegment(new Position(0, 0), new Position(0, 2e200)), 10);
    }

    [Fact]
    public void DistanceToSegment_DoesNotMissAMarkerHalfwayAlongAJourneyTooLongToSquare()
    {
        // the defect that matters: a marker the traveller went straight past, 3 units to the side
        // of the midpoint of a journey whose length overflows when squared
        Position marker = new(3, 1e200);

        Assert.Equal(3, marker.DistanceToSegment(new Position(0, 0), new Position(0, 2e200)), 10);
    }

    [Fact]
    public void DistanceToSegment_MeasuresAJourneySpanningTheWholeOfDoublesRange()
    {
        // The journey's own length is not a double — one end minus the other overflows before it
        // is ever squared — so this fails even arithmetic that only avoids squaring. The marker is
        // 5 units to the side of the midpoint, and that is what has to come back.
        Position marker = new(0, 5);

        double distance = marker.DistanceToSegment(new Position(-1e308, 0), new Position(1e308, 0));

        Assert.Equal(5, distance, 10);
    }

    [Theory]
    // the journey's end, on a diagonal long enough that both the length and the dot product
    // overflow — infinity divided by infinity, which is where the NaN came from
    [InlineData(1e200, 1e200, 0d, 0d, 1e200, 1e200)]
    // opposite corners of everything a double can express
    [InlineData(0d, 0d, -1.7976931348623157e308, -1.7976931348623157e308, 1.7976931348623157e308, 1.7976931348623157e308)]
    // a marker at the far edge of the range measured against a journey that barely moves, in both
    // the direction of travel and against it
    [InlineData(1.7976931348623157e308, 1.7976931348623157e308, 0d, 0d, 1e-300, 1e-300)]
    [InlineData(1.7976931348623157e308, -1.7976931348623157e308, 0d, 0d, 1e-300, 1e-300)]
    [InlineData(-1.7976931348623157e308, 1.7976931348623157e308, 0d, 0d, 1e-300, 1e-300)]
    // subnormal journey, ordinary marker
    [InlineData(1000d, 1000d, 0d, 0d, 5e-324, 5e-324)]
    public void FractionAlongSegment_IsAFractionForAnyFiniteJourney(
        double markerX, double markerY, double fromX, double fromY, double toX, double toY)
    {
        // A fraction that is NaN is worse than a wrong one. Every ordered comparison against NaN is
        // false both ways round, so a caller ordering two markers by when the journey reached them
        // gets "neither came first" and quietly does nothing — and that ordering is load bearing:
        // it is what stops a field flown backwards in one frame completing a quest.
        double along = new Position(markerX, markerY)
            .FractionAlongSegment(new Position(fromX, fromY), new Position(toX, toY));

        Assert.False(double.IsNaN(along), $"the fraction came back NaN for a journey of two finite ends");
        Assert.InRange(along, 0, 1);
    }

    [Fact]
    public void FractionAlongSegment_StillOrdersTwoMarkers_OnAJourneyTooLongToSquare()
    {
        // the ordering rule the quest watcher leans on, at a magnitude where the arithmetic used
        // to collapse every fraction to the same zero and report that neither marker came first
        Position from = new(0, 0);
        Position to = new(0, 4e200);

        Assert.True(new Position(0, 1e200).FractionAlongSegment(from, to)
            < new Position(0, 3e200).FractionAlongSegment(from, to));

        Assert.True(new Position(0, 3e200).FractionAlongSegment(to, from)
            < new Position(0, 1e200).FractionAlongSegment(to, from));
    }

    [Fact]
    public void FractionAlongSegment_IsUnchangedAtOrdinaryMagnitudes()
    {
        // rescaling the arithmetic must not cost precision on the path a frame actually takes:
        // a ship settling at 200 units a second, sixty times a second
        Position from = new(0, 0);
        Position to = new(0, 3.3333333333333335);

        Assert.Equal(0.3, new Position(0, 1).FractionAlongSegment(from, to), 15);
        Assert.Equal(0.25, new Position(-40, 17).FractionAlongSegment(new Position(-40, 0), new Position(-40, 68)), 15);
    }
}
