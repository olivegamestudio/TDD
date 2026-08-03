using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

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
    public void ResolvesTheQuestSystem()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType<GameSession>(provider.GetRequiredService<IGameSession>());
        Assert.IsType<BattleForceCampaign>(provider.GetRequiredService<ICampaign>());
    }

    [Fact]
    public void ResolvesTheCharactersTheGameCanBePlayedAs()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType<BattleForceRoster>(provider.GetRequiredService<ICharacterRoster>());
    }

    [Fact]
    public void TheShipIsNotRegistered_BecauseItBelongsToTheGameRatherThanTheProcess()
    {
        // A ship is transient: one per game, built from the character being played. Registered here
        // it would be a singleton outliving the game it belongs to — and the handling it flew on
        // could be registered to disagree with the hull the player owns, which is exactly the split
        // this replaced.
        using ServiceProvider provider = BuildProvider();

        Assert.Null(provider.GetService<Ship>());
        Assert.Null(provider.GetService<ShipMovement>());
        Assert.Null(provider.GetService<ShipHandling>());
    }

    [Fact]
    public void TheShipTheGameFlies_ComesFromTheCharacterTheSessionIsPlaying()
    {
        using ServiceProvider provider = BuildProvider();

        IGameSession session = provider.GetRequiredService<IGameSession>();

        Assert.Same(session.Character.Ship, session.Ship);
        Assert.Same(session.Character.Template.Ship.Handling, session.Ship.Handling);
        Assert.Same(DisgracedShip.Handling, session.Ship.Handling);
    }

    [Fact]
    public void SharesOneQuestSession_BetweenTheGameScreenAndAnythingElseThatNeedsIt()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IGameSession>(), provider.GetRequiredService<IGameSession>());
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
