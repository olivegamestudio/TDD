namespace OliveGameStudio.World.Tests;

/// <summary>
/// QA's adversarial cover for <see cref="Position.DistanceToSegment"/>: the boundary values and
/// degenerate journeys a sweep meets at the edges of its input range rather than in ordinary play.
/// </summary>
/// <remarks>
/// The sweep exists so that a trigger cannot be stepped over, which means its failure mode is
/// silence — a marker measured as far away when the traveller went through it fires nothing and
/// reports nothing. These pin the cases where the arithmetic could go quiet: a journey short
/// enough to underflow, one long enough to lose precision, and endpoints that are not numbers.
/// </remarks>
public sealed class PositionSegmentAdversarialTests
{
    /// <summary>
    /// A journey so short that squaring its length underflows to zero must be treated as the
    /// standstill it effectively is, not divided by.
    /// </summary>
    /// <remarks>
    /// <c>1e-200</c> is finite and non-zero, but <c>1e-400</c> is not representable, so
    /// <c>lengthSquared</c> is exactly <c>0</c> and the guard has to be <c>&lt;= 0</c> rather than
    /// <c>== 0</c> reasoning about a negative that cannot happen. If the guard were skipped the
    /// division would produce infinity or <see cref="double.NaN"/> and the marker would silently
    /// stop being measured.
    /// </remarks>
    [Fact]
    public void AJourneyThatUnderflowsWhenSquared_IsMeasuredAsAStandstill()
    {
        Position from = new(0, 0);
        Position to = new(1e-200, 0);
        Position marker = new(0, 7);

        double swept = marker.DistanceToSegment(from, to);

        Assert.False(double.IsNaN(swept), "an underflowing journey divided by zero");
        Assert.Equal(7, swept, 10);
    }

    /// <summary>
    /// A journey spanning far more of the world than a frame ever could still finds a marker in
    /// the middle of it.
    /// </summary>
    /// <remarks>
    /// This is the working range below the overflow bound that #76 covers: at 1e10 units the
    /// squared length is 1e20, comfortably finite, so the sweep must be exact here. It exists to
    /// catch a regression that trades precision for the overflow fix rather than adding to it.
    /// </remarks>
    [Fact]
    public void AVeryLongJourney_StillFindsAMarkerInTheMiddleOfIt()
    {
        Position from = new(-1e10, 0);
        Position to = new(1e10, 0);
        Position marker = new(0, 10);

        Assert.Equal(10, marker.DistanceToSegment(from, to), 6);
    }

    /// <summary>
    /// A marker sitting exactly on either endpoint measures exactly zero, with no rounding drift
    /// from the projection.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(300, 400)]
    public void AMarkerOnAnEndpoint_IsExactlyZeroAway(double x, double y)
    {
        Position from = new(0, 0);
        Position to = new(300, 400);
        Position marker = new(x, y);

        Assert.Equal(0, marker.DistanceToSegment(from, to));
    }

    /// <summary>
    /// Sweeping a journey that went nowhere answers exactly what <see cref="Position.DistanceTo"/>
    /// answers, so the standstill overload on the watcher is genuinely the same rule and not a
    /// near-enough approximation of it.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 4)]
    [InlineData(-1200.5, 880.25)]
    [InlineData(1e8, -1e8)]
    public void AStandstill_MeasuresExactlyAsDistanceToDoes(double x, double y)
    {
        Position standstill = new(17, -23);
        Position marker = new(x, y);

        Assert.Equal(marker.DistanceTo(standstill), marker.DistanceToSegment(standstill, standstill));
    }

    /// <summary>
    /// Every way a non-finite value can reach the measurement answers
    /// <see cref="double.NaN"/> rather than throwing or reporting a bogus finite distance.
    /// </summary>
    /// <remarks>
    /// A bogus finite distance is the dangerous outcome: it would fire a trigger for a journey
    /// that never happened. <see cref="double.NaN"/> compares false either way round, so the
    /// caller measures nothing as near, which is the direction that fails safe.
    /// </remarks>
    [Theory]
    [InlineData(double.NaN, 0, 100, 0, 50, 0)]              // from.X is not a number
    [InlineData(0, double.NaN, 100, 0, 50, 0)]              // from.Y is not a number
    [InlineData(0, 0, double.NaN, 0, 50, 0)]                // to.X is not a number
    [InlineData(0, 0, 100, double.NaN, 50, 0)]              // to.Y is not a number
    [InlineData(0, 0, 100, 0, double.NaN, 0)]               // the marker itself
    [InlineData(0, 0, double.PositiveInfinity, 0, 50, 0)]   // a journey to infinity
    [InlineData(double.NegativeInfinity, 0, 100, 0, 50, 0)] // a journey from infinity
    public void ANonFiniteInput_IsNotANumberRatherThanAWrongNumber(
        double fromX, double fromY, double toX, double toY, double markerX, double markerY)
    {
        Position marker = new(markerX, markerY);

        double swept = marker.DistanceToSegment(new Position(fromX, fromY), new Position(toX, toY));

        Assert.True(double.IsNaN(swept), $"expected NaN, got {swept}");
    }

    /// <summary>
    /// For finite inputs the sweep is always a real, non-negative distance that is no further than
    /// either end of the journey — the property that stops it widening a trigger sideways.
    /// </summary>
    /// <remarks>
    /// Spread over markers before, beside, beyond and exactly on the journey, so a regression that
    /// only misbehaves off the ends is caught as well as one that misbehaves in the middle.
    /// </remarks>
    [Theory]
    [InlineData(-500, -500)]
    [InlineData(-500, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 1)]
    [InlineData(50, -900)]
    [InlineData(100, 0)]
    [InlineData(1e6, 1e6)]
    public void ForFiniteInputs_TheSweepIsRealAndNoFurtherThanEitherEnd(double x, double y)
    {
        Position from = new(0, 0);
        Position to = new(100, 0);
        Position marker = new(x, y);

        double swept = marker.DistanceToSegment(from, to);

        Assert.False(double.IsNaN(swept));
        Assert.True(swept >= 0, $"negative distance {swept}");
        Assert.True(swept <= marker.DistanceTo(from) + 1e-9, "swept further than the near end");
        Assert.True(swept <= marker.DistanceTo(to) + 1e-9, "swept further than the far end");
    }

    /// <summary>
    /// The measurement does not depend on which way the traveller went along the same ground.
    /// </summary>
    /// <remarks>
    /// The watcher relies on this: it makes no claim about direction of travel, so a frame flown
    /// backwards has to measure identically to the same frame flown forwards.
    /// </remarks>
    [Theory]
    [InlineData(50, 30)]
    [InlineData(-20, 5)]
    [InlineData(140, -60)]
    public void TheSameGround_MeasuresTheSameInEitherDirection(double x, double y)
    {
        Position from = new(0, 0);
        Position to = new(100, 0);
        Position marker = new(x, y);

        Assert.Equal(marker.DistanceToSegment(from, to), marker.DistanceToSegment(to, from));
    }
}
