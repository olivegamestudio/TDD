using System.Numerics;
using OliveGameStudio;

namespace OliveGameStudio.Rendering.Tests;

/// <summary>
/// The camera's two halves, held to agreeing with each other.
/// </summary>
/// <remarks>
/// Placing anything in the world takes both — where it is and which way it faces — and they are
/// separate calls, so they can disagree without either looking wrong on its own. A body then lands
/// in exactly the right place pointing the wrong way, which reads as the world spinning rather
/// than as anything to do with rotation. These hold the pair together.
/// </remarks>
public sealed class CameraRotationTransformTests
{
    [Fact]
    public void WithTheCameraUpright_AWorldAngle_IsDrawnUnchanged()
    {
        Camera2D camera = new();

        Assert.Equal(0.75f, camera.WorldToScreenRotation(0.75f), precision: 5);
    }

    [Fact]
    public void TurningTheCamera_TurnsTheWorldTheOtherWay()
    {
        // Which is the whole of what makes the ship appear to point forward: the world comes
        // about, the ship does not.
        Camera2D camera = new() { Orientation = MathF.PI / 2 };

        Assert.Equal(-MathF.PI / 2, camera.WorldToScreenRotation(0), precision: 5);
    }

    [Fact]
    public void ThePositionAndTheFacing_AgreeAboutWhichWayTheCameraTurned()
    {
        // A body dead ahead, with the camera turned a quarter to starboard, moves to the left of
        // the screen — up to left is anticlockwise. A sprite's rotation is clockwise-positive, so
        // the facing has to come back negative by the same quarter turn, or the body is drawn
        // square to a world that has moved out from under it.
        Camera2D camera = new() { Orientation = MathF.PI / 2, PixelsPerUnit = 1f };

        Vector2 placed = camera.WorldToScreen(new Vector2(0, 100), new Vector2(800, 600));

        Assert.Equal(300f, placed.X, precision: 3);   // 400 - 100: left of centre
        Assert.Equal(300f, placed.Y, precision: 3);   // level with it
        Assert.Equal(-MathF.PI / 2, camera.WorldToScreenRotation(0), precision: 5);
    }

    [Fact]
    public void ABodyFacingTheWayTheCameraFaces_IsDrawnUpright()
    {
        // However far the camera has come about, something aligned with it reads as straight
        // ahead — the property the ship relies on to always point up the screen.
        Camera2D camera = new() { Orientation = 2.4f };

        Assert.Equal(0f, camera.WorldToScreenRotation(2.4f), precision: 5);
    }
}
