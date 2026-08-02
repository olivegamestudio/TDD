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

    // ---- FirstApproachWithin: the journey, not the point it ended on ----

    static readonly Position Marker = new(0, 0);

    [Fact]
    public void FirstApproachWithin_FindsAMarkerPassedInTheMiddleOfTheJourney()
    {
        // neither end is within 25 of the marker, so a point measurement would find nothing
        Assert.Equal(0.34375, Marker.FirstApproachWithin(new Position(0, -300), new Position(0, 500), 25));
    }

    [Fact]
    public void FirstApproachWithin_IsTheFirstApproachAndNotTheClosest()
    {
        // 100 units of the journey are inside the trigger; the answer is where it went in
        double? approach = Marker.FirstApproachWithin(new Position(0, -100), new Position(0, 100), 50);

        Assert.Equal(0.25, approach);
    }

    [Fact]
    public void FirstApproachWithin_IsZero_WhenTheJourneyBeganWithinTheDistance()
    {
        Assert.Equal(0, Marker.FirstApproachWithin(new Position(0, 10), new Position(0, 900), 25));
    }

    [Fact]
    public void FirstApproachWithin_MeasuresAJourneyThatCoveredNoGround_AsThePointItStoodAt()
    {
        Position standingStill = new(15, 20);                  // exactly 25 from the marker

        Assert.Equal(0, Marker.FirstApproachWithin(standingStill, standingStill, 25));
        Assert.Null(Marker.FirstApproachWithin(standingStill, standingStill, 24.999));
    }

    [Fact]
    public void FirstApproachWithin_IncludesTheDistanceItself()
    {
        // the boundary is closed, matching the <= a proximity trigger has always used
        Assert.NotNull(Marker.FirstApproachWithin(new Position(-500, 25), new Position(500, 25), 25));
        Assert.Null(Marker.FirstApproachWithin(new Position(-500, 25.001), new Position(500, 25.001), 25));
    }

    [Fact]
    public void FirstApproachWithin_DoesNotWidenTheDistanceSideways()
    {
        // a journey the length of a field, but never nearer than 200 units to the marker
        Assert.Null(Marker.FirstApproachWithin(new Position(200, -5000), new Position(200, 5000), 25));
    }

    [Fact]
    public void FirstApproachWithin_MeasuresTheSegmentAndNotTheLineThroughIt()
    {
        // heading straight for the marker but stopping 200 short is not arriving at it
        Assert.Null(Marker.FirstApproachWithin(new Position(0, 1000), new Position(0, 200), 25));

        // and neither is a journey that was already past it before it began
        Assert.Null(Marker.FirstApproachWithin(new Position(0, 200), new Position(0, 1000), 25));
    }

    [Fact]
    public void FirstApproachWithin_ReadsBackwardsWhenTheJourneyIsFlownBackwards()
    {
        // the same ground covered the other way round: the fractions are mirrored, which is how a
        // caller with two markers can tell which of them was reached first
        double? forwards = Marker.FirstApproachWithin(new Position(0, -300), new Position(0, 500), 25);
        double? backwards = Marker.FirstApproachWithin(new Position(0, 500), new Position(0, -300), 25);

        Assert.Equal(0.34375, forwards);
        Assert.Equal(0.59375, backwards);
    }

    [Fact]
    public void FirstApproachWithin_NeverMissesAMarkerThatAPointMeasurementWouldHaveFound()
    {
        // sweeping can only bring a marker closer, so nothing that fired before stops firing
        Position from = new(-40, -600);
        Position to = new(90, 700);

        for (double distance = 1; distance <= 2000; distance *= 1.5)
        {
            if (Marker.DistanceTo(from) <= distance || Marker.DistanceTo(to) <= distance)
            {
                Assert.NotNull(Marker.FirstApproachWithin(from, to, distance));
            }
        }
    }

    [Fact]
    public void FirstApproachWithin_MeasuresWhereAJourneyBegan_WhenItEndsNowhere()
    {
        // a position that went non-finite must not quietly drop a marker the player is sitting on
        foreach (double nowhere in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Equal(0, Marker.FirstApproachWithin(new Position(0, 10), new Position(0, nowhere), 25));
            Assert.Null(Marker.FirstApproachWithin(new Position(0, 900), new Position(0, nowhere), 25));
        }
    }

    [Fact]
    public void FirstApproachWithin_MeasuresNothing_WhenTheJourneyBeganNowhere()
    {
        Assert.Null(Marker.FirstApproachWithin(new Position(double.NaN, 0), new Position(0, 0), 25));
    }

    [Fact]
    public void FirstApproachWithin_FallsBackToWhereAnUnmeasurablyLongJourneyBegan()
    {
        // 1e200 units overflows the arithmetic; the answer is still the one that loses nothing
        Assert.Equal(0, Marker.FirstApproachWithin(new Position(0, 10), new Position(0, 1e200), 25));
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(new Position(1, 2), new Position(1, 2));
        Assert.NotEqual(new Position(1, 2), new Position(2, 1));
    }
}
