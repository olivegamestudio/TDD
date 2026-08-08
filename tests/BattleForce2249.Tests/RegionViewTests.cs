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

    /// <summary>
    /// One light, standing where it was asked to stand.
    /// </summary>
    static SceneDefinition OneLight(double x = 0, double y = 0, string layer = "Default") =>
        new("test", [new SceneBody("glow", RegionView.LightSprite, x, y, 0, 1, 1, layer, 0)], []);

    [Fact]
    public void ALight_IsPlacedByItsArtwork_RatherThanByItsFile()
    {
        // The light in glow.png does not sit in the middle of glow.png: there is a stretch of
        // near-empty canvas above it and almost none below, so the lit part is nearer two thirds
        // of the way down. Placed by the file's own middle, every light in the field is drawn
        // below where it was put — which reads as a light resting on something rather than one
        // hanging in space, and is what #188 reported.
        RegionView view = new(Camera()) { Scene = OneLight() };
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(RegionView.LightSprite, 241, 183);

        view.Render(renderer);

        Sprite drawn = renderer.Single();

        Assert.Equal(241f / 2f, drawn.Origin.X, precision: 3);
        Assert.Equal(183f * RegionView.LightArtworkCentreFraction, drawn.Origin.Y, precision: 3);

        // And it is genuinely lower down the texture than the file's middle, which is the whole
        // of the correction — a fraction that drifted back to a half would pass everything above.
        Assert.True(drawn.Origin.Y > 183f / 2f);
    }

    [Fact]
    public void Scenery_ThatIsNotALight_IsStillPlacedByTheMiddleOfItsFile()
    {
        // Only the light has been measured. Everything else keeps the origin it always had, so
        // this correction cannot quietly move the rocks the ship collides with — RegionObstacles
        // seeds those from the body's position, and a sprite drawn off its own collision shape is
        // worse than one drawn off its authored point.
        RegionView view = new(Camera()) { Scene = OneBody(0, 0, 0) };
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize("rock1", 985, 562);

        view.Render(renderer);

        Assert.Equal(new Vector2(985f / 2f, 562f / 2f), renderer.Single().Origin);
    }

    [Fact]
    public void ALight_Flickers_RatherThanBurningFlat()
    {
        RegionView view = new(Camera()) { Scene = OneLight() };

        Assert.NotEqual(BrightnessOf(view, seconds: 0), BrightnessOf(view, seconds: 0.9), precision: 3);
    }

    [Fact]
    public void ALight_DrawnTwiceAtOneInstant_IsDrawnTheSameBothTimes()
    {
        // The flicker is worked out from the time it is handed rather than accumulated as it
        // draws, for the reason an orb's place is: a frame drawn twice has to look the same both
        // times, and a value that ticked inside the draw could not promise that.
        RegionView view = new(Camera()) { Scene = OneLight() };

        Assert.Equal(BrightnessOf(view, seconds: 4.25), BrightnessOf(view, seconds: 4.25));
    }

    [Fact]
    public void TwoLights_AreNotInStepWithEachOther()
    {
        // A field of lights all dipping together is a power cut, not a flicker. The phase comes
        // from where a light stands, so two lights are out of step without anything having to be
        // authored per light — and a light keeps its phase when the region is loaded again.
        RegionView view = new(Camera())
        {
            Scene = new SceneDefinition("test",
            [
                new SceneBody("near", RegionView.LightSprite, 0, 0, 0, 1, 1, "Default", 0),
                new SceneBody("far", RegionView.LightSprite, 130, -95, 0, 1, 1, "Default", 1),
            ], []),
            SecondsElapsed = 1.5,
        };

        RecordingRenderer renderer = new();
        view.Render(renderer);

        Assert.NotEqual(renderer.Drawn[0].Colour.Red, renderer.Drawn[1].Colour.Red, precision: 3);
    }

    [Fact]
    public void AFlickeringLight_NeverOutshinesItsArtwork_NorGoesOut()
    {
        // The bounds are the point of stating the curve as a midpoint and a depth. Brighter than
        // the artwork is a light the author never drew; dark enough to lose is a light that
        // disappears out of a scene the player is navigating by.
        RegionView view = new(Camera()) { Scene = OneLight() };

        for (int step = 0; step <= 400; step++)
        {
            float brightness = BrightnessOf(view, seconds: step * 0.05);

            Assert.InRange(brightness, RegionView.DimmestLight, RegionView.BrightestLight);
        }
    }

    [Fact]
    public void Scenery_ThatIsNotALight_DoesNotFlicker()
    {
        RegionView view = new(Camera()) { Scene = OneBody(0, 0, 0), SecondsElapsed = 3.7 };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(Colour.White, renderer.Single().Colour);
    }

    [Fact]
    public void ALightPaintedOnTheSky_TakesTheTintAndTheFlickerTogether()
    {
        // Nothing authors one today, and the two are independent decisions — how dark this screen
        // draws its sky, and how a light behaves — so they compose rather than one winning.
        RegionView view = new(Camera())
        {
            Scene = new SceneDefinition("test",
            [
                new SceneBody("sky glow", RegionView.LightSprite, 0, 0, 0, 1, 1, "Default", 0, 0),
            ], []),
            BackdropTint = new Colour(0.5f, 0.5f, 0.5f, 1f),
            SecondsElapsed = 2.25,
        };

        RecordingRenderer renderer = new();
        view.Render(renderer);

        Colour drawn = renderer.Single().Colour;

        Assert.Equal(0.5f * RegionView.BrightnessAt(0, 0, 2.25), drawn.Red, precision: 5);
        Assert.Equal(1f, drawn.Alpha);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ATimeTheFlickerCannotBeWorkedOutFrom_IsRefusedWhereItIsSet(double seconds)
    {
        // Refused at the setter for the reason the camera refuses a target that is not finite: the
        // sine of a number that is not one is not a number, every light in the region is then drawn
        // through a colour channel that is not a number, and the failure would surface from inside
        // Colour on some later frame naming a channel rather than the clock that produced it.
        RegionView view = new(Camera());

        Assert.Throws<ArgumentOutOfRangeException>(() => view.SecondsElapsed = seconds);
    }

    /// <summary>
    /// How brightly the one light in a scene is drawn at a given moment.
    /// </summary>
    static float BrightnessOf(RegionView view, double seconds)
    {
        view.SecondsElapsed = seconds;

        RecordingRenderer renderer = new();
        view.Render(renderer);

        return renderer.Single().Colour.Red;
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
