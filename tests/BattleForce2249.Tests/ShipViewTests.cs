using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// What the ship looks like on screen, asserted as the sprite it draws. These are the tests
/// that would have caught a ship drawn off screen, drawn at the wrong size, or spinning about
/// its corner — the failures that read as "nothing is showing" when the game is run.
/// </summary>
public sealed class ShipViewTests
{
    static ShipView CreateView(out Camera2D camera)
    {
        camera = new Camera2D();
        return new ShipView(camera);
    }

    [Fact]
    public void Render_DrawsTheShip_Once()
    {
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Single(renderer.Drawn);
    }

    [Fact]
    public void Render_DrawsTheShipsOwnSprite()
    {
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal([ShipView.TextureKey], renderer.Textures.Requested);
    }

    [Fact]
    public void Render_LoadsTheTextureOnce_HoweverManyFramesAreDrawn()
    {
        // loading is on first draw, not on construction; it must not also be on every draw
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();

        view.Render(renderer);
        view.Render(renderer);
        view.Render(renderer);

        Assert.Single(renderer.Textures.Requested);
    }

    [Fact]
    public void Render_TurnsTheShipAboutItsMiddle()
    {
        // the origin is what the sprite rotates about. A corner origin swings the ship around
        // the outside of the screen instead of turning it on the spot.
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.TextureKey, 1024, 512);

        view.Render(renderer);

        Assert.Equal(new Vector2(512f, 256f), renderer.Single().Origin);
    }

    [Fact]
    public void Render_PutsTheShipWhereTheCameraSaysItIs()
    {
        ShipView view = CreateView(out Camera2D camera);
        RecordingRenderer renderer = new();
        view.Pose = new ShipPose(new Vector2(40f, 90f), 0f);

        view.Render(renderer);

        // the camera is looking at the origin here, so the ship draws 40 right and 90 up
        Assert.Equal(renderer.ViewportCentre + new Vector2(40f, -90f), renderer.Single().Position);
        Assert.Equal(Vector2.Zero, camera.Target);              // the view does not move the camera
    }

    [Fact]
    public void Render_DrawsTheShipInTheMiddle_WhenTheCameraIsOnIt()
    {
        ShipView view = CreateView(out Camera2D camera);
        RecordingRenderer renderer = new();
        view.Pose = new ShipPose(new Vector2(-3000f, 12_000f), 0f);
        camera.Target = view.Pose.Position;

        view.Render(renderer);

        Assert.Equal(renderer.ViewportCentre, renderer.Single().Position);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(-2.25f)]
    [InlineData(6.28f)]
    public void Render_PointsTheSpriteAlongTheHeading(float heading)
    {
        // the artwork faces up, which is world forward, so a heading passes straight through —
        // a ship flying sideways while its sprite faces up reads as broken immediately
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();
        view.Pose = new ShipPose(Vector2.Zero, heading);

        view.Render(renderer);

        Assert.Equal(heading, renderer.Single().Rotation);
    }

    [Fact]
    public void Render_SizesTheShipInWorldUnits_NotInTexturePixels()
    {
        // the sprite is 1024 pixels tall and the ship is 96 units long; drawing the texture at
        // its own size would fill the window with one ship
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.TextureKey, 1024, 1024);

        view.Render(renderer);

        Sprite sprite = renderer.Single();
        Assert.Equal(ShipView.LengthInWorldUnits, sprite.Scale * sprite.Texture.Height, 4);
    }

    [Fact]
    public void Render_SizesTheShipFromTheTexture_SoANewSpriteNeedsNoNewNumber()
    {
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.TextureKey, 256, 256);

        view.Render(renderer);

        Sprite sprite = renderer.Single();
        Assert.Equal(ShipView.LengthInWorldUnits, sprite.Scale * sprite.Texture.Height, 4);
    }

    [Fact]
    public void Render_GrowsTheShipWithTheZoom()
    {
        // the ship is a fixed size in the world, so zooming in makes it bigger on screen
        ShipView view = CreateView(out Camera2D camera);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.TextureKey, 1024, 1024);
        camera.PixelsPerUnit = 3f;

        view.Render(renderer);

        Sprite sprite = renderer.Single();
        Assert.Equal(ShipView.LengthInWorldUnits * 3f, sprite.Scale * sprite.Texture.Height, 4);
    }

    [Fact]
    public void Pose_StaysWhereItWasPut_UntilSomethingMovesIt()
    {
        // the view holds the pose; whatever flies the ship writes it once a frame
        ShipView view = CreateView(out _);
        ShipPose pose = new(new Vector2(7f, 8f), 0.5f);

        view.Pose = pose;

        Assert.Equal(pose, view.Pose);
    }

    [Fact]
    public void ANewShip_StartsAtTheOrigin_PointingForward()
    {
        ShipView view = CreateView(out _);

        Assert.Equal(Vector2.Zero, view.Pose.Position);
        Assert.Equal(0f, view.Pose.Heading);
    }
}
