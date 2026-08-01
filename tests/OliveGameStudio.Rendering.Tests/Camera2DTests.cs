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

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(float.NegativeInfinity, 0f)]
    public void Target_RefusesAnXThatIsNotFinite(float x, float y)
    {
        // #57: unchecked, this draws every sprite in the game at a NaN position — a blank screen
        // rather than an error, which is the hard failure to diagnose
        Camera2D camera = new();

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => camera.Target = new Vector2(x, y));

        Assert.Contains("Target", error.Message, StringComparison.Ordinal);
        Assert.Contains("X", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0f, float.NaN)]
    [InlineData(0f, float.PositiveInfinity)]
    [InlineData(0f, float.NegativeInfinity)]
    public void Target_RefusesAYThatIsNotFinite(float x, float y)
    {
        Camera2D camera = new();

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => camera.Target = new Vector2(x, y));

        Assert.Contains("Target", error.Message, StringComparison.Ordinal);
        Assert.Contains("Y", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Target_RefusedInAnObjectInitialiserToo()
    {
        // the initialiser goes through the same setter; a camera cannot be born non-finite either
        Assert.Throws<ArgumentException>(
            () => _ = new Camera2D { Target = new Vector2(float.NaN, float.NaN) });
    }

    [Fact]
    public void Target_KeepsTheLastGoodPosition_WhenARefusedOneIsAssigned()
    {
        // the refusal names the frame that produced the bad number and leaves the camera usable,
        // rather than half-assigning a position nothing can be drawn from
        Camera2D camera = new() { Target = new Vector2(120f, -45f) };

        Assert.Throws<ArgumentException>(() => camera.Target = new Vector2(0f, float.NaN));

        Assert.Equal(new Vector2(120f, -45f), camera.Target);
        Assert.Equal(Centre, camera.WorldToScreen(camera.Target, Viewport));
    }

    [Fact]
    public void Target_AcceptsAPositionAsFarOutAsTheWorldGoes()
    {
        // the rule is finiteness, not a bound: the world is unbounded and the player flies on
        Camera2D camera = new() { Target = new Vector2(-96_000f, 250_000f) };

        Assert.Equal(new Vector2(-96_000f, 250_000f), camera.Target);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void PixelsPerUnit_RefusesAZoomThatIsNotFinite(float pixelsPerUnit)
    {
        Camera2D camera = new();

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => camera.PixelsPerUnit = pixelsPerUnit);

        Assert.Contains("PixelsPerUnit", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void PixelsPerUnit_RefusesAZoomThatIsNotPositive(float pixelsPerUnit)
    {
        // a zoom of nothing puts nothing on screen and every tile calculation through a division
        // by zero; a negative one mirrors the world
        Camera2D camera = new();

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => camera.PixelsPerUnit = pixelsPerUnit);

        Assert.Contains("PixelsPerUnit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PixelsPerUnit_KeepsTheLastGoodZoom_WhenARefusedOneIsAssigned()
    {
        Camera2D camera = new() { PixelsPerUnit = 2f };

        Assert.Throws<ArgumentException>(() => camera.PixelsPerUnit = float.NaN);

        Assert.Equal(2f, camera.PixelsPerUnit);
    }

    [Fact]
    public void PixelsPerUnit_AcceptsAZoomWoundRightOut()
    {
        // small is not the same as invalid: the field is documented as clipping at a low enough
        // zoom, which is a limit rather than a mistake, so the setter must not refuse it
        Camera2D camera = new() { PixelsPerUnit = 0.000_01f };

        Assert.Equal(0.000_01f, camera.PixelsPerUnit);
    }

    [Fact]
    public void NoFiniteTarget_AndNoFiniteZoom_CanPutASpriteAtANaNPosition()
    {
        // what the refusals are for, said as the property they buy: with both members held to
        // the contract, nothing that reaches WorldToScreen can come back non-finite
        Camera2D camera = new() { Target = new Vector2(1_000_000f, -1_000_000f), PixelsPerUnit = 0.001f };

        Vector2 screen = camera.WorldToScreen(new Vector2(-500_000f, 750_000f), Viewport);

        Assert.True(float.IsFinite(screen.X) && float.IsFinite(screen.Y), $"drawn at {screen}");
    }
}
