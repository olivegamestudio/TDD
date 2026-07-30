using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the options pattern end to end: configured values must reach the objects that use
/// them, not merely bind.
/// </summary>
public sealed class OptionsTests : HostTestBase
{
    static ServiceProvider BuildProvider(
        Action<CompanyScreenOptions>? configure = null,
        Action<IServiceCollection>? configureServices = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce(configure);

        // after AddBattleForce: the engine uses AddSingleton, so the last registration wins
        configureServices?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void Duration_DefaultsToTwoSeconds()
    {
        using ServiceProvider provider = BuildProvider();

        CompanyScreenOptions options = provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(2), options.Duration);
    }

    [Fact]
    public void ConfiguredDuration_ShortensTheSplash()
    {
        // the value must reach CompanyScreen, not just sit in the options object
        Configure(options => options.Duration = TimeSpan.FromSeconds(10));

        IHost host = CreateHost();

        host.Start();
        host.Update(TimeSpan.FromSeconds(5));
        Assert.IsType<CompanyScreen>(ScreenDirector.Current);   // 5s of a 10s splash

        host.Update(TimeSpan.FromSeconds(5));
        Assert.IsType<MenuScreen>(ScreenDirector.Current);       // now elapsed
    }

    [Fact]
    public void ZeroDuration_SkipsStraightToTheMenu()
    {
        // the debug case: a developer who does not want to sit through the splash
        Configure(options => options.Duration = TimeSpan.Zero);

        IHost host = CreateHost();

        host.Start();
        host.Update(TimeSpan.FromMilliseconds(16));

        Assert.IsType<MenuScreen>(ScreenDirector.Current);
    }

    [Fact]
    public void FrameTimeController_StartsAtRealTime()
    {
        // the scale is runtime state, so nothing configures it away from real time at startup
        using ServiceProvider provider = BuildProvider(
            configureServices: services => services.AddSingleton<IFrameTimeController, ScaledFrameTimeController>());

        var frameTime = (ScaledFrameTimeController)provider.GetRequiredService<IFrameTimeController>();

        Assert.Equal(1, frameTime.TimeScale);
        Assert.Equal(TimeSpan.FromMilliseconds(16), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void NegativeDuration_IsRejectedOnResolve()
    {
        using ServiceProvider provider = BuildProvider(
            configure: options => options.Duration = TimeSpan.FromSeconds(-1));

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value);
    }
}
