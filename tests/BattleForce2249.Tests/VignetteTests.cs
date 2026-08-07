using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// The overlay that frames the play area. It is one sprite stretched over the whole window, so
/// what there is to get wrong is whether it actually covers the window — a vignette that stops
/// short leaves a bright band down an edge, and one drawn at its own aspect frames a square hole
/// in a wide screen.
/// </summary>
public sealed class VignetteTests
{
    static RecordingRenderer Drawn(float width, float height, int texture = 256)
    {
        RecordingRenderer renderer = new(width, height);
        renderer.Textures.SetSize(Vignette.AssetKey, texture, texture);

        new Vignette().Render(renderer);

        return renderer;
    }

    [Fact]
    public void ItCoversTheWholeViewport_HoweverWideTheWindowIs()
    {
        // A wide window is the case that matters: the mask is authored square, so covering this
        // window means the two axes are scaled by different amounts. Uniform scaling would either
        // leave the sides bare or push the top and bottom of the frame off screen, and both of
        // those are the vignette failing to frame anything.
        RecordingRenderer renderer = Drawn(width: 1920f, height: 1080f);

        Sprite drawn = renderer.Single();

        Assert.Equal(new Vector2(960f, 540f), drawn.Position);
        Assert.Equal(1920f, drawn.Scale * drawn.Stretch.X * 256f, precision: 3);
        Assert.Equal(1080f, drawn.Scale * drawn.Stretch.Y * 256f, precision: 3);
    }

    [Fact]
    public void ItIsPlacedByItsMiddle_SoTheFrameIsCentred()
    {
        RecordingRenderer renderer = Drawn(width: 800f, height: 600f, texture: 64);

        Assert.Equal(new Vector2(32f, 32f), renderer.Single().Origin);
    }

    [Fact]
    public void ItIsDrawnUnturned()
    {
        // The frame belongs to the window, not to the world. Turning it with the camera would
        // sweep the dark corners across the screen as the ship comes about.
        Assert.Equal(0f, Drawn(width: 800f, height: 600f).Single().Rotation);
    }

    [Fact]
    public void ItShowsByAsMuchAsItWasAskedFor()
    {
        Assert.Equal(
            Colour.White.WithAlpha(Vignette.DefaultStrength),
            Drawn(width: 800f, height: 600f).Single().Colour);
    }

    [Fact]
    public void ItsStrengthCanBeWoundDown_WithoutRedrawingTheAsset()
    {
        // The shape of the ramp is in the texture and how much of it shows is here, so tuning how
        // heavy the framing is does not mean regenerating a PNG.
        RecordingRenderer renderer = new(800f, 600f);

        new Vignette { Strength = 0.25f }.Render(renderer);

        Assert.Equal(0.25f, renderer.Single().Colour.Alpha, precision: 3);
    }

    [Theory]
    [InlineData(0f, 600f)]
    [InlineData(800f, 0f)]
    [InlineData(float.NaN, 600f)]
    [InlineData(800f, float.NaN)]
    [InlineData(float.PositiveInfinity, 600f)]
    public void AViewportItCannotBeStretchedAcross_DrawsNothing(float width, float height)
    {
        // Asked as "is this drawable" rather than as a comparison against zero, for the reason the
        // star field asks it that way: every ordered comparison against NaN is false, so the
        // negative form lets through the one value it exists to stop — and a stretch that is not a
        // number reaches the device as a sprite of no size, in full, raising nothing.
        RecordingRenderer renderer = new() { ViewportSize = new Vector2(width, height) };

        new Vignette().Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void ItLoadsItsTextureOnce_HoweverManyFramesAreDrawn()
    {
        RecordingRenderer renderer = new();
        Vignette overlay = new();

        overlay.Render(renderer);
        overlay.Render(renderer);
        overlay.Render(renderer);

        Assert.Single(renderer.Textures.Requested);
    }

    [Fact]
    public void ItIsDrawingOnly_SoNothingBeneathItLosesItsInput()
    {
        // The overlay must not intercept a click or a finger. It cannot: it answers nothing but
        // Render, so there is no seam through which it could take a press from the screen under
        // it. Asserted rather than assumed, because the day it grows a second interface is the day
        // it could start eating input, and that is a bug nobody would go looking for in a
        // drawable.
        Assert.Equal([typeof(IRenderable)], typeof(Vignette).GetInterfaces());
    }
}
