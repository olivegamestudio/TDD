using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers <see cref="CollisionDebugView"/>: that it draws a ring for the hull and a rectangle for
/// every solid body, that a non-solid body draws nothing, and that turning it off draws nothing at
/// all.
/// </summary>
public sealed class CollisionDebugViewTests
{
    /// <summary>
    /// A rectangle is drawn as four line segments, one per side — see
    /// <c>CollisionDebugView.DrawRectangle</c>.
    /// </summary>
    const int SegmentsPerObstacle = 4;

    static SceneBody Rock(string name, double x, double y, bool solid) =>
        new(name, "rock1", x, y, RotationDegrees: 0, ScaleX: 1, ScaleY: 1, Layer: "Environment", Order: 0, Solid: solid);

    [Fact]
    public void Disabled_DrawsNothing()
    {
        CollisionDebugView debug = new(new Camera2D()) { Enabled = false };
        RecordingRenderer renderer = new();

        debug.Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void Enabled_WithNoBodies_DrawsOnlyTheHullRing()
    {
        CollisionDebugView debug = new(new Camera2D())
        {
            Enabled = true,
            HullRadius = 10,
        };
        RecordingRenderer renderer = new();

        debug.Render(renderer);

        Assert.NotEmpty(renderer.Drawn);
        Assert.All(renderer.Drawn, sprite => Assert.Equal(
            renderer.Textures.Load(CollisionDebugView.PixelAssetKey), sprite.Texture));
    }

    [Fact]
    public void ASolidBody_AddsARectangle_ButANonSolidBodyDoesNot()
    {
        CollisionDebugView debug = new(new Camera2D()) { Enabled = true, HullRadius = 10 };
        RecordingRenderer hullOnly = new();
        debug.Render(hullOnly);
        int hullSegments = hullOnly.Drawn.Count;

        debug.Bodies = [Rock("solid-rock", 100, 0, solid: true)];
        RecordingRenderer withSolid = new();
        debug.Render(withSolid);

        Assert.Equal(hullSegments + SegmentsPerObstacle, withSolid.Drawn.Count);

        debug.Bodies = [Rock("solid-rock", 100, 0, solid: true), Rock("decoration", -100, 0, solid: false)];
        RecordingRenderer withDecoration = new();
        debug.Render(withDecoration);

        Assert.Equal(withSolid.Drawn.Count, withDecoration.Drawn.Count);
    }
}
