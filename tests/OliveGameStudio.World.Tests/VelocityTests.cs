namespace OliveGameStudio.World.Tests;

public sealed class VelocityTests
{
    [Fact]
    public void Stationary_IsNotMoving()
    {
        Assert.Equal(0, Velocity.Stationary.Speed);
    }

    [Fact]
    public void Speed_IgnoresDirection()
    {
        Assert.Equal(5, new Velocity(3, 4).Speed, 10);
        Assert.Equal(5, new Velocity(-3, -4).Speed, 10);
    }

    [Fact]
    public void Plus_ChangesEachAxis()
    {
        Velocity velocity = new(10, 20);

        Assert.Equal(new Velocity(13, 16), velocity.Plus(3, -4));
    }

    [Fact]
    public void Plus_LeavesTheOriginalAlone()
    {
        Velocity velocity = new(10, 20);

        velocity.Plus(3, -4);

        Assert.Equal(new Velocity(10, 20), velocity);
    }

    [Fact]
    public void Scaled_MultipliesBothAxes()
    {
        Assert.Equal(new Velocity(5, -10), new Velocity(10, -20).Scaled(0.5));
    }

    [Fact]
    public void Scaled_KeepsTheDirection()
    {
        // halving a velocity is coasting, not turning round
        Velocity halved = new Velocity(6, 8).Scaled(0.5);

        Assert.Equal(5, halved.Speed, 10);
        Assert.True(halved.X > 0 && halved.Y > 0);
    }
}
