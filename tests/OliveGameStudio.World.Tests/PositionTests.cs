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
        // squaring the gap before taking the root overflows at anything wider than the root of
        // double.MaxValue — 1.34e154 — so two perfectly real points answered Infinity. A caller
        // measuring proximity gets no error out of that, just a comparison that is never true.
        Position far = new(1e300, 0);

        Assert.Equal(1e300, far.DistanceTo(Position.Origin));
    }

    [Fact]
    public void DistanceTo_IsFinite_WhenBothAxesAreAstronomical()
    {
        Position far = new(1e300, 1e300);

        Assert.True(double.IsFinite(far.DistanceTo(Position.Origin)));
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
