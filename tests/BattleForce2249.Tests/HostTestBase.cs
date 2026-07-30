using Microsoft.Extensions.DependencyInjection;
using Moq;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Builds the host through <see cref="BattleForceServiceCollectionExtensions.AddBattleForce"/>
/// rather than by hand, so these tests exercise the same composition the game ships with.
/// Test doubles and non-default options are registered after it, which is what wins under the
/// engine's <c>AddSingleton</c> registrations.
/// </summary>
public abstract class HostTestBase : IDisposable
{
    readonly Lazy<ServiceProvider> _provider;

    Action<CompanyScreenOptions>? _configureOptions;
    Action<IServiceCollection>? _configureServices;

    protected HostTestBase()
    {
        _provider = new Lazy<ServiceProvider>(BuildProvider);
    }

    protected IScreenDirector ScreenDirector => Resolve<IScreenDirector>();

    protected ScaledFrameTimeController FrameTime => (ScaledFrameTimeController)Resolve<IFrameTimeController>();

    ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();

        services.AddBattleForce(_configureOptions);

        // after AddBattleForce, so these win over the engine defaults
        services.AddSingleton<IUIController>(Mock.Of<IUIController>());
        services.AddSingleton<ISaveProgressService>(Mock.Of<ISaveProgressService>());
        services.AddSingleton<IScreenDirector, ScreenDirector>();
        services.AddSingleton<IFrameTimeController, ScaledFrameTimeController>();

        _configureServices?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    T Resolve<T>() where T : notnull => _provider.Value.GetRequiredService<T>();

    /// <summary>
    /// Configures the host before it is built. Must be called before the first resolve.
    /// </summary>
    protected void Configure(
        Action<CompanyScreenOptions>? options = null,
        Action<IServiceCollection>? services = null)
    {
        if (_provider.IsValueCreated)
        {
            throw new InvalidOperationException("Configure must be called before the host is built.");
        }

        _configureOptions = options;
        _configureServices = services;
    }

    /// <summary>
    /// Resolves the game host from the container.
    /// </summary>
    public IHost CreateHost() => Resolve<IHost>();

    public void Dispose()
    {
        if (_provider.IsValueCreated)
        {
            _provider.Value.Dispose();
        }
    }
}
