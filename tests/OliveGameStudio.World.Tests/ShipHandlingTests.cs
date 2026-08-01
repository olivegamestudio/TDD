namespace OliveGameStudio.World.Tests;

public sealed class ShipHandlingTests
{
    [Fact]
    public void MaximumSpeed_IsWhereTheAccelerationAndTheDragBalance()
    {
        Assert.Equal(200, new ShipHandling(Acceleration: 180, Drag: 0.9, TurnRate: 1).MaximumSpeed, 10);
    }

    [Fact]
    public void MoreDragMakesForASlowerShip()
    {
        ShipHandling nimble = new(Acceleration: 100, Drag: 0.5, TurnRate: 1);

        Assert.True((nimble with { Drag = 2.0 }).MaximumSpeed < nimble.MaximumSpeed);
    }

    [Fact]
    public void MaximumSpeedFollowsTheOtherTwo_RatherThanBeingSetBesideThem()
    {
        // an upgrade that changes the acceleration must not leave a stale top speed behind it
        ShipHandling ship = new(Acceleration: 100, Drag: 1, TurnRate: 1);

        Assert.Equal(300, (ship with { Acceleration = 300 }).MaximumSpeed, 10);
    }

    [Fact]
    public void SecondsForAFullCircle_IsHowLongTheHelmTakesToComeAllTheWayRound()
    {
        // π radians a second is half a circle a second, so a whole one takes two
        Assert.Equal(2, new ShipHandling(Acceleration: 100, Drag: 1, TurnRate: Math.PI).SecondsForAFullCircle, 10);
    }

    [Fact]
    public void SecondsForAFullCircleFollowsTheTurnRate_RatherThanBeingSetBesideIt()
    {
        // the same argument as MaximumSpeed, and it is not hypothetical: the turn rate, the test
        // asserting how fast the ship comes about and the comment describing it had drifted into
        // three different claims about the same ship. Derived, there is only one claim to make.
        ShipHandling ship = new(Acceleration: 100, Drag: 1, TurnRate: 1);

        Assert.Equal(Math.Tau / 4, (ship with { TurnRate = 4 }).SecondsForAFullCircle, 10);
    }

    [Fact]
    public void AShipWithNoHelm_NeverComesAbout()
    {
        // a turn rate of zero is allowed — a ship on rails — and has to report something rather
        // than divide by zero
        Assert.Equal(
            double.PositiveInfinity,
            new ShipHandling(Acceleration: 100, Drag: 1, TurnRate: 0).SecondsForAFullCircle);
    }
}
