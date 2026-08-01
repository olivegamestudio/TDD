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
    public void DistanceTo_FromAnAstronomicalPosition_IsStillANumber()
    {
        // Squaring the gap before rooting it overflows at 1.34e154 (the root of double.MaxValue),
        // so a distance between two real points came back as Infinity. Infinity is not a distance,
        // and every proximity test measured against it silently answers "no".
        Position far = new(1e300, 0);

        Assert.True(double.IsFinite(far.DistanceTo(Position.Origin)), "distance was not a number");
        Assert.Equal(1e300, far.DistanceTo(Position.Origin));
    }

    [Fact]
    public void DistanceTo_DoesNotOverflowOnEitherAxis()
    {
        // Both axes at once is the case the intermediate square cannot survive at all.
        Position far = new(1e300, 1e300);

        Assert.True(double.IsFinite(far.DistanceTo(Position.Origin)), "distance was not a number");
    }

    [Fact]
    public void DistanceTo_StaysAccurateForOrdinaryDistances()
    {
        // The overflow-safe form must not cost precision where the game actually plays.
        Position marker = new(0, 1000);

        Assert.Equal(300, new Position(0, 700).DistanceTo(marker));
        Assert.Equal(5, new Position(3, 4).DistanceTo(Position.Origin));
    }

    [Fact]
    public void MaxCoordinate_IsTheLastPlaceAOneUnitMoveStillRegisters()
    {
        // This is the whole reason the bound sits where it does: past it, a double's steps are
        // wider than a world unit, so the ship burns fuel and the position never changes. Pin both
        // sides of the edge, so moving the constant has to be a decision rather than an accident.
        Position edge = new(Position.MaxCoordinate, 0);
        Assert.NotEqual(edge, edge.Offset(1, 0));

        Position beyond = new(Position.MaxCoordinate * 4, 0);
        Assert.Equal(beyond, beyond.Offset(1, 0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-123456.75, 987654.5)]
    [InlineData(Position.MaxCoordinate, -Position.MaxCoordinate)]
    public void IsWithinPlayableSpace_AcceptsAnywhereThePlayerCanFly(double x, double y)
    {
        Assert.True(new Position(x, y).IsWithinPlayableSpace);
    }

    [Theory]
    [InlineData(1e300, 0)]
    [InlineData(double.MaxValue, 0)]
    [InlineData(0, -1e300)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(0, double.NegativeInfinity)]
    [InlineData(double.NaN, 0)]
    public void IsWithinPlayableSpace_RefusesSomewhereNothingCanBeReachedFrom(double x, double y)
    {
        Assert.False(new Position(x, y).IsWithinPlayableSpace);
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
