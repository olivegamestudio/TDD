using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    /// <see cref="CompanyScreenOptions.SectionName"/> and <see cref="DisplayOptions.SectionName"/>
    /// sections of <paramref name="configuration"/>.
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
        services.Configure<DisplayOptions>(configuration.GetSection(DisplayOptions.SectionName));

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

        // A display of nothing is not a narrower screen, it is a screen that no content can be
        // held against: every floor derived from it collapses to zero and the checks taken against
        // it stop rejecting anything. Refused here rather than defended against at each use, so
        // there is one answer to "what does the game support" and it is always a real screen.
        services.AddOptions<DisplayOptions>()
            .Validate(
                options => options.WidestSupportedViewportInPixels > 0f,
                "Display:WidestSupportedViewportInPixels must be positive.")
            .Validate(
                options => float.IsFinite(options.WidestSupportedViewportInPixels),
                "Display:WidestSupportedViewportInPixels must be a real width, not infinity.");

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services
            .AddOliveGameStudio()

            // The display the game says it supports, resolved to the value rather than left behind
            // IOptions: what reads it is content, and content should not have to know the game
            // uses the options pattern to arrive at it.
            .AddSingleton(provider => provider.GetRequiredService<IOptions<DisplayOptions>>().Value)

            // the ship the game is flown in: the engine owns the physics, the game owns the
            // numbers that decide how it handles
            .AddSingleton(DisgracedShip.Handling)
            .AddSingleton<ShipMovement>()

            // the quest content, the world its markers stand in, and the watcher that measures
            // the player against them
            .AddSingleton<ICampaign, BattleForceCampaign>()
            .AddSingleton<IWorld, BattleForceWorld>()
            .AddSingleton<QuestProximityWatcher>()
            .AddSingleton<IGameSession, GameSession>()

            .AddSingleton<ICompanyScreen, CompanyScreen>()
            .AddSingleton<IMenuScreen, MenuScreen>()
            .AddSingleton<IGameScreen, GameScreen>()
            .AddSingleton<IShipView, ShipView>()

            // Registered as itself rather than behind an interface. Nothing outside the drawing
            // sets anything on it — it reads the camera and nothing else — so an interface would
            // be a name for the container's benefit and no one else's.
            .AddSingleton<StarField>()
            .AddSingleton<IHost, BattleForceHost>();

        return services;
    }
}
