using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class GameScreenTests
{
    sealed class StubShipView : IShipView
    {
        public int Rendered { get; private set; }

        public ShipPose Pose { get; set; }

        public void Render(IRenderer renderer) => Rendered++;
    }

    [Fact]
    public void Render_DrawsTheShip()
    {
        StubShipView ship = new();
        GameScreen screen = new(new Camera2D(), ship);

        screen.Render(new RecordingRenderer());

        Assert.Equal(1, ship.Rendered);
    }

    [Fact]
    public void Render_PointsTheCameraAtTheShip()
    {
        // the camera follows the ship: a fixed viewport is left behind within seconds
        Camera2D camera = new();
        StubShipView ship = new() { Pose = new ShipPose(new Vector2(120f, -45f), 0f) };
        GameScreen screen = new(camera, ship);

        screen.Render(new RecordingRenderer());

        Assert.Equal(new Vector2(120f, -45f), camera.Target);
    }

    [Fact]
    public void Render_KeepsTheCameraOnTheShip_AsItMoves()
    {
        Camera2D camera = new();
        StubShipView ship = new();
        GameScreen screen = new(camera, ship);

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
        GameScreen screen = new(camera, ship);
        RecordingRenderer renderer = new();

        screen.Render(renderer);

        Assert.Equal(renderer.ViewportCentre, renderer.Single().Position);
    }

    [Fact]
    public void Update_DrawsNothing()
    {
        // update and draw are separate for a reason: a frame the platform does not draw still ticks
        StubShipView ship = new();
        GameScreen screen = new(new Camera2D(), ship);

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(0, ship.Rendered);
    }
}
