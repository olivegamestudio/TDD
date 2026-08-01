using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class GameScreenTests
{
    sealed class StubShipView : IShipView
    {
        public int Rendered { get; private set; }

        public string AssetKey { get; set; } = ShipView.DefaultAssetKey;

        public ShipPose Pose { get; set; }

        public void Render(IRenderer renderer) => Rendered++;
    }

    static GameScreen ScreenFor(ICamera camera, IShipView ship) => new(camera, ship, new StarField(camera));

    [Fact]
    public void Render_DrawsTheShip()
    {
        StubShipView ship = new();
        GameScreen screen = ScreenFor(new Camera2D(), ship);

        screen.Render(new RecordingRenderer());

        Assert.Equal(1, ship.Rendered);
    }

    [Fact]
    public void Render_PointsTheCameraAtTheShip()
    {
        // the camera follows the ship: a fixed viewport is left behind within seconds
        Camera2D camera = new();
        StubShipView ship = new() { Pose = new ShipPose(new Vector2(120f, -45f), 0f) };
        GameScreen screen = ScreenFor(camera, ship);

        screen.Render(new RecordingRenderer());

        Assert.Equal(new Vector2(120f, -45f), camera.Target);
    }

    [Fact]
    public void Render_KeepsTheCameraOnTheShip_AsItMoves()
    {
        Camera2D camera = new();
        StubShipView ship = new();
        GameScreen screen = ScreenFor(camera, ship);

        ship.Pose = new ShipPose(new Vector2(0f, 500f), 0f);
        screen.Render(new RecordingRenderer());
        ship.Pose = new ShipPose(new Vector2(0f, 1000f), 0f);
        screen.Render(new RecordingRenderer());

        Assert.Equal(new Vector2(0f, 1000f), camera.Target);
    }

    [Fact]
    public void Render_LeavesTheShipInTheMiddleOfTheViewport()
    {
        // the whole point of the camera following: wherever the ship has flown to, it is on
        // screen, and it is in the middle
        Camera2D camera = new();
        ShipView ship = new(camera) { Pose = new ShipPose(new Vector2(-8000f, 250_000f), 1.2f) };
        GameScreen screen = ScreenFor(camera, ship);
        RecordingRenderer renderer = new();

        screen.Render(renderer);

        // last, because the ship is drawn over the stars
        Assert.Equal(renderer.ViewportCentre, renderer.Drawn[^1].Position);
    }

    [Fact]
    public void Render_PutsTheStarsBehindTheShip()
    {
        // sprites stack in the order they are drawn, so the ship goes last. The other way round
        // and the player flies behind their own background.
        Camera2D camera = new();
        ShipView ship = new(camera);
        GameScreen screen = ScreenFor(camera, ship);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.DefaultAssetKey, 512, 512);
        renderer.Textures.SetSize(StarField.AssetKey, 16, 16);

        screen.Render(renderer);

        Assert.True(
            renderer.Drawn.Count > 1,
            "The ship was the only thing drawn — the stars are missing.");
        Assert.Equal(512, renderer.Drawn[^1].Texture.Width);
        Assert.All(renderer.Drawn.SkipLast(1), sprite => Assert.Equal(16, sprite.Texture.Width));
    }

    [Fact]
    public void Render_MovesTheStarsWhenTheShipFlies()
    {
        // the ship holds the middle of the viewport, so the stars are the only thing that can
        // show the player they are moving at all
        Camera2D camera = new();
        StubShipView ship = new();
        GameScreen screen = ScreenFor(camera, ship);
        RecordingRenderer renderer = new();

        screen.Render(renderer);
        Vector2[] standingStill = [.. renderer.Drawn.Select(sprite => sprite.Position)];

        renderer.Clear();
        ship.Pose = new ShipPose(new Vector2(0f, 200f), 0f);
        screen.Render(renderer);
        Vector2[] underWay = [.. renderer.Drawn.Select(sprite => sprite.Position)];

        Assert.NotEmpty(standingStill);
        Assert.NotEqual(standingStill, underWay);
    }

    [Fact]
    public void Update_DrawsNothing()
    {
        // update and draw are separate for a reason: a frame the platform does not draw still ticks
        StubShipView ship = new();
        GameScreen screen = ScreenFor(new Camera2D(), ship);

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(0, ship.Rendered);
    }
}
