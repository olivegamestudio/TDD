using Microsoft.Extensions.DependencyInjection;

namespace Pilgrimage;

/// <summary>
/// Registers the Pilgrimage quest system.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the quest session. The game supplies the content by registering an
    /// <see cref="ICampaign"/>; without one the session cannot be resolved.
    /// </summary>
    /// <remarks>
    /// The session is a singleton because the player and their quests are the game's state, shared
    /// by everything that reads or advances them.
    /// </remarks>
    /// <param name="services">The collection to add the quest system to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddPilgrimage(this IServiceCollection services)
    {
        services.AddSingleton<IGameSession, GameSession>();

        return services;
    }
}
