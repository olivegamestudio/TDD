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

    /// <summary>
    /// A turned camera puts a position through a sine and a cosine in <see cref="float"/>, so the
    /// results are compared to well within a pixel rather than exactly. Nothing above the
    /// orientation tests needs this: an upright camera is still exact.
    /// </summary>
    const float Tolerance = 0.001f;

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
    public void ANewCamera_IsUpright()
    {
        // world forward is screen up until somebody says otherwise, which is what every test
        // above this line is written against
        Camera2D camera = new();

        Assert.Equal(0f, camera.Orientation);
    }

    [Fact]
    public void TheOrientation_PutsThatHeading_StraightUpTheScreen()
    {
        // the whole point of the property: turn the camera to a heading and whatever lies along
        // that heading is what the player is looking at the top of the window
        Camera2D camera = new() { Orientation = MathF.PI / 2f };

        // a quarter turn to starboard, so world starboard — positive X — is now dead ahead
        Vector2 ahead = camera.WorldToScreen(new Vector2(100f, 0f), Viewport);

        Assert.Equal(Centre.X, ahead.X, Tolerance);
        Assert.Equal(Centre.Y - 100f, ahead.Y, Tolerance);
    }

    [Fact]
    public void TheOrientation_PutsWhatIsBehind_AtTheBottomOfTheScreen()
    {
        Camera2D camera = new() { Orientation = MathF.PI / 2f };

        Vector2 behind = camera.WorldToScreen(new Vector2(-100f, 0f), Viewport);

        Assert.Equal(Centre.X, behind.X, Tolerance);
        Assert.Equal(Centre.Y + 100f, behind.Y, Tolerance);
    }

    [Fact]
    public void TurningToStarboard_SwingsTheWorldToPort()
    {
        // the sense of the rotation, which is the half of this that can be got backwards without
        // any test above noticing: turn right and the world appears to swing left. Getting it the
        // other way round leaves a ship that banks into its own turn.
        Camera2D camera = new() { Orientation = MathF.PI / 2f };

        // due north, with the camera facing east, is off the ship's port bow — the left of screen
        Vector2 north = camera.WorldToScreen(new Vector2(0f, 100f), Viewport);

        Assert.Equal(Centre.X - 100f, north.X, Tolerance);
        Assert.Equal(Centre.Y, north.Y, Tolerance);
    }

    [Fact]
    public void TheOrientation_TurnsTheWorldAboutTheTarget_NotAboutTheOrigin()
    {
        // what keeps the ship in the middle of the viewport while the world turns around it. A
        // rotation about the world origin instead would fling the ship off screen the moment it
        // turned anywhere but at the origin.
        Camera2D camera = new() { Target = new Vector2(9000f, -4000f), Orientation = 1.1f };

        Assert.Equal(Centre.X, camera.WorldToScreen(camera.Target, Viewport).X, Tolerance);
        Assert.Equal(Centre.Y, camera.WorldToScreen(camera.Target, Viewport).Y, Tolerance);
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(2f)]
    [InlineData(-1.4f)]
    public void TheOrientation_TurnsTheWorld_WithoutStretchingIt(float orientation)
    {
        // a rotation moves a point around the target and never towards or away from it. A matrix
        // written with a cosine where a sine belongs still looks plausible at a quarter turn and
        // squashes the world at every angle in between.
        Camera2D camera = new() { Orientation = orientation };
        Vector2 marker = new(300f, -400f);

        Vector2 screen = camera.WorldToScreen(marker, Viewport);

        Assert.Equal(marker.Length(), Vector2.Distance(screen, Centre), Tolerance);
    }

    [Fact]
    public void AFullTurn_DrawsWhatNoTurnDraws()
    {
        // crossing 0 and 2*pi is ordinary flying, so the angle wraps rather than accumulating
        // into somewhere the world is drawn differently
        Camera2D camera = new();
        Vector2 marker = new(120f, 340f);

        Vector2 upright = camera.WorldToScreen(marker, Viewport);
        camera.Orientation = MathF.Tau;
        Vector2 allTheWayRound = camera.WorldToScreen(marker, Viewport);

        Assert.Equal(upright.X, allTheWayRound.X, Tolerance);
        Assert.Equal(upright.Y, allTheWayRound.Y, Tolerance);
    }

    [Fact]
    public void AnUprightCamera_IsUntouchedByTheTurn()
    {
        // exact, not approximate: an orientation of zero must cost the transform nothing at all,
        // or every position in the game moves by a fraction of a pixel the day this was added
        Camera2D camera = new() { Target = new Vector2(17f, -23f), PixelsPerUnit = 3f };

        // 17 units to starboard of the target and 43 astern of it, at three pixels to the unit
        Assert.Equal(new Vector2(Centre.X + 51f, Centre.Y + 129f), camera.WorldToScreen(new Vector2(34f, -66f), Viewport));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void TheOrientation_RefusesAnAngleThatIsNotFinite(float orientation)
    {
        // the sine of one of these is NaN, which draws every sprite in the world at a position
        // that is nowhere — in full, without an error, to a blank window. Refused where it is
        // written so that the failure names the frame that produced it.
        Camera2D camera = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Orientation = orientation);
    }

    [Fact]
    public void ARefusedOrientation_LeavesTheCameraAsItWas()
    {
        Camera2D camera = new() { Orientation = 0.75f };

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Orientation = float.NaN);

        Assert.Equal(0.75f, camera.Orientation);
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
    [InlineData(0f, float.NaN)]
    [InlineData(float.NaN, float.NaN)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(0f, float.NegativeInfinity)]
    public void TheTarget_RefusesAPositionThatIsNotFinite(float x, float y)
    {
        // Subtracted from every world position that is drawn, so one of these puts the whole
        // world at a position that is nowhere — drawn in full, without an error, to a blank
        // window. Refused where it is written so the failure names the frame that produced it,
        // which is the same call the orientation above already makes.
        Camera2D camera = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Target = new Vector2(x, y));
    }

    [Fact]
    public void ARefusedTarget_LeavesTheCameraAsItWas()
    {
        // still a usable camera afterwards: it holds the last position it was legitimately
        // pointed at rather than being left half assigned
        Camera2D camera = new() { Target = new Vector2(17f, -23f) };

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Target = new Vector2(float.NaN, 0f));

        Assert.Equal(new Vector2(17f, -23f), camera.Target);
    }

    [Fact]
    public void TheTarget_HasNoBoundOnHowFarOutItIs()
    {
        // the world is unbounded and the player flies forward indefinitely, so distance is not
        // what is being refused above — only a number that is not one
        Camera2D camera = new() { Target = new Vector2(float.MaxValue, -float.MaxValue) };

        Assert.Equal(new Vector2(float.MaxValue, -float.MaxValue), camera.Target);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ThePixelsPerUnit_RefusesAZoomThatIsNotFinite(float pixelsPerUnit)
    {
        // multiplied into every world offset, so this scatters the world exactly as a non-finite
        // target does. It was previously left to whichever caller remembered to check, which was
        // one caller, checking with an ordered comparison that answers "fine" for NaN.
        Camera2D camera = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.PixelsPerUnit = pixelsPerUnit);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void ThePixelsPerUnit_RefusesAZoomThatIsNotPositive(float pixelsPerUnit)
    {
        // a zoom of nothing collapses the world to a point, and a negative one turns it inside out
        Camera2D camera = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.PixelsPerUnit = pixelsPerUnit);
    }

    [Fact]
    public void ARefusedZoom_LeavesTheCameraAsItWas()
    {
        Camera2D camera = new() { PixelsPerUnit = 3f };

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.PixelsPerUnit = float.NaN);

        Assert.Equal(3f, camera.PixelsPerUnit);
    }

    [Fact]
    public void ThePixelsPerUnit_KeepsAZoomWoundFarDown()
    {
        // deliberately still allowed: the star field's clipping at a low zoom is a stated limit
        // of the star field, not a mistake for the camera to veto
        Camera2D camera = new() { PixelsPerUnit = 0.000_01f };

        Assert.Equal(0.000_01f, camera.PixelsPerUnit);
    }
}
