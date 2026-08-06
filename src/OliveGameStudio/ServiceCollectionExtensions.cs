using Microsoft.Extensions.DependencyInjection;

namespace OliveGameStudio;

/// <summary>
/// Registers the engine services that any game built on Olive Game Studio needs.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default engine services: screen direction, UI control, save progress, input
    /// routing and the ship input it writes to, frame time filtering, and the camera the world is
    /// drawn through.
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

            // Where the ship's controls are left for the physics to pick up. It reads as nobody at
            // the controls until something routes a frame into it, because the device is the
            // platform host's to own and it may never push one.
            .AddSingleton<RoutedShipInput>()
            .AddSingleton<IShipInput>(services => services.GetRequiredService<RoutedShipInput>())

            // And where the frame's other asks are left: to talk to somebody, or to be out of
            // whatever has the controls. Separate from the ship's controls because these are
            // presses rather than states, and nothing that flies a ship should have to know that.
            .AddSingleton<RoutedInteraction>()

            // The conversation the player is in, if they are in one. Engine state rather than
            // game state: who is talking and what they say is content, but there being one
            // conversation at a time is not.
            .AddSingleton<Conversations>()

            // Registered by hand rather than by type so the dead zone is a stated argument. A host
            // that knows its own hardware registers another after this call; one that says nothing
            // gets InputRouter.DefaultDeadZone rather than a stick read raw.
            .AddSingleton<IInputRouter>(services => new InputRouter(
                services.GetRequiredService<IUIController>(),
                services.GetRequiredService<RoutedShipInput>(),
                services.GetRequiredService<RoutedInteraction>()))

            // one camera for the whole game: everything drawn in the world has to agree about
            // where the viewport is, or two things at the same world position draw apart
            .AddSingleton<ICamera, Camera2D>()

            // real time straight through; register another after this call to scale or pause
            .AddSingleton<IFrameTimeController, PassThroughFrameTimeController>();

        return services;
    }
}
