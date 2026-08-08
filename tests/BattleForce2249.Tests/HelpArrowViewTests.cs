using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers <see cref="HelpArrowView"/>: it draws exactly the bodies on the <c>"UI"</c> layer, at
/// the position and rotation <see cref="RegionView"/> would draw them at, and fades each one out
/// as the ship gets close to it rather than drawing it at a fixed strength regardless of where
/// the player has actually flown to.
/// </summary>
public sealed class HelpArrowViewTests
{
    static SceneBody Arrow(double x, double y, double rotationDegrees = 0, string layer = HelpArrowView.Layer) =>
        new("Arrow", "Icon_Example02", x, y, rotationDegrees, 15, 15, layer, 0);

    static Camera2D Camera() => new() { Target = Vector2.Zero, PixelsPerUnit = 1f };

    [Fact]
    public void ABodyOnTheUILayer_IsDrawn()
    {
        HelpArrowView view = new(Camera()) { Bodies = [Arrow(0, 200)] };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Single(renderer.Drawn);
    }

    [Fact]
    public void ABodyOnAnyOtherLayer_IsNotDrawn()
    {
        // Everything that is not an arrow is RegionView's to draw, not this view's — see both
        // types' remarks on why the two never overlap.
        HelpArrowView view = new(Camera()) { Bodies = [Arrow(0, 200, layer: "Environment")] };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void FarFromTheShip_AnArrowIsDrawnAtFullStrength()
    {
        HelpArrowView view = new(Camera())
        {
            Bodies = [Arrow(0, 200)],
            ShipPosition = new Vector2(0, 200 - (float)HelpArrowView.FullyVisibleDistance),
        };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(1f, renderer.Single().Colour.Alpha);
    }

    [Fact]
    public void RightOnTopOfTheShip_AnArrowIsNotDrawnAtAll()
    {
        // It has done its job of pointing the way to here — drawing it at alpha zero would still
        // cost a draw call for a picture nobody can see.
        HelpArrowView view = new(Camera())
        {
            Bodies = [Arrow(0, 200)],
            ShipPosition = new Vector2(0, 200),
        };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void BetweenTheTwoDistances_AnArrowFadesInStraightLine()
    {
        double midpoint = (HelpArrowView.FullyVisibleDistance + HelpArrowView.HiddenDistance) / 2;

        HelpArrowView view = new(Camera())
        {
            Bodies = [Arrow(0, 200)],
            ShipPosition = new Vector2(0, 200 - (float)midpoint),
        };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(0.5f, renderer.Single().Colour.Alpha, 3);
    }

    [Fact]
    public void ClosingIn_FadesTheArrowOut_RatherThanLatchingItGoneForever()
    {
        // Proximity this frame, not a flag that remembers a player flew past once — turning round
        // and flying away has to bring the arrow back exactly as it would on a first approach.
        HelpArrowView view = new(Camera()) { Bodies = [Arrow(0, 200)] };
        RecordingRenderer renderer = new();

        view.ShipPosition = new Vector2(0, 200 - (float)HelpArrowView.FullyVisibleDistance);
        view.Render(renderer);
        float far = renderer.Single().Colour.Alpha;
        renderer.Clear();

        view.ShipPosition = new Vector2(0, 200);
        view.Render(renderer);
        Assert.Empty(renderer.Drawn);

        view.ShipPosition = new Vector2(0, 200 - (float)HelpArrowView.FullyVisibleDistance);
        view.Render(renderer);
        Assert.Equal(far, renderer.Single().Colour.Alpha);
    }

    [Fact]
    public void AnArrow_IsPlacedAndTurned_TheSameWayRegionViewWouldDrawIt()
    {
        // The two never draw the same body, but a player has to be unable to tell the difference —
        // an arrow that snapped to a different spot or angle than the scenery around it the moment
        // it was handed to this view instead would read as a rendering bug, not as a feature.
        Camera2D camera = Camera();
        camera.Orientation = MathF.PI / 4;

        HelpArrowView helpArrows = new(camera)
        {
            Bodies = [Arrow(10, 30, rotationDegrees: 45)],
            ShipPosition = new Vector2(10, 30 - (float)HelpArrowView.FullyVisibleDistance),
        };
        RegionView region = new(camera)
        {
            Scene = new SceneDefinition("test", [Arrow(10, 30, rotationDegrees: 45, layer: "Environment")], []),
        };

        RecordingRenderer arrowRenderer = new();
        RecordingRenderer regionRenderer = new();

        helpArrows.Render(arrowRenderer);
        region.Render(regionRenderer);

        Sprite arrow = arrowRenderer.Single();
        Sprite scenery = regionRenderer.Single();

        Assert.Equal(scenery.Position.X, arrow.Position.X, 3);
        Assert.Equal(scenery.Position.Y, arrow.Position.Y, 3);
        Assert.Equal(scenery.Rotation, arrow.Rotation, 3);
        Assert.Equal(scenery.Scale, arrow.Scale, 3);
    }

    [Fact]
    public void AnArrow_IsSizedFromItsAuthoredScale_AndTheCamerasZoom()
    {
        Camera2D camera = Camera();
        camera.PixelsPerUnit = 3f;

        HelpArrowView view = new(camera)
        {
            Bodies = [Arrow(0, 200)],
            ShipPosition = new Vector2(0, 200 - (float)HelpArrowView.FullyVisibleDistance),
        };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(15f * 3f / RegionView.AuthoredPixelsPerUnit, renderer.Single().Scale, 5);
    }

    [Fact]
    public void Render_LoadsTheTextureOnce_HoweverManyArrowsShareIt()
    {
        HelpArrowView view = new(Camera())
        {
            Bodies = [Arrow(0, 200), Arrow(50, 260), Arrow(90, 300)],
            ShipPosition = new Vector2(0, 200 - (float)HelpArrowView.FullyVisibleDistance),
        };
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(1, renderer.Textures.Requested.Count(key => key == "Icon_Example02"));
    }

    [Fact]
    public void NoBodies_DrawsNothing()
    {
        HelpArrowView view = new(Camera());
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Empty(renderer.Drawn);
    }
}
