using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the composition root. Without these, a missing or miswired registration only
/// surfaces when the real game is launched.
/// </summary>
public sealed class ServiceRegistrationTests
{
    static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce();

        // after AddBattleForce: the engine uses AddSingleton, so the last registration wins
        configure?.Invoke(services);

        // the same validation the composition root should fail on, not the game window
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void ResolvesTheHost_WithEveryDependencySatisfied()
    {
        using ServiceProvider provider = BuildProvider();

        IHost host = provider.GetRequiredService<IHost>();

        Assert.IsType<BattleForceHost>(host);
    }

    [Fact]
    public void ResolvesTheEngineServices()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType<LifecycleScreenDirector>(provider.GetRequiredService<IScreenDirector>());
        Assert.IsType<UIController>(provider.GetRequiredService<IUIController>());
        Assert.IsType<LocalSaveProgressService>(provider.GetRequiredService<ISaveProgressService>());
    }

    [Fact]
    public void ResolvesTheDrawingServices()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType<Camera2D>(provider.GetRequiredService<ICamera>());
        Assert.IsType<ShipView>(provider.GetRequiredService<IShipView>());
    }

    [Fact]
    public void SharesOneCamera_BetweenTheShipAndTheScreenThatFollowsIt()
    {
        // two cameras would draw the ship in one place and everything else relative to another
        using ServiceProvider provider = BuildProvider();

        ICamera camera = provider.GetRequiredService<ICamera>();
        IShipView ship = provider.GetRequiredService<IShipView>();
        var screen = (GameScreen)provider.GetRequiredService<IGameScreen>();

        ship.Pose = new ShipPose(new Vector2(500f, 600f), 0f);
        screen.Render(new RecordingRenderer());

        Assert.Equal(new Vector2(500f, 600f), camera.Target);
    }

    [Fact]
    public void SharesOneShip_BetweenWhateverFliesItAndWhatDrawsIt()
    {
        // the logic side sets a pose on the ship it resolves; it has to be the one on screen
        using ServiceProvider provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IShipView>(), provider.GetRequiredService<IShipView>());
    }

    [Fact]
    public void DefaultsToThePassThroughFrameTimeController()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType<PassThroughFrameTimeController>(provider.GetRequiredService<IFrameTimeController>());
    }

    [Fact]
    public void HonoursAFrameTimeControllerRegisteredByTheHostApplication()
    {
        // the debug/release swap: register after AddBattleForce and the last one wins
        using ServiceProvider provider = BuildProvider(services =>
            services.AddSingleton<IFrameTimeController, ScaledFrameTimeController>());

        Assert.IsType<ScaledFrameTimeController>(provider.GetRequiredService<IFrameTimeController>());
    }

    [Fact]
    public void GivesTheHostTheOverriddenFrameTimeController()
    {
        // resolving the override is not enough; the host must actually be built with it
        using ServiceProvider provider = BuildProvider(services =>
            services.AddSingleton<IFrameTimeController, ScaledFrameTimeController>());

        var frameTime = (ScaledFrameTimeController)provider.GetRequiredService<IFrameTimeController>();
        frameTime.TimeScale = 0;

        IHost host = provider.GetRequiredService<IHost>();
        host.Start();
        host.Update(TimeSpan.FromDays(1));

        IScreenDirector director = provider.GetRequiredService<IScreenDirector>();
        Assert.IsType<CompanyScreen>(director.Current);      // frozen by the injected controller
    }

    [Fact]
    public void SharesOneInstancePerService()
    {
        // the host and the game must observe the same director and frame time controller
        using ServiceProvider provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IScreenDirector>(), provider.GetRequiredService<IScreenDirector>());
        Assert.Same(provider.GetRequiredService<IFrameTimeController>(), provider.GetRequiredService<IFrameTimeController>());
        Assert.Same(provider.GetRequiredService<IHost>(), provider.GetRequiredService<IHost>());
    }
}
