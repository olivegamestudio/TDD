using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class BattleForceHostTests : HostTestBase
{
    [Fact]
    public void Boots_OnCompany_ThenReachesTheMenu()
    {
        IHost game = CreateHost();

        game.Start();
        Assert.IsType<CompanyScreen>(ScreenDirector.Current);

        game.Update(TimeSpan.FromDays(1));
        Assert.IsType<MenuScreen>(ScreenDirector.Current);
    }

    [Fact]
    public void DoesNotAdvanceScreens_WhilePaused()
    {
        IHost game = CreateHost();
        FrameTime.TimeScale = 0;

        game.Start();
        Assert.IsType<CompanyScreen>(ScreenDirector.Current);

        // a frame long enough to elapse the company screen many times over
        game.Update(TimeSpan.FromDays(1));

        Assert.IsType<CompanyScreen>(ScreenDirector.Current);   // still held on the splash
    }

    [Fact]
    public void HoldsScreens_UnderAPausableController()
    {
        // the host is agnostic about which variant filters its frame time
        PausableFrameTimeController frameTime = new();
        Configure(services: services => services.AddSingleton<IFrameTimeController>(frameTime));

        IHost game = CreateHost();

        game.Start();
        frameTime.Pause();
        game.Update(TimeSpan.FromDays(1));
        Assert.IsType<CompanyScreen>(ScreenDirector.Current);

        frameTime.Resume();
        game.Update(TimeSpan.FromDays(1));
        Assert.IsType<MenuScreen>(ScreenDirector.Current);
    }

    [Fact]
    public void DrawsTheStudioLogo_OnTheSplash()
    {
        // end to end through the real composition, as the ship test below is: host, director,
        // screen, renderer. This used to assert that nothing was drawn at all, which was true of
        // a build whose first two screens were black — the splash now has a logo to put up, so
        // what the test pins is which one and that the host reaches it.
        IHost game = CreateHost();
        RecordingRenderer renderer = new();

        game.Start();
        game.Draw(renderer);

        Assert.Equal(CompanyScreen.LogoAssetKey, Assert.Single(renderer.Textures.Requested));
    }

    [Fact]
    public void DrawsTheMenu_OnceTheMenuIsCurrent()
    {
        IHost game = CreateHost();
        RecordingRenderer renderer = new();

        game.Start();
        game.Update(TimeSpan.FromDays(1));   // past the splash
        Assert.IsType<MenuScreen>(ScreenDirector.Current);

        game.Draw(renderer);

        Assert.Contains(MenuScreen.TitleAssetKey, renderer.Textures.Requested);
        Assert.Contains(MenuScreen.StartButtonAssetKey, renderer.Textures.Requested);
    }

    [Fact]
    public void DrawsTheShip_OnceTheGameScreenIsCurrent()
    {
        // end to end through the real composition: host, director, screen, ship, renderer
        IHost game = CreateHost();
        RecordingRenderer renderer = new();

        game.Start();
        ScreenDirector.NavigateTo(Resolve<IGameScreen>());
        game.Draw(renderer);

        // the stars are asked for too, and first, because they are drawn behind the ship
        Assert.Contains(ShipView.DefaultAssetKey, renderer.Textures.Requested);
    }

    [Fact]
    public void KeepsDrawing_FrameAfterFrame()
    {
        // a sprite drawn once and then dropped is a ship that flickers and vanishes
        IHost game = CreateHost();
        RecordingRenderer renderer = new();
        MarkTheShip(renderer);

        game.Start();
        ScreenDirector.NavigateTo(Resolve<IGameScreen>());
        game.Draw(renderer);
        renderer.Clear();
        game.Draw(renderer);

        AssertTheShipIsDrawn(renderer);
    }

    [Fact]
    public void PausingTheGame_DoesNotStopItBeingDrawn()
    {
        // the frame time controller filters updates, not frames: a paused game is still on screen
        IHost game = CreateHost();
        RecordingRenderer renderer = new();
        MarkTheShip(renderer);
        FrameTime.TimeScale = 0;

        game.Start();
        ScreenDirector.NavigateTo(Resolve<IGameScreen>());
        game.Update(TimeSpan.FromSeconds(1));
        game.Draw(renderer);

        AssertTheShipIsDrawn(renderer);
    }

    /// <summary>
    /// Gives the ship a texture size nothing else uses, so the ship can be picked out of a frame
    /// that now holds a star field as well.
    /// </summary>
    static void MarkTheShip(RecordingRenderer renderer)
    {
        renderer.Textures.SetSize(ShipView.DefaultAssetKey, 512, 512);
        renderer.Textures.SetSize(StarField.AssetKey, 16, 16);
    }

    /// <summary>
    /// Asserts the ship reached the screen, and reached it over the stars rather than behind one.
    /// </summary>
    /// <remarks>
    /// Found by its texture rather than assumed to sit at a fixed offset from the end: the frame
    /// the play area is vignetted with draws over everything including the ship, and the
    /// collision debug overlay draws over the frame — both legitimately drawn after it, and
    /// neither this test's concern. What it protects is that the ship is drawn at all, and after
    /// the stars rather than before them.
    /// </remarks>
    static void AssertTheShipIsDrawn(RecordingRenderer renderer)
    {
        int shipIndex = renderer.Drawn.ToList().FindIndex(sprite => sprite.Texture.Width == 512);
        int starIndex = renderer.Drawn.ToList().FindIndex(sprite => sprite.Texture.Width == 16);

        Assert.True(shipIndex >= 0, "the ship was never drawn");
        Assert.True(shipIndex > starIndex, "the ship was drawn before the stars behind it");
    }

    [Fact]
    public void ResumesAdvancingScreens_WhenUnpaused()
    {
        IHost game = CreateHost();
        FrameTime.TimeScale = 0;

        game.Start();
        game.Update(TimeSpan.FromDays(1));
        Assert.IsType<CompanyScreen>(ScreenDirector.Current);

        FrameTime.TimeScale = 1;
        game.Update(TimeSpan.FromDays(1));

        Assert.IsType<MenuScreen>(ScreenDirector.Current);
    }
}
