using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// The ship's engine glow, held to being part of the ship rather than part of the place it is
/// flying through.
/// </summary>
/// <remarks>
/// The glow shipped as scenery: six <c>glow</c> bodies authored into the debris field at the
/// world origin, which is where the ship starts. Standing still it looked correct, and that is
/// the whole difficulty — the fault only appears once the player flies, and what they see is the
/// ship leaving its own engines behind at the spawn point. These are the tests that fail if it
/// ever goes back to being drawn at a fixed place in the world.
/// </remarks>
public sealed class ShipEngineGlowTests
{
    static ShipView CreateView(out Camera2D camera)
    {
        camera = new Camera2D();
        return new ShipView(camera);
    }

    /// <summary>
    /// What the ship drew before the hull — one sprite per engine glow, in the order they are
    /// declared.
    /// </summary>
    static IReadOnlyList<Sprite> Glows(RecordingRenderer renderer) =>
        [.. renderer.Drawn.SkipLast(1)];

    [Fact]
    public void TheShip_HasEnginesToGlow()
    {
        // Guards every test below from passing because there was nothing to draw.
        Assert.NotEmpty(ShipView.EngineGlows);
    }

    [Fact]
    public void Render_DrawsAGlowForEveryEngine_BeneathTheHull()
    {
        // Beneath, because the glow is light spilling out from under the ship rather than a
        // decal on top of it — drawn after the hull it would wash the artwork out.
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();

        // Sized apart so the two can be told from each other in what was drawn.
        renderer.Textures.SetSize(ShipView.EngineGlowAssetKey, 16, 16);
        renderer.Textures.SetSize(ShipView.DefaultAssetKey, 512, 512);

        view.Render(renderer);

        Assert.Equal(ShipView.EngineGlows.Count + 1, renderer.Drawn.Count);
        Assert.All(Glows(renderer), glow => Assert.Equal(16, glow.Texture.Width));
        Assert.Equal(512, renderer.Drawn[^1].Texture.Width);
    }

    [Fact]
    public void Render_LoadsTheGlowOnce_HoweverManyFramesAreDrawn()
    {
        // One texture for every engine, loaded on the first draw like the hull's. Loading it per
        // glow per frame is six loads a frame for a picture that never changes.
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();

        view.Render(renderer);
        view.Render(renderer);

        Assert.Equal(
            1,
            renderer.Textures.Requested.Count(key => key == ShipView.EngineGlowAssetKey));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(-2.25f)]
    [InlineData(3f)]
    public void Render_KeepsEveryGlowOnTheShip_AtEveryHeading(float heading)
    {
        // The bug, stated as an assertion. A camera following the ship holds the ship in the
        // middle of the window, so every glow has to be drawn at its own fixed place around that
        // middle — at any heading, from anywhere in the world. A glow left in the world drifts
        // off the middle the moment the ship moves away from wherever it was authored.
        ShipView view = CreateView(out Camera2D camera);
        RecordingRenderer renderer = new();
        view.Pose = new ShipPose(new Vector2(-4000f, 9000f), heading);
        camera.Target = view.Pose.Position;
        camera.Orientation = heading;
        camera.PixelsPerUnit = 2f;

        view.Render(renderer);

        IReadOnlyList<Sprite> glows = Glows(renderer);

        for (int engine = 0; engine < ShipView.EngineGlows.Count; engine++)
        {
            Vector2 offset = ShipView.EngineGlows[engine].Offset;

            // Y is negated because world forward is screen up — the one conversion the camera
            // performs and this has to expect.
            Vector2 expected = renderer.ViewportCentre + (new Vector2(offset.X, -offset.Y) * 2f);

            Assert.Equal(expected.X, glows[engine].Position.X, 2);
            Assert.Equal(expected.Y, glows[engine].Position.Y, 2);
        }
    }

    [Fact]
    public void Render_CarriesTheGlowsRoundWithTheHeading()
    {
        // Turning the ship has to turn where its engines are, not only which way they point. An
        // offset added in world axes puts the port engine off the starboard bow as soon as the
        // ship comes about.
        ShipView view = CreateView(out Camera2D camera);
        RecordingRenderer renderer = new();

        // A quarter turn to starboard: the ship's forward is now the world's +X, and what sits
        // to starboard of it is the world's -Y.
        view.Pose = new ShipPose(Vector2.Zero, MathF.PI / 2f);

        view.Render(renderer);

        IReadOnlyList<Sprite> glows = Glows(renderer);

        for (int engine = 0; engine < ShipView.EngineGlows.Count; engine++)
        {
            Vector2 offset = ShipView.EngineGlows[engine].Offset;
            Vector2 world = new(offset.Y, -offset.X);
            Vector2 expected = camera.WorldToScreen(world, renderer.ViewportSize);

            Assert.Equal(expected.X, glows[engine].Position.X, 2);
            Assert.Equal(expected.Y, glows[engine].Position.Y, 2);
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(-2.25f)]
    public void Render_TurnsEveryGlowWithTheShip(float heading)
    {
        // The other half of being parented: a glow keeps its own angle relative to the hull, so
        // the plume that trails aft goes on trailing aft through a turn.
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();
        view.Pose = new ShipPose(Vector2.Zero, heading);

        view.Render(renderer);

        IReadOnlyList<Sprite> glows = Glows(renderer);

        for (int engine = 0; engine < ShipView.EngineGlows.Count; engine++)
        {
            // Negated for the same reason the scenery's is: the content turns anticlockwise and
            // a sprite turns clockwise.
            float own = -ShipView.EngineGlows[engine].RotationDegrees * MathF.PI / 180f;

            Assert.Equal(heading + own, glows[engine].Rotation, 4);
        }
    }

    [Fact]
    public void Render_TurnsEveryGlowAboutItsOwnMiddle()
    {
        // A corner origin swings a glow out from under the ship as the ship turns, which reads as
        // the engines coming loose.
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.EngineGlowAssetKey, 240, 180);

        view.Render(renderer);

        Assert.All(Glows(renderer), glow => Assert.Equal(new Vector2(120f, 90f), glow.Origin));
    }

    [Fact]
    public void Render_SizesTheGlowsInWorldUnits_NotInTexturePixels()
    {
        // Sized against the world the ship flies through, exactly as the hull is, so a glow keeps
        // its size relative to the ship rather than to the window.
        ShipView view = CreateView(out Camera2D camera);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.EngineGlowAssetKey, 200, 100);
        camera.PixelsPerUnit = 1f;

        view.Render(renderer);

        IReadOnlyList<Sprite> glows = Glows(renderer);

        for (int engine = 0; engine < ShipView.EngineGlows.Count; engine++)
        {
            ShipEngineGlow glow = ShipView.EngineGlows[engine];
            Sprite drawn = glows[engine];

            Assert.Equal(
                glow.ScaleAcross * 200f / RegionView.AuthoredPixelsPerUnit,
                drawn.Scale * drawn.Stretch.X * 200f,
                3);

            Assert.Equal(
                glow.ScaleAlong * 100f / RegionView.AuthoredPixelsPerUnit,
                drawn.Scale * drawn.Stretch.Y * 100f,
                3);
        }
    }

    [Fact]
    public void Render_GrowsTheGlowsWithTheZoom()
    {
        // The glow is a fixed size in the world, so zooming in makes it bigger on screen — the
        // same rule the hull follows, or the two come apart as the camera moves.
        ShipView view = CreateView(out Camera2D camera);
        RecordingRenderer close = new();
        RecordingRenderer far = new();

        camera.PixelsPerUnit = 4f;
        view.Render(close);

        camera.PixelsPerUnit = 1f;
        view.Render(far);

        for (int engine = 0; engine < ShipView.EngineGlows.Count; engine++)
        {
            Assert.Equal(4f * Glows(far)[engine].Scale, Glows(close)[engine].Scale, 4);
        }
    }

    [Fact]
    public void Render_DrawsTheGlowsOpaqueWhite()
    {
        // Nothing tints the glow; it is drawn exactly as it was authored, and a drawable that
        // quietly picked up a colour would be the wrong shade with nothing saying so.
        ShipView view = CreateView(out _);
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.All(Glows(renderer), glow => Assert.Equal(Colour.White, glow.Colour));
    }
}
