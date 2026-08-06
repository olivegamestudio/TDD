namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers walking a route. The cases that matter are the ones a frame rate produces: a frame long
/// enough to cross several waypoints must follow the route round them rather than cut the corner,
/// and the answer after a second of walking must not depend on how many frames that second was
/// cut into.
/// </summary>
public sealed class NpcPatrolTests
{
    static NpcPatrol Patrol(double speed, params Position[] waypoints) => new(speed, waypoints);

    static void AssertClose(Position expected, Position actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }

    [Fact]
    public void APatrolKeepsWhatItWasGiven()
    {
        NpcPatrol patrol = Patrol(10, new Position(0, 0), new Position(0, 100));

        Assert.Equal(10, patrol.Speed);
        Assert.Equal([new Position(0, 0), new Position(0, 100)], patrol.Waypoints);
    }

    [Fact]
    public void WalkingCoversSpeedTimesTime()
    {
        NpcPatrol patrol = Patrol(10, new Position(0, 100));

        Position at = patrol.Advance(new Position(0, 0), TimeSpan.FromSeconds(2));

        AssertClose(new Position(0, 20), at);
    }

    [Fact]
    public void AFrameOfNoTime_MovesNobody()
    {
        NpcPatrol patrol = Patrol(10, new Position(0, 100));

        Position at = patrol.Advance(new Position(3, 4), TimeSpan.Zero);

        Assert.Equal(new Position(3, 4), at);
    }

    [Fact]
    public void ArrivingAtAWaypoint_HeadsForTheNext()
    {
        NpcPatrol patrol = Patrol(10, new Position(0, 10), new Position(10, 10));

        patrol.Advance(new Position(0, 0), TimeSpan.FromSeconds(1));

        Assert.Equal(new Position(10, 10), patrol.Next);
    }

    [Fact]
    public void AfterTheLastWaypoint_ItStartsTheRouteAgain()
    {
        NpcPatrol patrol = Patrol(10, new Position(0, 10), new Position(0, 20));

        patrol.Advance(new Position(0, 0), TimeSpan.FromSeconds(2));

        Assert.Equal(new Position(0, 10), patrol.Next);
    }

    [Fact]
    public void ALongFrame_FollowsTheRouteRoundACorner()
    {
        // the corner is the whole point: sampling one waypoint a frame would put the walker on the
        // straight line between where they were and where they ended up, which is not the route
        NpcPatrol patrol = Patrol(10, new Position(0, 10), new Position(10, 10));

        Position at = patrol.Advance(new Position(0, 0), TimeSpan.FromSeconds(1.5));

        AssertClose(new Position(5, 10), at);
    }

    [Fact]
    public void TheAnswerDoesNotDependOnHowTheTimeWasCutUp()
    {
        NpcPatrol coarse = Patrol(10, new Position(0, 10), new Position(10, 10));
        NpcPatrol fine = Patrol(10, new Position(0, 10), new Position(10, 10));

        Position inOneFrame = coarse.Advance(new Position(0, 0), TimeSpan.FromSeconds(1.5));

        Position inFifteen = new(0, 0);
        for (int frame = 0; frame < 15; frame++)
        {
            inFifteen = fine.Advance(inFifteen, TimeSpan.FromSeconds(0.1));
        }

        AssertClose(inOneFrame, inFifteen);
    }

    [Fact]
    public void ARouteThatGoesNowhere_LeavesTheWalkerWhereTheyAre()
    {
        // every waypoint on the same spot: the walk spends no distance reaching any of them, so
        // without a stop it would go round the route forever inside one frame
        NpcPatrol patrol = Patrol(10, new Position(5, 5), new Position(5, 5));

        Position at = patrol.Advance(new Position(5, 5), TimeSpan.FromSeconds(1));

        Assert.Equal(new Position(5, 5), at);
    }

    [Fact]
    public void AOneWaypointPatrol_WalksThereAndStays()
    {
        NpcPatrol patrol = Patrol(10, new Position(0, 5));

        Position arriving = patrol.Advance(new Position(0, 0), TimeSpan.FromSeconds(1));
        Position after = patrol.Advance(arriving, TimeSpan.FromSeconds(1));

        Assert.Equal(new Position(0, 5), arriving);
        Assert.Equal(new Position(0, 5), after);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APatrolRefusesASpeedThatIsNotWalking(double speed)
    {
        // standing still is said by having no patrol at all, not by having one that goes nowhere
        Assert.Throws<ArgumentOutOfRangeException>(() => Patrol(speed, new Position(0, 1)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void APatrolRefusesASpeedThatIsNotANumber(double speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Patrol(speed, new Position(0, 1)));
    }

    [Fact]
    public void APatrolRefusesNoRouteAtAll()
    {
        Assert.Throws<ArgumentNullException>(() => new NpcPatrol(10, null!));
    }

    [Fact]
    public void APatrolRefusesAnEmptyRoute()
    {
        Assert.Throws<ArgumentException>(() => Patrol(10));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void APatrolRefusesAWaypointThatIsNotAPlace(double coordinate)
    {
        // every distance measured to it would be NaN, and every comparison against that is false
        Assert.Throws<ArgumentException>(() => Patrol(10, new Position(coordinate, 0)));
        Assert.Throws<ArgumentException>(() => Patrol(10, new Position(0, coordinate)));
    }

    [Fact]
    public void WalkingRefusesTimeRunningBackwards()
    {
        NpcPatrol patrol = Patrol(10, new Position(0, 10));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => patrol.Advance(Position.Origin, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WalkingRefusesAFrameNoDistanceCouldCover()
    {
        // the walk spends the distance it is given, so an infinite one is never spent — refused
        // where the two numbers meet rather than left to hang the frame it arrived on
        NpcPatrol patrol = Patrol(double.MaxValue, new Position(0, 10));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => patrol.Advance(Position.Origin, TimeSpan.MaxValue));
    }
}
