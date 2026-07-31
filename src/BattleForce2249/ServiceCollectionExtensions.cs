using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// Registers Battle Force 2249 on top of the Olive Game Studio engine services.
/// </summary>
public static class BattleForceServiceCollectionExtensions
{
    /// <summary>
    /// Adds the game host and the engine services it depends on, binding options from the
    /// <see cref="CompanyScreenOptions.SectionName"/> section of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The collection to add the game to.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <param name="configure">
    /// Optional code configuration, applied after binding so it overrides the configured values.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddBattleForce(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CompanyScreenOptions>? configure = null)
    {
        services.Configure<CompanyScreenOptions>(configuration.GetSection(CompanyScreenOptions.SectionName));

        return services.AddBattleForce(configure);
    }

    /// <summary>
    /// Adds the game host and the engine services it depends on.
    /// </summary>
    /// <param name="services">The collection to add the game to.</param>
    /// <param name="configure">Optional configuration of <see cref="CompanyScreenOptions"/>.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddBattleForce(
        this IServiceCollection services,
        Action<CompanyScreenOptions>? configure = null)
    {
        services.AddOptions<CompanyScreenOptions>()
            .Validate(
                options => options.Duration >= TimeSpan.Zero,
                "CompanyScreen:Duration cannot be negative.");

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services
            .AddOliveGameStudio()
            .AddPilgrimage()
            .AddSingleton<ICampaign, BattleForceCampaign>()
            .AddSingleton<ICompanyScreen, CompanyScreen>()
            .AddSingleton<IMenuScreen, MenuScreen>()
            .AddSingleton<IGameScreen, GameScreen>()
            .AddSingleton<IHost, BattleForceHost>();

        return services;
    }
}
