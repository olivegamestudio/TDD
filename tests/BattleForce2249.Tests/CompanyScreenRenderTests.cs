using System.Numerics;
using Microsoft.Extensions.Options;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// What the company screen puts on screen. The screen's job is one logo, centred, arriving and
/// leaving softly — so these assert where it lands, how big it is, and that the fade actually
/// reaches both ends rather than merely varying.
/// </summary>
public sealed class CompanyScreenRenderTests
{
    static CompanyScreen Screen(double seconds = 2) =>
        new(Options.Create(new CompanyScreenOptions { Duration = TimeSpan.FromSeconds(seconds) }));

    [Fact]
    public void Render_DrawsTheStudioLogo()
    {
        CompanyScreen screen = Screen();
        RecordingRenderer renderer = new();

        screen.Enter();
        screen.Render(renderer);

        Sprite sprite = renderer.Single();
        Assert.Equal(CompanyScreen.LogoAssetKey, Assert.Single(renderer.Textures.Requested));
        Assert.Equal(renderer.ViewportCentre, sprite.Position);
    }

    [Fact]
    public void Render_PlacesTheLogoOnItsOwnCentre()
    {
        // The origin is what Position places, so a logo meant to sit in the middle of the screen
        // has to be positioned by its own middle. Getting this wrong puts it a half-logo down and
        // right of centre, which looks like a layout bug rather than a wrong origin.
        CompanyScreen screen = Screen();
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(CompanyScreen.LogoAssetKey, 4500, 4500);

        screen.Enter();
        screen.Render(renderer);

        Assert.Equal(new Vector2(2250f, 2250f), renderer.Single().Origin);
    }

    [Fact]
    public void Render_ScalesTheLogoToAShareOfTheWidth()
    {
        CompanyScreen screen = Screen();
        RecordingRenderer renderer = new(width: 1920f, height: 1080f);
        renderer.Textures.SetSize(CompanyScreen.LogoAssetKey, 4500, 4500);

        screen.Enter();
        screen.Render(renderer);

        // A third of 1920 is 640, from a texture 4500 across.
        Assert.Equal(640f / 4500f, renderer.Single().Scale, precision: 5);
    }

    [Fact]
    public void Render_LoadsTheLogoOnce_HoweverManyFramesAreDrawn()
    {
        CompanyScreen screen = Screen();
        RecordingRenderer renderer = new();

        screen.Enter();
        screen.Render(renderer);
        screen.Render(renderer);
        screen.Render(renderer);

        Assert.Single(renderer.Textures.Requested);
    }

    [Fact]
    public void Opacity_StartsAtNothing()
    {
        CompanyScreen screen = Screen();
        screen.Enter();

        Assert.Equal(0f, screen.Opacity);
    }

    [Fact]
    public void Opacity_ReachesFull_ByTheEndOfTheFadeIn()
    {
        // A quarter of two seconds is half a second in.
        CompanyScreen screen = Screen(seconds: 2);
        screen.Enter();

        screen.Update(TimeSpan.FromSeconds(0.5));

        Assert.Equal(1f, screen.Opacity);
    }

    [Fact]
    public void Opacity_HoldsFull_ThroughTheMiddle()
    {
        CompanyScreen screen = Screen(seconds: 2);
        screen.Enter();

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(1f, screen.Opacity);
    }

    [Fact]
    public void Opacity_FallsAway_TowardsTheEnd()
    {
        // Three quarters through: half way down the fade out.
        CompanyScreen screen = Screen(seconds: 2);
        screen.Enter();

        screen.Update(TimeSpan.FromSeconds(1.75));

        Assert.Equal(0.5f, screen.Opacity, precision: 5);
    }

    [Fact]
    public void Opacity_IsNothing_OnceTheScreenHasFinished()
    {
        CompanyScreen screen = Screen(seconds: 2);
        screen.Enter();

        screen.Update(TimeSpan.FromSeconds(2));

        Assert.Equal(0f, screen.Opacity);
    }

    [Fact]
    public void Opacity_DoesNotGoNegative_WhenAFrameOvershootsTheEnd()
    {
        // A long frame — a breakpoint, a stalled load — carries the countdown past zero. The logo
        // has to be invisible, not negatively visible: an alpha below zero is not a fade, it is a
        // number the graphics device clamps while nothing says why it was wrong.
        CompanyScreen screen = Screen(seconds: 2);
        screen.Enter();

        screen.Update(TimeSpan.FromSeconds(30));

        Assert.Equal(0f, screen.Opacity);
    }

    [Fact]
    public void Opacity_IsFull_WhenThereIsNoDurationToFadeAcross()
    {
        // A screen configured to last no time cannot fade. Drawing it invisible would be worse
        // than drawing it plainly, and dividing by its duration would not produce a number at all.
        CompanyScreen screen = Screen(seconds: 0);
        screen.Enter();

        Assert.Equal(1f, screen.Opacity);
    }

    [Fact]
    public void Render_FadesTheLogoByTheOpacity()
    {
        CompanyScreen screen = Screen(seconds: 2);
        RecordingRenderer renderer = new();

        screen.Enter();
        screen.Update(TimeSpan.FromSeconds(0.25));
        screen.Render(renderer);

        Assert.Equal(screen.Opacity, renderer.Single().Colour.Alpha, precision: 5);
        Assert.Equal(0.5f, renderer.Single().Colour.Alpha, precision: 5);
    }
}
