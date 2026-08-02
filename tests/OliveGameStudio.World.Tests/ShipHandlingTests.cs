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
}
