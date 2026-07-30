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
