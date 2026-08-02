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
    public void DrawsNothing_OnTheSplashAndTheMenu()
    {
        // neither screen draws yet; the host must survive being asked to draw them anyway,
        // because the platform draws every frame from the first one
        IHost game = CreateHost();
        RecordingRenderer renderer = new();

        game.Start();
        game.Draw(renderer);

        Assert.Empty(renderer.Drawn);
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
    /// Asserts the ship reached the screen, and reached it last so it is over the stars rather than
    /// behind one.
    /// </summary>
    static void AssertTheShipIsDrawn(RecordingRenderer renderer)
    {
        Assert.NotEmpty(renderer.Drawn);
        Assert.Equal(512, renderer.Drawn[^1].Texture.Width);
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
