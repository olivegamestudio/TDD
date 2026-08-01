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
    public void DistanceToSegment_IsZero_ForAPointOnTheSegment()
    {
        Position halfway = new(0, 500);

        // projecting onto the segment is floating point arithmetic, so the comparisons that go
        // through it are to a precision rather than to the bit
        Assert.Equal(0, halfway.DistanceToSegment(new Position(0, 0), new Position(0, 1000)), 9);
    }

    [Fact]
    public void DistanceToSegment_MeasuresPerpendicularly_FromBesideTheSegment()
    {
        // the marker sits 3 units to the side of a segment that runs straight past it
        Position marker = new(3, 400);

        Assert.Equal(3, marker.DistanceToSegment(new Position(0, 0), new Position(0, 1000)), 9);
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheNearerEnd_ForAPointBeyondTheSegment()
    {
        // not the infinite line: a segment that stops short of the marker has not reached it, and
        // measuring against the line would fire triggers the player never travelled past
        Position marker = new(0, 1000);

        Assert.Equal(400, marker.DistanceToSegment(new Position(0, 0), new Position(0, 600)));
    }

    [Fact]
    public void DistanceToSegment_MeasuresToTheStart_ForAPointBehindIt()
    {
        Position marker = new(0, -50);

        Assert.Equal(50, marker.DistanceToSegment(new Position(0, 0), new Position(0, 600)));
    }

    [Fact]
    public void DistanceToSegment_OfAStandstill_IsTheDistanceToThePoint()
    {
        // a frame that covered no ground is a point, and has to measure like one
        Position marker = new(3, 4);
        Position stoodStill = new(0, 0);

        Assert.Equal(5, marker.DistanceToSegment(stoodStill, stoodStill));
    }

    [Fact]
    public void DistanceToSegment_DoesNotDependOnTheDirectionOfTravel()
    {
        Position marker = new(20, 300);
        Position a = new(0, 0);
        Position b = new(0, 1000);

        Assert.Equal(marker.DistanceToSegment(a, b), marker.DistanceToSegment(b, a), 9);
    }

    [Fact]
    public void DistanceToSegment_IsNeverFurtherThanEitherEnd()
    {
        Position marker = new(5, 500);
        Position a = new(0, 0);
        Position b = new(0, 1000);

        double swept = marker.DistanceToSegment(a, b);

        Assert.True(swept <= marker.DistanceTo(a));
        Assert.True(swept <= marker.DistanceTo(b));
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
