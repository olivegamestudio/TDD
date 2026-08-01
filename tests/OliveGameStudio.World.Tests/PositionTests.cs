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
    public void DistanceTo_Overflows_OnlyWhenTheAnswerItselfWillNotFitInADouble()
    {
        // Hypot moved the failure from "the intermediate square overflowed" to "the answer does",
        // which is as far as a double can be taken. Worth pinning the difference: 1e300 apart is a
        // number now, and the only gap that still answers Infinity is one no double could hold.
        // Anything a caller bounds its own coordinates to is comfortably the first case.
        Assert.True(double.IsFinite(new Position(1e300, 0).DistanceTo(Position.Origin)));
        Assert.True(double.IsFinite(new Position(1e308, 1e308).DistanceTo(Position.Origin)));
        Assert.False(double.IsFinite(new Position(-1e308, 0).DistanceTo(new Position(1e308, 0))));
    }

    [Fact]
    public void Offset_AtAnAstronomicalPosition_DoesNotMoveAtAll()
    {
        // the arithmetic fact every coordinate bound rests on: a double's own steps eventually grow
        // wider than the move being asked for, and the position swallows it. A ship out here reports
        // motion every frame and never goes anywhere, which is why callers bound their coordinates
        // rather than trusting finiteness.
        // the step has to be on the astronomical axis to be swallowed — a ship out here can still
        // move on an axis that is near zero, which is exactly why the failure is so quiet
        Assert.Equal(new Position(1e300, 0), new Position(1e300, 0).Offset(10, 0));
        Assert.Equal(new Position(0, 1e300), new Position(0, 1e300).Offset(0, 10));
    }

    [Fact]
    public void Offset_StillMoves_AtTheScalesAGameCanPlausiblyBound()
    {
        // the other side of the same fact, and the one that makes a bound worth choosing carefully:
        // a ten unit step is exact well past a billion units out, so a bound drawn there costs a
        // player nothing.
        Assert.Equal(new Position(0, 1e9 + 10), new Position(0, 1e9).Offset(0, 10));
        Assert.Equal(new Position(1e9 + 10, 0), new Position(1e9, 0).Offset(10, 0));
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
