using Microsoft.Extensions.DependencyInjection;

namespace OliveGameStudio;

/// <summary>
/// Registers the engine services that any game built on Olive Game Studio needs.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default engine services: screen direction, UI control, save progress, ship input,
    /// and frame time filtering.
    /// </summary>
    /// <remarks>
    /// Services are registered with <c>AddSingleton</c>, so a caller that registers its own
    /// implementation <em>after</em> calling this method wins. That registration is the seam
    /// for choosing a different implementation, including a different
    /// <see cref="IFrameTimeController"/> per build.
    /// </remarks>
    /// <param name="services">The collection to add the engine services to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddOliveGameStudio(this IServiceCollection services)
    {
        services
            .AddSingleton<IScreenDirector, LifecycleScreenDirector>()
            .AddSingleton<IUIController, UIController>()
            .AddSingleton<ISaveProgressService, LocalSaveProgressService>()

            // nobody at the controls until the platform host binds a real keyboard or gamepad,
            // because the device is the host's to own; register another after this call
            .AddSingleton<IShipInput, NeutralShipInput>()

            // real time straight through; register another after this call to scale or pause
            .AddSingleton<IFrameTimeController, PassThroughFrameTimeController>();

        return services;
    }
}
