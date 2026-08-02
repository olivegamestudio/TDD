using System.Numerics;

namespace OliveGameStudio.Tests;

/// <summary>
/// The camera is the one place the world's axes are reconciled with the screen's, so these
/// tests are mostly about which way up things are. Everything drawn in the world inherits
/// whatever is pinned here.
/// </summary>
public sealed class Camera2DTests
{
    static readonly Vector2 Viewport = new(800f, 600f);

    static readonly Vector2 Centre = new(400f, 300f);

    [Fact]
    public void TheTarget_LandsInTheMiddleOfTheViewport()
    {
        Camera2D camera = new() { Target = new Vector2(1234f, -5678f) };

        Assert.Equal(Centre, camera.WorldToScreen(camera.Target, Viewport));
    }

    [Fact]
    public void TheOrigin_IsTheMiddleOfTheViewport_ForACameraThatHasNotMoved()
    {
        Camera2D camera = new();

        Assert.Equal(Centre, camera.WorldToScreen(Vector2.Zero, Viewport));
    }

    [Fact]
    public void ForwardInTheWorld_IsUpTheScreen()
    {
        // the world's positive Y is forward; the screen's positive Y is down. Getting this
        // backwards flies the ship into the bottom of the window on full thrust.
        Camera2D camera = new();

        Vector2 ahead = camera.WorldToScreen(new Vector2(0f, 100f), Viewport);

        Assert.Equal(Centre.X, ahead.X);
        Assert.Equal(Centre.Y - 100f, ahead.Y);
    }

    [Fact]
    public void AsternInTheWorld_IsDownTheScreen()
    {
        Camera2D camera = new();

        Vector2 behind = camera.WorldToScreen(new Vector2(0f, -100f), Viewport);

        Assert.Equal(Centre.Y + 100f, behind.Y);
    }

    [Fact]
    public void StarboardInTheWorld_IsRightOnTheScreen()
    {
        Camera2D camera = new();

        Vector2 right = camera.WorldToScreen(new Vector2(100f, 0f), Viewport);

        Assert.Equal(Centre.X + 100f, right.X);
        Assert.Equal(Centre.Y, right.Y);
    }

    [Fact]
    public void MovingTheCameraForward_MovesTheWorldDownTheScreen()
    {
        // what makes a chase camera read as motion: the ship holds still, the world slides past
        Camera2D camera = new();
        Vector2 marker = new(0f, 500f);

        Vector2 before = camera.WorldToScreen(marker, Viewport);
        camera.Target = new Vector2(0f, 100f);
        Vector2 after = camera.WorldToScreen(marker, Viewport);

        Assert.Equal(before.Y + 100f, after.Y);
    }

    [Fact]
    public void OnePixelPerUnit_ByDefault()
    {
        Camera2D camera = new();

        Assert.Equal(1f, camera.PixelsPerUnit);
    }

    [Fact]
    public void PixelsPerUnit_ScalesTheDistanceFromTheTarget()
    {
        Camera2D camera = new() { PixelsPerUnit = 2f };

        Vector2 screen = camera.WorldToScreen(new Vector2(10f, 20f), Viewport);

        Assert.Equal(Centre.X + 20f, screen.X);
        Assert.Equal(Centre.Y - 40f, screen.Y);
    }

    [Fact]
    public void PixelsPerUnit_DoesNotMoveTheTarget()
    {
        // zoom pulls the world in around what the camera is looking at, not around the origin
        Camera2D camera = new() { Target = new Vector2(300f, 400f), PixelsPerUnit = 4f };

        Assert.Equal(Centre, camera.WorldToScreen(camera.Target, Viewport));
    }

    [Fact]
    public void TheViewportSize_IsReadPerCall()
    {
        // a desktop window can be resized between frames, so nothing about it is cached
        Camera2D camera = new();

        Assert.Equal(new Vector2(400f, 300f), camera.WorldToScreen(Vector2.Zero, Viewport));
        Assert.Equal(new Vector2(640f, 360f), camera.WorldToScreen(Vector2.Zero, new Vector2(1280f, 720f)));
    }
}
