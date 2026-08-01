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
    /// <see cref="CompanyScreenOptions.SectionName"/> and <see cref="DisplayOptions.SectionName"/>
    /// sections of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The collection to add the game to.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <param name="configure">
    /// Optional code configuration, applied after binding so it overrides the configured values.
    /// </param>
    /// <param name="configureDisplay">
    /// Optional code configuration of what the game says about the screen, applied after binding
    /// for the same reason.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddBattleForce(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CompanyScreenOptions>? configure = null,
        Action<DisplayOptions>? configureDisplay = null)
    {
        services.Configure<CompanyScreenOptions>(configuration.GetSection(CompanyScreenOptions.SectionName));
        services.Configure<DisplayOptions>(configuration.GetSection(DisplayOptions.SectionName));

        return services.AddBattleForce(configure, configureDisplay);
    }

    /// <summary>
    /// Adds the game host and the engine services it depends on.
    /// </summary>
    /// <param name="services">The collection to add the game to.</param>
    /// <param name="configure">Optional configuration of <see cref="CompanyScreenOptions"/>.</param>
    /// <param name="configureDisplay">Optional configuration of <see cref="DisplayOptions"/>.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddBattleForce(
        this IServiceCollection services,
        Action<CompanyScreenOptions>? configure = null,
        Action<DisplayOptions>? configureDisplay = null)
    {
        services.AddOptions<CompanyScreenOptions>()
            .Validate(
                options => options.Duration >= TimeSpan.Zero,
                "CompanyScreen:Duration cannot be negative.");

        // Refused here rather than left to the first thing that measures itself against it: a
        // screen of zero, or of NaN, is not a display anyone can draw for, and a game that starts
        // on one only says so later and somewhere else.
        services.AddOptions<DisplayOptions>()
            .Validate(
                options => float.IsFinite(options.WidestSupportedViewportInPixels)
                    && options.WidestSupportedViewportInPixels > 0f,
                $"{DisplayOptions.SectionName}:"
                + $"{nameof(DisplayOptions.WidestSupportedViewportInPixels)} must be a finite "
                + "positive number of pixels.")
            .Validate(
                options => float.IsFinite(options.NarrowestSupportedViewportInPixels)
                    && options.NarrowestSupportedViewportInPixels > 0f,
                $"{DisplayOptions.SectionName}:"
                + $"{nameof(DisplayOptions.NarrowestSupportedViewportInPixels)} must be a finite "
                + "positive number of pixels.")

            // A contradiction rather than a tight bound: the two screens are what a star layer is
            // held between, so declaring them the wrong way round leaves no tile size that clears
            // both. Caught in the declaration rather than discovered as a bound that accepts
            // nothing.
            .Validate(
                options => !(options.NarrowestSupportedViewportInPixels
                    > options.WidestSupportedViewportInPixels),
                $"{DisplayOptions.SectionName}:"
                + $"{nameof(DisplayOptions.NarrowestSupportedViewportInPixels)} cannot be wider "
                + $"than {nameof(DisplayOptions.WidestSupportedViewportInPixels)}.");

        if (configure is not null)
        {
            services.Configure(configure);
        }

        if (configureDisplay is not null)
        {
            services.Configure(configureDisplay);
        }

        services
            .AddOliveGameStudio()

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
