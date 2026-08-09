using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// How a scene body's authored scale becomes a collision size. The interesting case is the
/// mirrored one: negative scale is ordinary content — the cheapest way to make one rock read as
/// several — and it says which way a body faces, never how big it is.
/// </summary>
public sealed class RegionObstaclesTests
{
    static SceneBody Rock(double scaleX, double scaleY, double rotationDegrees = 0) =>
        new(
            "rock",
            "rock1",
            X: 50,
            Y: 100,
            rotationDegrees,
            scaleX,
            scaleY,
            Layer: "Environment",
            Order: 0,
            Solid: true);

    static ShipMovement Ship() =>
        new(new ShipHandling(Acceleration: 100, Drag: 1, TurnRate: 2), hullRadius: 1);

    [Fact]
    public void ABodyCollidesAtItsSpritesSize_ScaledAsAuthored()
    {
        // rock1 is 985x562 real pixels at 100 authored pixels per unit.
        RegionObstacles.ObstacleSize size = RegionObstacles.SizeOf(Rock(scaleX: 2, scaleY: 3));

        Assert.Equal(19.70, size.Width, precision: 6);
        Assert.Equal(16.86, size.Height, precision: 6);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(-1, -1)]
    public void AMirroredBody_IsTheSameSizeAsAnUnmirroredOne(double scaleX, double scaleY)
    {
        // Mirroring turns a body round; it does not shrink it, and it certainly does not give it a
        // negative width. Scale carries both facts because the content format has nowhere else to
        // put the first one, so the sign is the renderer's business and the magnitude is this
        // one's.
        RegionObstacles.ObstacleSize mirrored = RegionObstacles.SizeOf(Rock(scaleX, scaleY));
        RegionObstacles.ObstacleSize upright = RegionObstacles.SizeOf(Rock(scaleX: 1, scaleY: 1));

        Assert.Equal(upright, mirrored);
    }

    [Fact]
    public void AMirroredBody_KeepsTheSizeItWasScaledTo()
    {
        // The sign goes, the magnitude stays: a rock mirrored *and* enlarged is still enlarged.
        RegionObstacles.ObstacleSize size = RegionObstacles.SizeOf(Rock(scaleX: -1.5, scaleY: 1));

        Assert.Equal(RegionObstacles.SizeOf(Rock(scaleX: 1.5, scaleY: 1)), size);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(-1.5, -2)]
    public void ASceneHoldingAMirroredSolidBody_Seeds(double scaleX, double scaleY)
    {
        // The failure this guards against is not a wrong-sized rock — it is a region that will not
        // load at all. Seed stops at the first body it cannot size, so one mirrored rock takes the
        // whole scene's collision with it, and the ship then flies through everything after it.
        SceneDefinition scene = new("test", [Rock(scaleX, scaleY, rotationDegrees: 45)], []);

        RegionObstacles.Seed(scene, Ship());
    }
}
