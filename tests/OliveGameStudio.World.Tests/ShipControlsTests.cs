namespace OliveGameStudio.World.Tests;

public sealed class ShipControlsTests
{
    [Fact]
    public void Neutral_AsksForNothing()
    {
        Assert.Equal(0, ShipControls.Neutral.Thrust);
        Assert.Equal(0, ShipControls.Neutral.Turn);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    [InlineData(0.4, 0.4)]
    [InlineData(2, 1)]
    [InlineData(-7.5, -1)]
    public void Thrust_IsClampedToWhatTheShipIsRatedFor(double asked, double expected)
    {
        // a device that reports its axes in some other range must not out-fly the ship
        Assert.Equal(expected, new ShipControls(thrust: asked, turn: 0).Thrust);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    [InlineData(0.25, 0.25)]
    [InlineData(3, 1)]
    [InlineData(-3, -1)]
    public void Turn_IsClampedToWhatTheShipIsRatedFor(double asked, double expected)
    {
        Assert.Equal(expected, new ShipControls(thrust: 0, turn: asked).Turn);
    }

    [Fact]
    public void TwoSetsOfControlsAskingForTheSameThingAreEqual()
    {
        Assert.Equal(new ShipControls(1, -1), new ShipControls(4, -9));
    }
}
