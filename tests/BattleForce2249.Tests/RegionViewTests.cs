using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// How a region's scenery is placed on screen. The camera turns the world around the ship, so the
/// interesting thing here is that a body's <em>position</em> and its <em>facing</em> agree about
/// which way that turn went — they are computed in different places and can disagree silently.
/// </summary>
public sealed class RegionViewTests
{
    static SceneDefinition OneBody(double x, double y, double rotationDegrees, double parallax = 1) =>
        new("test", [new SceneBody("body", "rock1", x, y, rotationDegrees, 1, 1, "Environment", 0, parallax)], []);

    static Camera2D Camera() => new() { Target = Vector2.Zero, PixelsPerUnit = 1f };

    [Fact]
    public void AsTheCameraTurns_SceneryTurnsWithTheWorld_NotAgainstIt()
    {
        // The camera swings the world the opposite way to its own heading: turned a quarter to
        // starboard, a body dead ahead appears to the left. A sprite's rotation is clockwise, so
        // staying square with the world means turning anticlockwise by the same angle.
        //
        // Getting the sign wrong here does not look like a rotation bug. It looks like the whole
        // debris field spinning as the ship comes about, which reads as a physics problem.
        Camera2D camera = Camera();
        camera.Orientation = MathF.PI / 2;

        RegionView view = new(camera) { Scene = OneBody(0, 100, 0) };
        RecordingRenderer renderer = new(width: 800, height: 600);

        view.Render(renderer);

        Sprite drawn = renderer.Single();

        // The position says the world turned anticlockwise on screen: straight up became left.
        Assert.Equal(400f - 100f, drawn.Position.X, precision: 3);
        Assert.Equal(300f, drawn.Position.Y, precision: 3);

        // The facing has to say the same thing.
        Assert.Equal(-MathF.PI / 2, drawn.Rotation, precision: 3);
    }

    [Fact]
    public void AnAuthoredAngle_IsMirrored_BecauseTheConventionsTurnOppositeWays()
    {
        // The content was authored where a positive angle turns anticlockwise with Y up; a Sprite
        // turns clockwise, because the screen's Y axis points down.
        RegionView view = new(Camera()) { Scene = OneBody(0, 0, 90) };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(-MathF.PI / 2, renderer.Single().Rotation, precision: 3);
    }

    [Fact]
    public void ABackdrop_DoesNotTurnWithTheCamera_NorMoveWithIt()
    {
        // Painted on the sky. Coming about must not swing it, and flying must not slide it.
        Camera2D camera = Camera();
        camera.Orientation = MathF.PI / 3;
        camera.Target = new Vector2(500, -250);

        RegionView view = new(camera) { Scene = OneBody(0, 0, 0, parallax: 0) };
        RecordingRenderer renderer = new(width: 800, height: 600);

        view.Render(renderer);

        Sprite drawn = renderer.Single();

        Assert.Equal(0f, drawn.Rotation);
        Assert.Equal(new Vector2(400f, 300f), drawn.Position);
    }

    [Fact]
    public void ARegion_IsDrawnAsAuthored_UntilSomethingTintsIt()
    {
        RegionView view = new(Camera())
        {
            Scene = new SceneDefinition("test",
            [
                new SceneBody("sky", "space", 0, 0, 0, 1, 1, "Default", 0, 0),
                new SceneBody("rock", "rock1", 0, 0, 0, 1, 1, "Environment", 0),
            ], []),
        };

        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(2, renderer.Drawn.Count);
        Assert.All(renderer.Drawn, sprite => Assert.Equal(Colour.White, sprite.Colour));
    }

    [Fact]
    public void TheBackdropTakesTheTint_AndTheWorldDoesNot()
    {
        // The sky is scenery the player looks past; the rock is scenery the player flies into.
        // Dimming the second one takes away the warning they get of what they are about to hit,
        // which is why the tint stops at the sky.
        Colour dusk = new(0.5f, 0.5f, 0.5f, 1f);

        RegionView view = new(Camera())
        {
            Scene = new SceneDefinition("test",
            [
                new SceneBody("sky", "space", 0, 0, 0, 1, 1, "Default", 0, 0),
                new SceneBody("rock", "rock1", 0, 0, 0, 1, 1, "Environment", 0),
            ], []),
            BackdropTint = dusk,
        };

        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(dusk, renderer.Drawn[0].Colour);
        Assert.Equal(Colour.White, renderer.Drawn[1].Colour);
    }

    [Fact]
    public void Scenery_IsDrawnFurthestLayerFirst()
    {
        RegionView view = new(Camera())
        {
            Scene = new SceneDefinition("test",
            [
                new SceneBody("front", "rock2", 0, 0, 0, 1, 1, "Characters", 0),
                new SceneBody("back", "rock1", 0, 0, 0, 1, 1, "Parallax", 0),
            ], []),
        };

        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(renderer.Textures.Load("rock1"), renderer.Drawn[0].Texture);
        Assert.Equal(renderer.Textures.Load("rock2"), renderer.Drawn[1].Texture);
    }
}
